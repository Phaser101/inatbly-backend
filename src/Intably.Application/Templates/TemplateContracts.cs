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
    int Version,
    string Status,
    bool HasPublishedOnce,
    Guid OwnerGrg,
    string Owner,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyCollection<TemplateInformationFieldDetails> InformationFields,
    IReadOnlyCollection<TemplateStepGroupDetails> Groups,
    IReadOnlyCollection<TemplateStepDetails> Steps);

public sealed record TemplateStepGroupDetails(
    Guid Ptsgrg,
    string Name,
    string Description,
    int Order,
    string ExecutionMode,
    IReadOnlyCollection<Guid> PrerequisitePtsgrgs);

public sealed record TemplateInformationFieldDetails(
    Guid Rfrg,
    string Label,
    string Type,
    bool Required,
    string Placeholder,
    string Kind,
    bool Pinned,
    Guid? ProducingPtsrg,
    IReadOnlyCollection<string> Options);

public sealed record TemplateStepDetails(
    Guid Ptsrg,
    Guid Ptsgrg,
    int Order,
    string Title,
    Guid? RequiredRoleFrrg,
    string? RequiredRole,
    string Instructions,
    string? SupportingUrl,
    Guid? DefaultAssigneeGrg,
    string? DefaultAssignee,
    int? DueOffsetDays,
    bool NoteRequired);
