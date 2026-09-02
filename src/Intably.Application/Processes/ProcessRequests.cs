namespace Intably.Application.Processes;

public sealed record StartProcessRequest(
    Guid Ptrg,
    string Name,
    IReadOnlyCollection<StartProcessInformationValue> InformationValues);

public sealed record StartProcessInformationValue(Guid Rfrg, string Value);

public sealed record UpdateProcessInformationRequest(
    string Value,
    string RowVersion);

public sealed record SetProcessStepStatusRequest(
    string Status,
    string? Note,
    string RowVersion);

public sealed record AssignProcessStepRequest(
    Guid? AssigneeGrg,
    string RowVersion);

public sealed record CloseProcessRequest(string FinalNote, string RowVersion);
