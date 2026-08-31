namespace Intably.Application.Templates;

public sealed record SaveTemplateRequest(
    string Name,
    string Description,
    IReadOnlyCollection<SaveTemplateRequestField> RequestFields,
    IReadOnlyCollection<SaveTemplateStep> Steps);

public sealed record SaveTemplateRequestField(
    string Label,
    string Type,
    bool Required,
    string Placeholder,
    string Source,
    Guid? SourceFieldSetId,
    string? SourceFieldSetName,
    int? SourceFieldSetVersion,
    IReadOnlyCollection<string> Options);

public sealed record SaveTemplateStep(
    string Title,
    Guid? RequiredRoleFrrg,
    string RequiredRole,
    string Instructions,
    string? SupportingUrl,
    Guid? DefaultAssigneeGrg,
    string? DefaultAssignee,
    int? DueOffsetDays,
    bool NoteRequired);
