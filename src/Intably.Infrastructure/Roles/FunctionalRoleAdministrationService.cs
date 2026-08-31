using Intably.Application.Administration;
using Intably.Application.Roles;
using Intably.Domain.Roles;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Roles;

internal sealed class FunctionalRoleAdministrationService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : IFunctionalRoleAdministrationService
{
    public async Task<FunctionalRoleLookup> CreateAsync(
        SaveFunctionalRoleRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var role = FunctionalRole.Create(
            name,
            request.Description,
            timeProvider.GetUtcNow());
        dbContext.FunctionalRoles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(role);
    }

    public async Task<FunctionalRoleLookup> UpdateAsync(
        Guid frrg,
        SaveFunctionalRoleRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var role = await dbContext.FunctionalRoles.FindAsync([frrg], cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"Functional role '{frrg}' was not found.");
        await EnsureNameAvailableAsync(request.Name.Trim(), frrg, cancellationToken);

        try
        {
            role.Update(request.Name, request.Description);
        }
        catch (InvalidOperationException exception)
        {
            throw new AdministrationConflictException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(role);
    }

    public async Task ArchiveAsync(
        Guid frrg,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.FunctionalRoles.FindAsync([frrg], cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"Functional role '{frrg}' was not found.");

        try
        {
            role.Archive();
        }
        catch (InvalidOperationException exception)
        {
            throw new AdministrationConflictException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameAvailableAsync(
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.FunctionalRoles.AnyAsync(
            role => role.Name == name && role.Id != excludedId,
            cancellationToken))
        {
            throw new AdministrationConflictException(
                $"A functional role named '{name}' already exists.");
        }
    }

    private static void Validate(SaveFunctionalRoleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AdministrationValidationException(
                "A role name is required.");
        }

        if (request.Name.Trim().Length > 100)
        {
            throw new AdministrationValidationException(
                "Role names cannot exceed 100 characters.");
        }

        if (request.Description is null)
        {
            throw new AdministrationValidationException(
                "A role description is required.");
        }

        if (request.Description.Trim().Length > 500)
        {
            throw new AdministrationValidationException(
                "Role descriptions cannot exceed 500 characters.");
        }
    }

    private static FunctionalRoleLookup Map(FunctionalRole role)
    {
        return new FunctionalRoleLookup(
            role.Id,
            role.Name,
            role.Description,
            role.IsArchived ? "Archived" : "Active",
            role.CreatedAtUtc);
    }
}
