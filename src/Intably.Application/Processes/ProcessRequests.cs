namespace Intably.Application.Processes;

public sealed record StartProcessRequest(
    Guid Ptrg,
    string Name,
    IReadOnlyCollection<StartProcessRequestValue> RequestValues);

public sealed record StartProcessRequestValue(Guid Rfrg, string Value);

public sealed record SetProcessStepStatusRequest(
    string Status,
    string? Note,
    string RowVersion);

public sealed record AssignProcessStepRequest(
    Guid? AssigneeGrg,
    string RowVersion);

public sealed record CloseProcessRequest(string FinalNote, string RowVersion);
