namespace Intably.Application.Processes;

public sealed record ProcessSummary(
    Guid Pirg,
    Guid Ptrg,
    string Name,
    string TemplateName,
    int TemplateVersion,
    string Status,
    string Context,
    Guid OwnerGrg,
    string Owner,
    int CompletedStepCount,
    int BlockedStepCount,
    int StepCount,
    IReadOnlyCollection<string> Assignees,
    IReadOnlyCollection<string> StepStatuses,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string FinalNote,
    string RowVersion);

public sealed record ProcessDetails(
    Guid Pirg,
    Guid Ptrg,
    string Name,
    string TemplateName,
    int TemplateVersion,
    string Status,
    string Context,
    Guid OwnerGrg,
    string Owner,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    Guid? ClosedByGrg,
    string? ClosedBy,
    string FinalNote,
    string RowVersion,
    IReadOnlyCollection<ProcessRequestValueDetails> RequestValues,
    IReadOnlyCollection<ProcessStepDetails> Steps);

public sealed record ProcessRequestValueDetails(
    Guid Rfrg,
    string Label,
    string Type,
    bool Required,
    string Value);

public sealed record ProcessStepDetails(
    Guid Psrg,
    Guid Ptsrg,
    int Order,
    string Title,
    Guid? RequiredRoleFrrg,
    string RequiredRole,
    string Instructions,
    string? SupportingUrl,
    Guid? AssigneeGrg,
    string? Assignee,
    string Status,
    DateTimeOffset? DueAtUtc,
    bool NoteRequired,
    Guid? ExecutorGrg,
    string? Executor,
    DateTimeOffset? CompletedAtUtc,
    string ExecutionNote,
    string BlockedReason,
    string RowVersion);

public sealed record EligibleAssignee(
    Guid Grg,
    string DisplayName,
    string Email);

public sealed record ProcessTimelineEvent(
    Guid Aerg,
    Guid? Psrg,
    Guid ActorGrg,
    string Actor,
    string Action,
    string AffectedItem,
    string? BeforeValue,
    string? AfterValue,
    string? Note,
    DateTimeOffset OccurredAtUtc);
