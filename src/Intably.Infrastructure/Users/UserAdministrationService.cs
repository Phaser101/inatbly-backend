using System.Data;
using Intably.Application.Administration;
using Intably.Application.Users;
using Intably.Domain.Permissions;
using Intably.Domain.Roles;
using Intably.Domain.Users;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Users;

internal sealed class UserAdministrationService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : IUserAdministrationService
{
    public async Task<UserLookup> ReplaceFunctionalRolesAsync(
        Guid grg,
        ReplaceUserFunctionalRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FunctionalRoleIds);

        var requestedIds = request.FunctionalRoleIds.ToArray();
        if (requestedIds.Distinct().Count() != requestedIds.Length)
        {
            throw new AdministrationConflictException(
                "A functional role can only be assigned once.");
        }

        var user = await dbContext.Users.FindAsync([grg], cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"User '{grg}' was not found.");
        var requestedRoles = await dbContext.FunctionalRoles
            .Where(role => requestedIds.Contains(role.Id))
            .ToArrayAsync(cancellationToken);
        var missingRoleId = requestedIds.FirstOrDefault(
            id => requestedRoles.All(role => role.Id != id));
        if (missingRoleId != Guid.Empty)
        {
            throw new AdministrationNotFoundException(
                $"Functional role '{missingRoleId}' was not found.");
        }

        var archivedRole = requestedRoles.FirstOrDefault(role => role.IsArchived);
        if (archivedRole is not null)
        {
            throw new AdministrationConflictException(
                $"Archived functional role '{archivedRole.Id}' cannot be assigned.");
        }

        var existing = await dbContext.UserFunctionalRoles
            .Where(membership => membership.UserId == grg)
            .ToArrayAsync(cancellationToken);
        dbContext.UserFunctionalRoles.RemoveRange(existing);
        dbContext.UserFunctionalRoles.AddRange(
            requestedIds.Select(
                roleId => new UserFunctionalRole(
                    grg,
                    roleId,
                    timeProvider.GetUtcNow())));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetUserAsync(user, cancellationToken);
    }

    public async Task<UserLookup> SetActiveAsync(
        Guid grg,
        SetUserActiveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var user = await dbContext.Users.FindAsync([grg], cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"User '{grg}' was not found.");

        if (!request.Active && user.IsActive)
        {
            await EnsureAnotherPermissionsAdministratorAsync(grg, cancellationToken);
        }

        try
        {
            if (request.Active)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new AdministrationConflictException(exception.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetUserAsync(user, cancellationToken);
    }

    private async Task EnsureAnotherPermissionsAdministratorAsync(
        Guid excludedUserId,
        CancellationToken cancellationToken)
    {
        var userHasPermission = await dbContext.PermissionGrants.AnyAsync(
            grant =>
                grant.UserId == excludedUserId
                && grant.Permission == ApplicationPermission.ManagePermissions,
            cancellationToken);
        if (!userHasPermission)
        {
            return;
        }

        var anotherExists = await (
            from grant in dbContext.PermissionGrants
            join user in dbContext.Users on grant.UserId equals user.Id
            where
                grant.Permission == ApplicationPermission.ManagePermissions
                && grant.UserId != excludedUserId
                && user.IsActive
            select grant.Id
        ).AnyAsync(cancellationToken);

        if (!anotherExists)
        {
            throw new AdministrationConflictException(
                "The final active MANAGE_PERMISSIONS administrator cannot be deactivated.");
        }
    }

    private async Task<UserLookup> GetUserAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var roles = await (
            from membership in dbContext.UserFunctionalRoles.AsNoTracking()
            join role in dbContext.FunctionalRoles.AsNoTracking()
                on membership.FunctionalRoleId equals role.Id
            where membership.UserId == user.Id
            orderby role.Name
            select new UserLookupRole(
                role.Id,
                role.Name,
                role.IsArchived ? "Archived" : "Active")
        ).ToArrayAsync(cancellationToken);

        return new UserLookup(
            user.Id,
            user.DisplayName,
            user.Email,
            user.IsActive,
            roles);
    }
}
