using Intably.Domain.Common;

namespace Intably.Domain.Processes;

public sealed class ProcessStep : Entity
{
    private ProcessStep()
    {
    }

    internal ProcessStep(
        Guid processId,
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
        ProcessId = processId;
        SourceTemplateStepId = sourceTemplateStepId;
        ProcessStepGroupId = processStepGroupId;
        Order = order;
        Title = title;
        RequiredRoleId = requiredRoleId;
        RequiredRoleName = requiredRoleName;
        Instructions = instructions;
        SupportingUrl = supportingUrl;
        AssigneeUserId = assigneeUserId;
        AssigneeDisplayName = assigneeDisplayName;
        DueAtUtc = dueAtUtc;
        NoteRequired = noteRequired;
    }

    public Guid ProcessId { get; private set; }

    public Guid SourceTemplateStepId { get; private set; }

    public Guid ProcessStepGroupId { get; private set; }

    public int Order { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid? RequiredRoleId { get; private set; }

    public string RequiredRoleName { get; private set; } = string.Empty;

    public string Instructions { get; private set; } = string.Empty;

    public string? SupportingUrl { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public string? AssigneeDisplayName { get; private set; }

    public ProcessStepStatus Status { get; private set; }

    public DateTimeOffset? DueAtUtc { get; private set; }

    public bool NoteRequired { get; private set; }

    public Guid? ExecutorUserId { get; private set; }

    public string? ExecutorDisplayName { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string ExecutionNote { get; private set; } = string.Empty;

    public string BlockedReason { get; private set; } = string.Empty;

    public byte[] RowVersion { get; private set; } = [];

    internal void Assign(Guid? assigneeUserId, string? assigneeDisplayName)
    {
        AssigneeUserId = assigneeUserId;
        AssigneeDisplayName = assigneeDisplayName;
    }

    internal void SetStatus(
        ProcessStepStatus status,
        Guid actorUserId,
        string actorDisplayName,
        string? note,
        DateTimeOffset occurredAtUtc)
    {
        if (!CanTransition(Status, status))
        {
            throw new InvalidOperationException(
                $"A step cannot transition from {Status} to {status}.");
        }

        if (status == ProcessStepStatus.Blocked && string.IsNullOrWhiteSpace(note))
        {
            throw new InvalidOperationException(
                "A blocked step requires a reason.");
        }

        if (
            status == ProcessStepStatus.Complete
            && NoteRequired
            && string.IsNullOrWhiteSpace(note))
        {
            throw new InvalidOperationException(
                "This step requires an execution note before completion.");
        }

        Status = status;
        BlockedReason =
            status == ProcessStepStatus.Blocked ? note!.Trim() : string.Empty;
        ExecutionNote =
            status == ProcessStepStatus.Complete ? note?.Trim() ?? string.Empty : string.Empty;
        ExecutorUserId =
            status == ProcessStepStatus.Complete ? actorUserId : null;
        ExecutorDisplayName =
            status == ProcessStepStatus.Complete ? actorDisplayName.Trim() : null;
        CompletedAtUtc =
            status == ProcessStepStatus.Complete ? occurredAtUtc : null;
    }

    private static bool CanTransition(
        ProcessStepStatus current,
        ProcessStepStatus next)
    {
        return (current, next) switch
        {
            (ProcessStepStatus.NotStarted, ProcessStepStatus.InProgress) => true,
            (ProcessStepStatus.NotStarted, ProcessStepStatus.Blocked) => true,
            (ProcessStepStatus.NotStarted, ProcessStepStatus.Complete) => true,
            (ProcessStepStatus.InProgress, ProcessStepStatus.Blocked) => true,
            (ProcessStepStatus.InProgress, ProcessStepStatus.Complete) => true,
            (ProcessStepStatus.Blocked, ProcessStepStatus.InProgress) => true,
            (ProcessStepStatus.Blocked, ProcessStepStatus.Complete) => true,
            (ProcessStepStatus.Complete, ProcessStepStatus.InProgress) => true,
            _ => false,
        };
    }
}
