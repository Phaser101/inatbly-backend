using Intably.Domain.Common;

namespace Intably.Domain.Processes;

public sealed class ProcessInstance : Entity
{
    private readonly List<ProcessAuditEvent> _auditEvents = [];
    private readonly List<ProcessRequestValue> _requestValues = [];
    private readonly List<ProcessStepGroup> _stepGroups = [];
    private readonly List<ProcessStep> _steps = [];

    private ProcessInstance()
    {
    }

    private ProcessInstance(
        Guid templateId,
        int templateVersion,
        string templateName,
        string name,
        Guid ownerUserId,
        string ownerDisplayName,
        DateTimeOffset createdAtUtc)
    {
        TemplateId = templateId;
        TemplateVersion = templateVersion;
        TemplateName = templateName;
        Name = name;
        OwnerUserId = ownerUserId;
        OwnerDisplayName = ownerDisplayName;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TemplateId { get; private set; }

    public int TemplateVersion { get; private set; }

    public string TemplateName { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid OwnerUserId { get; private set; }

    public string OwnerDisplayName { get; private set; } = string.Empty;

    public ProcessStatus Status { get; private set; } = ProcessStatus.Open;

    public string Context { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public string? ClosedByDisplayName { get; private set; }

    public string FinalNote { get; private set; } = string.Empty;

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ProcessAuditEvent> AuditEvents => _auditEvents;

    public IReadOnlyCollection<ProcessRequestValue> InformationValues =>
        _requestValues;

    public IReadOnlyCollection<ProcessStepGroup> StepGroups => _stepGroups;

    public IReadOnlyCollection<ProcessStep> Steps => _steps;

    public static ProcessInstance Create(
        Guid templateId,
        int templateVersion,
        string templateName,
        string name,
        Guid ownerUserId,
        string ownerDisplayName,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerDisplayName);

        var process = new ProcessInstance(
            templateId,
            templateVersion,
            templateName.Trim(),
            name.Trim(),
            ownerUserId,
            ownerDisplayName.Trim(),
            createdAtUtc);

        process._auditEvents.Add(
            ProcessAuditEvent.Create(
                process.Id,
                ownerUserId,
                ownerDisplayName,
                "Process created",
                process.Name,
                createdAtUtc));

        return process;
    }

    public ProcessStepGroup AddStepGroup(
        Guid sourceTemplateStepGroupId,
        string name,
        string description,
        int order,
        StepGroupExecutionMode executionMode)
    {
        EnsureOpen();
        var group = new ProcessStepGroup(
            Id,
            sourceTemplateStepGroupId,
            name.Trim(),
            description.Trim(),
            order,
            executionMode);
        _stepGroups.Add(group);
        return group;
    }

    public void AddStepGroupPrerequisite(
        Guid groupId,
        Guid prerequisiteGroupId)
    {
        EnsureOpen();
        var group = GetStepGroup(groupId);
        group.AddPrerequisite(GetStepGroup(prerequisiteGroupId));
    }

    public ProcessStep AddStep(
        Guid sourceTemplateStepId,
        Guid processStepGroupId,
        int order,
        string title,
        Guid? requiredRoleId,
        string requiredRoleName,
        string instructions,
        string? supportingUrl,
        Guid? assigneeUserId,
        string? assigneeDisplayName,
        DateTimeOffset? dueAtUtc,
        bool noteRequired)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var step = new ProcessStep(
            Id,
            sourceTemplateStepId,
            processStepGroupId,
            order,
            title.Trim(),
            requiredRoleId,
            requiredRoleName.Trim(),
            instructions.Trim(),
            supportingUrl,
            assigneeUserId,
            assigneeDisplayName,
            dueAtUtc,
            noteRequired);

        _steps.Add(step);
        return step;
    }

    public void AddInformationValue(
        Guid sourceRequestFieldId,
        string label,
        string fieldType,
        bool isRequired,
        string value,
        int order,
        ProcessInformationKind kind,
        bool pinned,
        Guid? producingProcessStepId,
        IEnumerable<string> options,
        Guid? modifiedByUserId,
        string? modifiedByDisplayName,
        DateTimeOffset? modifiedAtUtc)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        _requestValues.Add(
            new ProcessRequestValue(
                Id,
                sourceRequestFieldId,
                label.Trim(),
                fieldType,
                isRequired,
                value.Trim(),
                order,
                kind,
                pinned,
                producingProcessStepId,
                options,
                modifiedByUserId,
                modifiedByDisplayName,
                modifiedAtUtc));

        RebuildContext();
    }

    public ProcessAuditEvent AssignStep(
        Guid stepId,
        Guid? assigneeUserId,
        string? assigneeDisplayName,
        Guid actorUserId,
        string actorDisplayName,
        DateTimeOffset occurredAtUtc)
    {
        EnsureOpen();
        var step = GetStep(stepId);
        var previousAssignee = step.AssigneeUserId;
        var previousAssigneeName = step.AssigneeDisplayName;

        if (previousAssignee == assigneeUserId)
        {
            throw new InvalidOperationException("The step already has this assignee.");
        }

        step.Assign(assigneeUserId, assigneeDisplayName);
        var auditEvent = ProcessAuditEvent.Create(
                Id,
                actorUserId,
                actorDisplayName,
                "Assignment changed",
                step.Title,
                occurredAtUtc,
                step.Id,
                previousAssigneeName ?? "Unassigned",
                assigneeDisplayName ?? "Unassigned");
        _auditEvents.Add(auditEvent);
        return auditEvent;
    }

    public ProcessAuditEvent SetStepStatus(
        Guid stepId,
        ProcessStepStatus status,
        Guid actorUserId,
        string actorDisplayName,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        EnsureOpen();
        var step = GetStep(stepId);
        var previousStatus = step.Status;

        if (status == ProcessStepStatus.Complete
            && _requestValues.Any(value =>
                value.Kind == ProcessInformationKind.StepOutput
                && value.ProducingProcessStepId == step.Id
                && value.IsRequired
                && string.IsNullOrWhiteSpace(value.Value)))
        {
            throw new InvalidOperationException(
                "Required process information must be populated before completing this step.");
        }

        EnsureSequentialTransition(step, status);
        step.SetStatus(
            status,
            actorUserId,
            actorDisplayName,
            note,
            occurredAtUtc);
        var auditEvent = ProcessAuditEvent.Create(
                Id,
                actorUserId,
                actorDisplayName,
                $"Step {status.ToString().ToLowerInvariant()}",
                step.Title,
                occurredAtUtc,
                step.Id,
                previousStatus.ToString(),
                status.ToString(),
                note?.Trim());
        _auditEvents.Add(auditEvent);
        return auditEvent;
    }

    public ProcessAuditEvent UpdateInformationValue(
        Guid rfrg,
        string value,
        Guid actorUserId,
        string actorDisplayName,
        DateTimeOffset occurredAtUtc)
    {
        EnsureOpen();
        var information = GetInformationValue(rfrg);
        var before = information.Value;
        information.Update(
            value.Trim(),
            actorUserId,
            actorDisplayName,
            occurredAtUtc);
        RebuildContext();
        var auditEvent = ProcessAuditEvent.Create(
            Id,
            actorUserId,
            actorDisplayName,
            "Process information updated",
            information.Label,
            occurredAtUtc,
            information.ProducingProcessStepId,
            before,
            information.Value);
        _auditEvents.Add(auditEvent);
        return auditEvent;
    }

    public bool CanUpdateStep(Guid stepId)
    {
        var step = GetStep(stepId);
        if (Status == ProcessStatus.Closed)
        {
            return false;
        }

        return step.Status switch
        {
            ProcessStepStatus.NotStarted => CanStart(step),
            ProcessStepStatus.Complete => CanReopen(step),
            _ => true,
        };
    }

    public ProcessAuditEvent Close(
        Guid actorUserId,
        string actorDisplayName,
        string finalNote,
        DateTimeOffset closedAtUtc)
    {
        if (Status == ProcessStatus.Closed)
        {
            throw new InvalidOperationException("The process is already closed.");
        }

        if (_steps.Any(step => step.Status != ProcessStepStatus.Complete))
        {
            throw new InvalidOperationException(
                "Every process step must be complete before closing the process.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(finalNote);
        Status = ProcessStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        ClosedByUserId = actorUserId;
        ClosedByDisplayName = actorDisplayName.Trim();
        FinalNote = finalNote.Trim();
        var auditEvent = ProcessAuditEvent.Create(
                Id,
                actorUserId,
                actorDisplayName,
                "Process closed",
                Name,
                closedAtUtc,
                note: FinalNote);
        _auditEvents.Add(auditEvent);
        return auditEvent;
    }

    private void EnsureOpen()
    {
        if (Status == ProcessStatus.Closed)
        {
            throw new InvalidOperationException(
                "A closed process cannot be changed.");
        }
    }

    private void EnsureSequentialTransition(
        ProcessStep step,
        ProcessStepStatus nextStatus)
    {
        if (step.Status == nextStatus)
        {
            return;
        }

        if (step.Status == ProcessStepStatus.NotStarted
            && nextStatus is (ProcessStepStatus.InProgress
                or ProcessStepStatus.Blocked
                or ProcessStepStatus.Complete)
            && !CanStart(step))
        {
            throw new InvalidOperationException(
                "The step's group prerequisites and sequence must be completed before this step can be updated.");
        }

        if (step.Status == ProcessStepStatus.Complete
            && nextStatus == ProcessStepStatus.InProgress
            && !CanReopen(step))
        {
            throw new InvalidOperationException(
                "A completed step cannot be reopened after a later or dependent step has started.");
        }
    }

    private bool CanStart(ProcessStep step)
    {
        var group = GetStepGroup(step.ProcessStepGroupId);
        if (group.PrerequisiteGroups.Any(prerequisite =>
                _steps.Any(candidate =>
                    candidate.ProcessStepGroupId == prerequisite.Id
                    && candidate.Status != ProcessStepStatus.Complete)))
        {
            return false;
        }

        return group.ExecutionMode != StepGroupExecutionMode.Sequential
            || _steps
                .Where(candidate =>
                    candidate.ProcessStepGroupId == group.Id
                    && candidate.Order < step.Order)
                .All(candidate => candidate.Status == ProcessStepStatus.Complete);
    }

    private bool CanReopen(ProcessStep step)
    {
        var group = GetStepGroup(step.ProcessStepGroupId);
        if (group.ExecutionMode == StepGroupExecutionMode.Sequential
            && _steps.Any(candidate =>
                candidate.ProcessStepGroupId == group.Id
                && candidate.Order > step.Order
                && candidate.Status != ProcessStepStatus.NotStarted))
        {
            return false;
        }

        var dependentGroupIds = GetTransitivelyDependentGroupIds(group.Id);
        return !_steps.Any(candidate =>
            dependentGroupIds.Contains(candidate.ProcessStepGroupId)
            && candidate.Status != ProcessStepStatus.NotStarted);
    }

    private HashSet<Guid> GetTransitivelyDependentGroupIds(Guid groupId)
    {
        var result = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(groupId);
        while (pending.TryDequeue(out var prerequisiteId))
        {
            foreach (var dependent in _stepGroups.Where(candidate =>
                         candidate.PrerequisiteGroups.Any(
                             prerequisite => prerequisite.Id == prerequisiteId)))
            {
                if (result.Add(dependent.Id))
                {
                    pending.Enqueue(dependent.Id);
                }
            }
        }

        return result;
    }

    private ProcessStep GetStep(Guid stepId)
    {
        return _steps.SingleOrDefault(step => step.Id == stepId)
            ?? throw new InvalidOperationException("The process step was not found.");
    }

    private ProcessRequestValue GetInformationValue(Guid rfrg)
    {
        return _requestValues.SingleOrDefault(
                value => value.SourceRequestFieldId == rfrg)
            ?? throw new InvalidOperationException(
                "The process information field was not found.");
    }

    private void RebuildContext()
    {
        var context = string.Join(
            " · ",
            _requestValues
                .Where(item => item.Pinned && !string.IsNullOrWhiteSpace(item.Value))
                .OrderBy(item => item.Order)
                .Take(4)
                .Select(item => $"{item.Label}: {item.Value}"));
        Context = context.Length <= 1000 ? context : context[..1000];
    }

    private ProcessStepGroup GetStepGroup(Guid groupId)
    {
        return _stepGroups.SingleOrDefault(group => group.Id == groupId)
            ?? throw new InvalidOperationException(
                "The process step group was not found.");
    }
}
