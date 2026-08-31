using Intably.Domain.Common;

namespace Intably.Domain.Processes;

public sealed class ProcessAuditEvent : Entity
{
    private ProcessAuditEvent()
    {
    }

    private ProcessAuditEvent(
        Guid processId,
        Guid actorUserId,
        string actorDisplayName,
        string action,
        string affectedItem,
        DateTimeOffset occurredAtUtc,
        Guid? processStepId,
        string? beforeValue,
        string? afterValue,
        string? note)
    {
        ProcessId = processId;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName.Trim();
        Action = action;
        AffectedItem = affectedItem;
        OccurredAtUtc = occurredAtUtc;
        ProcessStepId = processStepId;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        Note = note;
    }

    public Guid ProcessId { get; private set; }

    public Guid? ProcessStepId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string ActorDisplayName { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string AffectedItem { get; private set; } = string.Empty;

    public string? BeforeValue { get; private set; }

    public string? AfterValue { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static ProcessAuditEvent Create(
        Guid processId,
        Guid actorUserId,
        string actorDisplayName,
        string action,
        string affectedItem,
        DateTimeOffset occurredAtUtc,
        Guid? processStepId = null,
        string? beforeValue = null,
        string? afterValue = null,
        string? note = null)
    {
        return new ProcessAuditEvent(
            processId,
            actorUserId,
            actorDisplayName,
            action,
            affectedItem,
            occurredAtUtc,
            processStepId,
            beforeValue,
            afterValue,
            note);
    }
}
