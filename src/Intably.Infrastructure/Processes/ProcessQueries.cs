using Intably.Application.Processes;
using Intably.Domain.Processes;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Processes;

internal sealed partial class ProcessService
{
    public async Task<IReadOnlyCollection<ProcessSummary>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var processes = await dbContext.Processes
            .AsNoTracking()
            .OrderByDescending(process => process.CreatedAtUtc)
            .Select(process => new
            {
                Pirg = process.Id,
                Ptrg = process.TemplateId,
                process.Name,
                process.TemplateName,
                process.TemplateVersion,
                Status = process.Status.ToString(),
                process.Context,
                OwnerGrg = process.OwnerUserId,
                Owner = process.OwnerDisplayName,
                CompletedStepCount = process.Steps.Count(step =>
                    step.Status == ProcessStepStatus.Complete),
                BlockedStepCount = process.Steps.Count(step =>
                    step.Status == ProcessStepStatus.Blocked),
                StepCount = process.Steps.Count,
                process.CreatedAtUtc,
                process.ClosedAtUtc,
                process.FinalNote,
                process.RowVersion,
            })
            .ToArrayAsync(cancellationToken);

        var processIds = processes.Select(process => process.Pirg).ToArray();
        var stepFacets = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(step => processIds.Contains(step.ProcessId))
            .Select(step => new
            {
                step.ProcessId,
                step.AssigneeDisplayName,
                step.Status,
            })
            .ToArrayAsync(cancellationToken);
        var facetsByProcess = stepFacets
            .GroupBy(step => step.ProcessId)
            .ToDictionary(group => group.Key);

        return processes.Select(process =>
        {
            facetsByProcess.TryGetValue(process.Pirg, out var facets);
            return new ProcessSummary(
                process.Pirg,
                process.Ptrg,
                process.Name,
                process.TemplateName,
                process.TemplateVersion,
                process.Status,
                process.Context,
                process.OwnerGrg,
                process.Owner,
                process.CompletedStepCount,
                process.BlockedStepCount,
                process.StepCount,
                facets?
                    .Where(step => step.AssigneeDisplayName is not null)
                    .Select(step => step.AssigneeDisplayName!)
                    .Distinct()
                    .ToArray() ?? [],
                facets?
                    .Select(step => step.Status.ToString())
                    .Distinct()
                    .ToArray() ?? [],
                process.CreatedAtUtc,
                process.ClosedAtUtc,
                process.FinalNote,
                Convert.ToBase64String(process.RowVersion));
        }).ToArray();
    }

    public async Task<ProcessDetails> GetAsync(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        return MapDetails(await RequireProcessAsync(pirg, cancellationToken));
    }

    public async Task<IReadOnlyCollection<EligibleAssignee>>
        GetEligibleAssigneesAsync(
            Guid pirg,
            Guid psrg,
            CancellationToken cancellationToken)
    {
        var process = await RequireProcessAsync(pirg, cancellationToken);
        var step = RequireStep(process, psrg);
        var users = dbContext.Users.AsNoTracking().Where(user => user.IsActive);

        if (step.RequiredRoleId.HasValue)
        {
            var roleId = step.RequiredRoleId.Value;
            users =
                from user in users
                join membership in dbContext.UserFunctionalRoles
                    on user.Id equals membership.UserId
                join role in dbContext.FunctionalRoles
                    on membership.FunctionalRoleId equals role.Id
                where
                    membership.FunctionalRoleId == roleId
                    && !role.IsArchived
                select user;
        }

        return await users
            .OrderBy(user => user.DisplayName)
            .Select(user => new EligibleAssignee(
                user.Id,
                user.DisplayName,
                user.Email))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProcessTimelineEvent>> GetTimelineAsync(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Processes.AnyAsync(
                process => process.Id == pirg,
                cancellationToken))
        {
            throw new ProcessNotFoundException("The process was not found.");
        }

        return await dbContext.ProcessAuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.ProcessId == pirg)
            .OrderBy(auditEvent => auditEvent.OccurredAtUtc)
            .ThenBy(auditEvent => auditEvent.Id)
            .Select(auditEvent => new ProcessTimelineEvent(
                auditEvent.Id,
                auditEvent.ProcessStepId,
                auditEvent.ActorUserId,
                auditEvent.ActorDisplayName,
                auditEvent.Action,
                auditEvent.AffectedItem,
                auditEvent.BeforeValue,
                auditEvent.AfterValue,
                auditEvent.Note,
                auditEvent.OccurredAtUtc))
            .ToArrayAsync(cancellationToken);
    }
}
