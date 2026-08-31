using Intably.Application.Processes;
using Intably.Application.Users;
using Intably.Domain.Processes;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Processes;

internal sealed partial class ProcessService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : IProcessService
{
    private DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    private Task<ProcessInstance?> LoadProcessAsync(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        return dbContext.Processes
            .AsSplitQuery()
            .Include(process => process.RequestValues)
            .Include(process => process.Steps)
            .Include(process => process.AuditEvents)
            .SingleOrDefaultAsync(process => process.Id == pirg, cancellationToken);
    }

    private async Task<ProcessInstance> RequireProcessAsync(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        return await LoadProcessAsync(pirg, cancellationToken)
            ?? throw new ProcessNotFoundException("The process was not found.");
    }

    private static ProcessStep RequireStep(ProcessInstance process, Guid psrg)
    {
        return process.Steps.SingleOrDefault(step => step.Id == psrg)
            ?? throw new ProcessNotFoundException("The process step was not found.");
    }

    private static byte[] ParseRowVersion(string rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new ProcessValidationException("RowVersion is required.");
        }

        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ProcessValidationException(
                "RowVersion must be a valid base64 value.");
        }
    }

    private static void EnsureRowVersion(byte[] current, string submitted)
    {
        if (!current.SequenceEqual(ParseRowVersion(submitted)))
        {
            throw new ProcessConflictException(
                "The submitted RowVersion is stale. Refresh and retry.");
        }
    }

    private static bool CanManage(CurrentUserProfile actor)
    {
        return actor.Permissions.Contains("MANAGE_PROCESSES");
    }

    private async Task<bool> IsEligibleAsync(
        Guid userId,
        Guid? requiredRoleId,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.Users.AnyAsync(
            user => user.Id == userId && user.IsActive,
            cancellationToken);
        if (!active)
        {
            return false;
        }

        return !requiredRoleId.HasValue
            || await (
                from membership in dbContext.UserFunctionalRoles
                join role in dbContext.FunctionalRoles
                    on membership.FunctionalRoleId equals role.Id
                where
                    membership.UserId == userId
                    && membership.FunctionalRoleId == requiredRoleId
                    && !role.IsArchived
                select membership)
                .AnyAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ProcessConflictException(
                "The process was changed by another request. Refresh and retry.");
        }
    }
}
