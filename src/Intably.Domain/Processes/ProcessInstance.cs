using Intably.Domain.Common;

namespace Intably.Domain.Processes;

public sealed class ProcessInstance : Entity
{
    private readonly List<ProcessAuditEvent> _auditEvents = [];
    private readonly List<ProcessRequestValue> _requestValues = [];
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

    public IReadOnlyCollection<ProcessRequestValue> RequestValues => _requestValues;

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

    public ProcessStep AddStep(
        Guid sourceTemplateStepId,
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

    public void AddRequestValue(
        Guid sourceRequestFieldId,
        string label,
        string fieldType,
        bool isRequired,
        string value,
        int order)
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
                order));

        Context = string.Join(
            " · ",
            _requestValues
                .OrderBy(item => item.Order)
                .Take(2)
                .Select(item => $"{item.Label}: {item.Value}"));
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

    private ProcessStep GetStep(Guid stepId)
    {
        return _steps.SingleOrDefault(step => step.Id == stepId)
            ?? throw new InvalidOperationException("The process step was not found.");
    }
}
