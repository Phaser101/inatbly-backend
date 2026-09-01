using Intably.Application.Permissions;
using Intably.Application.Processes;
using Intably.Application.Users;
using Intably.Domain.Processes;
using Intably.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Processes;

internal sealed partial class ProcessService
{
    public async Task<ProcessDetails> StartAsync(
        StartProcessRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ProcessValidationException("The process name is required.");
        }

        if (request.Name.Trim().Length > 200)
        {
            throw new ProcessValidationException(
                "The process name cannot exceed 200 characters.");
        }

        var template = await dbContext.ProcessTemplates
            .AsSplitQuery()
            .Include(candidate => candidate.Versions)
                .ThenInclude(version => version.RequestFields)
                    .ThenInclude(field => field.Options)
            .Include(candidate => candidate.Versions)
                .ThenInclude(version => version.Steps)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == request.Ptrg
                    && candidate.Status == TemplateStatus.Active,
                cancellationToken)
            ?? throw new ProcessNotFoundException(
                "An active published template was not found.");
        var version = template.Versions.Single(candidate =>
            candidate.IsPublished
            && candidate.Version == template.PublishedVersion);

        var submittedValues = new Dictionary<Guid, string>();
        foreach (var submitted in request.RequestValues)
        {
            if (!submittedValues.TryAdd(submitted.Rfrg, submitted.Value ?? string.Empty))
            {
                throw new ProcessValidationException(
                    "Each request field can be submitted only once.");
            }
        }

        var fieldIds = version.RequestFields.Select(field => field.Id).ToHashSet();
        if (submittedValues.Keys.Any(id => !fieldIds.Contains(id)))
        {
            throw new ProcessValidationException(
                "A submitted request field does not belong to the published template.");
        }

        foreach (var field in version.RequestFields.Where(field => field.IsRequired))
        {
            if (!submittedValues.TryGetValue(field.Id, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                throw new ProcessValidationException(
                    $"A value is required for '{field.Label}'.");
            }
        }

        if (submittedValues.Values.Any(value => value.Trim().Length > 4000))
        {
            throw new ProcessValidationException(
                "Request values cannot exceed 4000 characters.");
        }

        var process = ProcessInstance.Create(
            template.Id,
            version.Version,
            version.Name,
            request.Name,
            actor.Grg,
            actor.DisplayName,
            UtcNow);

        foreach (var field in version.RequestFields.OrderBy(field => field.Order))
        {
            process.AddRequestValue(
                field.Id,
                field.Label,
                field.Type.ToString().ToLowerInvariant(),
                field.IsRequired,
                submittedValues.GetValueOrDefault(field.Id, string.Empty),
                field.Order);
        }

        foreach (var step in version.Steps.OrderBy(step => step.Order))
        {
            string? assigneeName = null;
            if (step.DefaultAssigneeUserId.HasValue)
            {
                var assignee = await dbContext.Users.SingleOrDefaultAsync(
                    user => user.Id == step.DefaultAssigneeUserId,
                    cancellationToken);
                if (assignee is null
                    || !await IsEligibleAsync(
                        assignee.Id,
                        step.RequiredRoleId,
                        cancellationToken))
                {
                    throw new ProcessValidationException(
                        $"The default assignee for '{step.Title}' is not eligible.");
                }

                assigneeName = assignee.DisplayName;
            }

            process.AddStep(
                step.Id,
                step.Order,
                step.Title,
                step.RequiredRoleId,
                step.RequiredRoleName,
                step.Instructions,
                step.SupportingUrl,
                step.DefaultAssigneeUserId,
                assigneeName,
                step.DueOffsetDays.HasValue
                    ? UtcNow.AddDays(step.DueOffsetDays.Value)
                    : null,
                step.NoteRequired);
        }

        dbContext.Processes.Add(process);
        await SaveAsync(cancellationToken);
        return MapDetails(process);
    }

    public async Task<ProcessDetails> SetStepStatusAsync(
        Guid pirg,
        Guid psrg,
        SetProcessStepStatusRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ProcessStepStatus>(
                request.Status,
                true,
                out var status))
        {
            throw new ProcessValidationException(
                $"Unknown process step status '{request.Status}'.");
        }

        var process = await RequireProcessAsync(pirg, cancellationToken);
        var step = RequireStep(process, psrg);
        if (!HasPermission(actor, PermissionContracts.UpdateProcessSteps))
        {
            var allowed = step.AssigneeUserId.HasValue
                ? step.AssigneeUserId == actor.Grg
                : await IsEligibleAsync(
                    actor.Grg,
                    step.RequiredRoleId,
                    cancellationToken);
            if (!allowed)
            {
                throw new ProcessForbiddenException(
                    "The current user cannot update this step.");
            }
        }

        EnsureRowVersion(step.RowVersion, request.RowVersion);
        try
        {
            var auditEvent = process.SetStepStatus(
                psrg,
                status,
                actor.Grg,
                actor.DisplayName,
                request.Note,
                UtcNow);
            dbContext.ProcessAuditEvents.Add(auditEvent);
        }
        catch (InvalidOperationException exception)
        {
            throw new ProcessConflictException(exception.Message);
        }

        await SaveAsync(cancellationToken);
        return MapDetails(process);
    }

    public async Task<ProcessDetails> AssignStepAsync(
        Guid pirg,
        Guid psrg,
        AssignProcessStepRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken)
    {
        var process = await RequireProcessAsync(pirg, cancellationToken);
        var step = RequireStep(process, psrg);
        if (process.OwnerUserId != actor.Grg
            && !HasPermission(actor, PermissionContracts.AssignProcessSteps))
        {
            throw new ProcessForbiddenException(
                "Only the process owner or a user with assignment permission can assign steps.");
        }

        string? assigneeName = null;
        if (request.AssigneeGrg.HasValue)
        {
            var assignee = await dbContext.Users.SingleOrDefaultAsync(
                user => user.Id == request.AssigneeGrg,
                cancellationToken);
            if (assignee is null)
            {
                throw new ProcessNotFoundException("The assignee was not found.");
            }

            if (!await IsEligibleAsync(
                    assignee.Id,
                    step.RequiredRoleId,
                    cancellationToken))
            {
                throw new ProcessValidationException(
                    "The selected user is not eligible for this step.");
            }

            assigneeName = assignee.DisplayName;
        }

        EnsureRowVersion(step.RowVersion, request.RowVersion);
        try
        {
            var auditEvent = process.AssignStep(
                psrg,
                request.AssigneeGrg,
                assigneeName,
                actor.Grg,
                actor.DisplayName,
                UtcNow);
            dbContext.ProcessAuditEvents.Add(auditEvent);
        }
        catch (InvalidOperationException exception)
        {
            throw new ProcessConflictException(exception.Message);
        }

        await SaveAsync(cancellationToken);
        return MapDetails(process);
    }

    public async Task<ProcessDetails> CloseAsync(
        Guid pirg,
        CloseProcessRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken)
    {
        var process = await RequireProcessAsync(pirg, cancellationToken);
        if (process.OwnerUserId != actor.Grg
            && !HasPermission(actor, PermissionContracts.CloseProcesses))
        {
            throw new ProcessForbiddenException(
                "Only the process owner or a user with close permission can close the process.");
        }

        EnsureRowVersion(process.RowVersion, request.RowVersion);
        try
        {
            var auditEvent = process.Close(
                actor.Grg,
                actor.DisplayName,
                request.FinalNote,
                UtcNow);
            dbContext.ProcessAuditEvents.Add(auditEvent);
        }
        catch (ArgumentException exception)
        {
            throw new ProcessValidationException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new ProcessConflictException(exception.Message);
        }

        await SaveAsync(cancellationToken);
        return MapDetails(process);
    }
}
