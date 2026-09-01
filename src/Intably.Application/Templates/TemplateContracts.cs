namespace Intably.Application.Templates;

public sealed record TemplateSummary(
    Guid Ptrg,
    string Name,
    string Description,
    int Version,
    string Status,
    bool HasUnpublishedChanges,
    int StepCount,
    Guid OwnerGrg,
    string Owner,
    DateTimeOffset UpdatedAtUtc);

public sealed record TemplateDetails(
    Guid Ptrg,
    string Name,
    string Description,
    bool RequireSequentialSteps,
    int Version,
    string Status,
    bool HasPublishedOnce,
    Guid OwnerGrg,
    string Owner,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyCollection<TemplateRequestFieldDetails> RequestFields,
    IReadOnlyCollection<TemplateStepDetails> Steps);

public sealed record TemplateRequestFieldDetails(
    Guid Rfrg,
    string Label,
    string Type,
    bool Required,
    string Placeholder,
    string Source,
    IReadOnlyCollection<string> Options);

public sealed record TemplateStepDetails(
    Guid Ptsrg,
    string Title,
    Guid? RequiredRoleFrrg,
    string? RequiredRole,
    string Instructions,
    string? SupportingUrl,
    Guid? DefaultAssigneeGrg,
    string? DefaultAssignee,
    int? DueOffsetDays,
    bool NoteRequired);
