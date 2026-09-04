namespace Intably.Application.Templates;

public sealed record SaveTemplateRequest(
    string Name,
    string Description,
    IReadOnlyCollection<SaveTemplateInformationField> InformationFields,
    IReadOnlyCollection<SaveTemplateStepGroup> Groups,
    IReadOnlyCollection<SaveTemplateStep> Steps);

public sealed record SaveTemplateStepGroup(
    Guid Ptsgrg,
    string Name,
    string Description,
    int Order,
    string ExecutionMode,
    IReadOnlyCollection<Guid> PrerequisitePtsgrgs);

public sealed record SaveTemplateInformationField(
    string Label,
    string Type,
    bool Required,
    string Placeholder,
    string Kind,
    bool Pinned,
    Guid? ProducingPtsrg,
    IReadOnlyCollection<string> Options);

public sealed record SaveTemplateStep(
    Guid Ptsrg,
    Guid Ptsgrg,
    int Order,
    string Title,
    Guid? RequiredRoleFrrg,
    string RequiredRole,
    string Instructions,
    string? SupportingUrl,
    Guid? DefaultAssigneeGrg,
    string? DefaultAssignee,
    int? DueOffsetDays,
    bool NoteRequired);
