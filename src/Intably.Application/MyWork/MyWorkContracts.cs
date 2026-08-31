namespace Intably.Application.MyWork;

public sealed record MyWorkItem(
    Guid Psrg,
    Guid Pirg,
    string ProcessName,
    int StepOrder,
    string StepTitle,
    Guid? RequiredRoleFrrg,
    string RequiredRole,
    Guid? AssigneeGrg,
    string? Assignee,
    bool AssignedToCurrentUser,
    bool EligibleForCurrentUser,
    string Status,
    DateTimeOffset? DueAtUtc,
    Guid OwnerGrg,
    string Owner,
    bool RecentlyCompleted);
