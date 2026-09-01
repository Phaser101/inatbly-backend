using Intably.Application.MyWork;
using Intably.Application.Users;
using Intably.Domain.Processes;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.MyWork;

internal sealed class MyWorkService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : IMyWorkService
{
    public async Task<IReadOnlyCollection<MyWorkItem>> GetAsync(
        CurrentUserProfile currentUser,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUser.Grg;
        var completedSinceUtc = timeProvider.GetUtcNow().AddDays(-14);
        var currentUserRoleIds =
            from membership in dbContext.UserFunctionalRoles
            join role in dbContext.FunctionalRoles
                on membership.FunctionalRoleId equals role.Id
            where membership.UserId == currentUserId && !role.IsArchived
            select membership.FunctionalRoleId;

        return await (
            from step in dbContext.ProcessSteps.AsNoTracking()
            join process in dbContext.Processes.AsNoTracking()
                on step.ProcessId equals process.Id
            let eligible =
                !step.RequiredRoleId.HasValue
                || currentUserRoleIds.Contains(step.RequiredRoleId.Value)
            where
                (
                    process.Status == ProcessStatus.Open
                    && step.Status != ProcessStepStatus.Complete
                    && (
                        !process.RequireSequentialSteps
                        || step.Status != ProcessStepStatus.NotStarted
                        || !dbContext.ProcessSteps.Any(
                            prior =>
                                prior.ProcessId == process.Id
                                && prior.Order < step.Order
                                && prior.Status != ProcessStepStatus.Complete)
                    )
                    && (
                        step.AssigneeUserId == currentUserId
                        || (!step.AssigneeUserId.HasValue && eligible)
                    )
                )
                || (
                    step.Status == ProcessStepStatus.Complete
                    && step.ExecutorUserId == currentUserId
                    && step.CompletedAtUtc >= completedSinceUtc
                )
            orderby
                step.Status == ProcessStepStatus.Complete,
                step.DueAtUtc == null,
                step.DueAtUtc,
                process.CreatedAtUtc descending,
                step.Order,
                step.Id
            select new MyWorkItem(
                step.Id,
                process.Id,
                process.Name,
                step.Order,
                step.Title,
                step.RequiredRoleId,
                step.RequiredRoleName,
                step.AssigneeUserId,
                step.AssigneeDisplayName,
                step.AssigneeUserId == currentUserId,
                eligible,
                step.Status.ToString(),
                step.DueAtUtc,
                process.OwnerUserId,
                process.OwnerDisplayName,
                step.Status == ProcessStepStatus.Complete
                    && step.ExecutorUserId == currentUserId
                    && step.CompletedAtUtc >= completedSinceUtc))
            .ToArrayAsync(cancellationToken);
    }
}
