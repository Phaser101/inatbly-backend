using Intably.Application.Permissions;
using Intably.Application.Processes;
using Intably.Application.Users;
using Intably.Domain.Common;
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
            .Include(candidate => candidate.Versions)
                .ThenInclude(version => version.StepGroups)
                    .ThenInclude(group => group.PrerequisiteGroups)
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
        foreach (var submitted in request.InformationValues)
        {
            if (!submittedValues.TryAdd(submitted.Rfrg, submitted.Value ?? string.Empty))
            {
                throw new ProcessValidationException(
                    "Each request field can be submitted only once.");
            }
        }

        var launchFields = version.RequestFields
            .Where(field => field.Kind == ProcessInformationKind.LaunchInput)
            .ToArray();
        var fieldIds = launchFields.Select(field => field.Id).ToHashSet();
        if (submittedValues.Keys.Any(id => !fieldIds.Contains(id)))
        {
            throw new ProcessValidationException(
                "Only launch input fields can be submitted when starting a process.");
        }

        foreach (var field in launchFields.Where(field => field.IsRequired))
        {
            if (!submittedValues.TryGetValue(field.Id, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                throw new ProcessValidationException(
                    $"A value is required for '{field.Label}'.");
            }
        }

        var process = ProcessInstance.Create(
            template.Id,
            version.Version,
            version.Name,
            request.Name,
            actor.Grg,
            actor.DisplayName,
            UtcNow);

        var processGroupsByTemplateGroupId = new Dictionary<Guid, ProcessStepGroup>();
        foreach (var group in version.StepGroups.OrderBy(group => group.Order))
        {
            processGroupsByTemplateGroupId[group.Id] = process.AddStepGroup(
                group.Id,
                group.Name,
                group.Description,
                group.Order,
                group.ExecutionMode);
        }

        foreach (var group in version.StepGroups)
        {
            foreach (var prerequisite in group.PrerequisiteGroups)
            {
                process.AddStepGroupPrerequisite(
                    processGroupsByTemplateGroupId[group.Id].Id,
                    processGroupsByTemplateGroupId[prerequisite.Id].Id);
            }
        }

        var processStepsByTemplateStepId = new Dictionary<Guid, ProcessStep>();
        foreach (var step in version.Steps
                     .OrderBy(step => processGroupsByTemplateGroupId[
                         step.TemplateStepGroupId].Order)
                     .ThenBy(step => step.Order))
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

            processStepsByTemplateStepId[step.Id] = process.AddStep(
                step.Id,
                processGroupsByTemplateGroupId[step.TemplateStepGroupId].Id,
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

        foreach (var field in version.RequestFields.OrderBy(field => field.Order))
        {
            var value = field.Kind == ProcessInformationKind.StepOutput
                ? string.Empty
                : submittedValues.GetValueOrDefault(field.Id, string.Empty);
            ProcessInformationValidator.Validate(
                field.Type,
                field.IsRequired && field.Kind == ProcessInformationKind.LaunchInput,
                value,
                field.Options.Select(option => option.Value));
            process.AddInformationValue(
                field.Id,
                field.Label,
                field.Type.ToString().ToLowerInvariant(),
                field.IsRequired,
                value,
                field.Order,
                field.Kind,
                field.Pinned,
                field.ProducingTemplateStepId.HasValue
                    ? processStepsByTemplateStepId[
                        field.ProducingTemplateStepId.Value].Id
                    : null,
                field.Options
                    .OrderBy(option => option.Order)
                    .Select(option => option.Value),
                field.Kind == ProcessInformationKind.LaunchInput
                    ? actor.Grg
                    : null,
                field.Kind == ProcessInformationKind.LaunchInput
                    ? actor.DisplayName
                    : null,
                field.Kind == ProcessInformationKind.LaunchInput
                    ? UtcNow
                    : null);
        }

        dbContext.Processes.Add(process);
        await SaveAsync(cancellationToken);
        return MapDetails(process);
    }

    public async Task<ProcessDetails> UpdateInformationAsync(
        Guid pirg,
        Guid rfrg,
        UpdateProcessInformationRequest request,
        CurrentUserProfile actor,
        CancellationToken cancellationToken)
    {
        var process = await RequireProcessAsync(pirg, cancellationToken);
        var information = process.InformationValues.SingleOrDefault(
            value => value.SourceRequestFieldId == rfrg)
            ?? throw new ProcessNotFoundException(
                "The process information field was not found.");
        var hasGlobalAccess = HasPermission(
            actor,
            PermissionContracts.UpdateProcessInformation);
        var allowed = hasGlobalAccess
            || (
                information.Kind == ProcessInformationKind.LaunchInput
                && process.OwnerUserId == actor.Grg)
            || (
                information.Kind == ProcessInformationKind.StepOutput
                && await CanEditStepOutputAsync(
                    process,
                    information,
                    actor,
                    cancellationToken));
        if (!allowed)
        {
            throw new ProcessForbiddenException(
                "The current user cannot update this process information.");
        }

        if (!Enum.TryParse<RequestFieldType>(
                information.FieldType,
                true,
                out var type))
        {
            throw new ProcessValidationException(
                "The snapshotted process information type is invalid.");
        }

        ProcessInformationValidator.Validate(
            type,
            information.IsRequired,
            request.Value ?? string.Empty,
            information.Options);
        EnsureRowVersion(information.RowVersion, request.RowVersion);
        try
        {
            var auditEvent = process.UpdateInformationValue(
                rfrg,
                request.Value ?? string.Empty,
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
