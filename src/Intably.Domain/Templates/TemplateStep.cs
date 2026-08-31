using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class TemplateStep : Entity
{
    private TemplateStep()
    {
    }

    private TemplateStep(
        Guid templateVersionId,
        int order,
        string title,
        Guid? requiredRoleId,
        string requiredRoleName,
        string instructions,
        string? supportingUrl,
        Guid? defaultAssigneeUserId,
        string? defaultAssigneeName,
        int? dueOffsetDays,
        bool noteRequired)
    {
        TemplateVersionId = templateVersionId;
        Order = order;
        Title = title;
        RequiredRoleId = requiredRoleId;
        RequiredRoleName = requiredRoleName;
        Instructions = instructions;
        SupportingUrl = supportingUrl;
        DefaultAssigneeUserId = defaultAssigneeUserId;
        DefaultAssigneeName = defaultAssigneeName;
        DueOffsetDays = dueOffsetDays;
        NoteRequired = noteRequired;
    }

    public Guid TemplateVersionId { get; private set; }

    public int Order { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid? RequiredRoleId { get; private set; }

    public string RequiredRoleName { get; private set; } = string.Empty;

    public string Instructions { get; private set; } = string.Empty;

    public string? SupportingUrl { get; private set; }

    public Guid? DefaultAssigneeUserId { get; private set; }

    public string? DefaultAssigneeName { get; private set; }

    public int? DueOffsetDays { get; private set; }

    public bool NoteRequired { get; private set; }

    internal static TemplateStep Create(
        Guid templateVersionId,
        int order,
        string title,
        Guid? requiredRoleId,
        string requiredRoleName,
        string instructions,
        string? supportingUrl,
        Guid? defaultAssigneeUserId,
        string? defaultAssigneeName,
        int? dueOffsetDays,
        bool noteRequired)
    {
        return new TemplateStep(
            templateVersionId,
            order,
            title.Trim(),
            requiredRoleId,
            requiredRoleName.Trim(),
            instructions.Trim(),
            string.IsNullOrWhiteSpace(supportingUrl) ? null : supportingUrl.Trim(),
            defaultAssigneeUserId,
            string.IsNullOrWhiteSpace(defaultAssigneeName)
                ? null
                : defaultAssigneeName.Trim(),
            dueOffsetDays,
            noteRequired);
    }
}
