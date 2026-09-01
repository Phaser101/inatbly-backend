using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class TemplateVersion : Entity
{
    private readonly List<TemplateRequestField> _requestFields = [];
    private readonly List<TemplateStep> _steps = [];

    private TemplateVersion()
    {
    }

    private TemplateVersion(
        Guid templateId,
        int version,
        string name,
        string description,
        bool requireSequentialSteps,
        DateTimeOffset createdAtUtc)
    {
        TemplateId = templateId;
        Version = version;
        Name = name;
        Description = description;
        RequireSequentialSteps = requireSequentialSteps;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TemplateId { get; private set; }

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool RequireSequentialSteps { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public IReadOnlyCollection<TemplateRequestField> RequestFields => _requestFields;

    public IReadOnlyCollection<TemplateStep> Steps => _steps;

    internal static TemplateVersion Create(
        Guid templateId,
        int version,
        string name,
        string description,
        bool requireSequentialSteps,
        DateTimeOffset createdAtUtc)
    {
        return new TemplateVersion(
            templateId,
            version,
            name,
            description,
            requireSequentialSteps,
            createdAtUtc);
    }

    public void AddRequestField(
        int order,
        string label,
        RequestFieldType type,
        bool isRequired,
        string placeholder,
        RequestFieldSource source,
        IEnumerable<string> options)
    {
        _requestFields.Add(
            TemplateRequestField.Create(
                Id,
                order,
                label,
                type,
                isRequired,
                placeholder,
                source,
                options));
    }

    public void AddStep(
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
        _steps.Add(
            TemplateStep.Create(
                Id,
                order,
                title,
                requiredRoleId,
                requiredRoleName,
                instructions,
                supportingUrl,
                defaultAssigneeUserId,
                defaultAssigneeName,
                dueOffsetDays,
                noteRequired));
    }

    internal void Publish(DateTimeOffset publishedAtUtc)
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException(
                "A template must contain at least one step before publishing.");
        }

        if (_steps.Any(step => string.IsNullOrWhiteSpace(step.Title)))
        {
            throw new InvalidOperationException(
                "Every template step must have a title.");
        }

        if (_requestFields.Any(field => string.IsNullOrWhiteSpace(field.Label)))
        {
            throw new InvalidOperationException(
                "Every request field must have a label.");
        }

        if (_requestFields.Any(field =>
                field.Type == RequestFieldType.Select
                && !field.Options.Any(option =>
                    !string.IsNullOrWhiteSpace(option.Value))))
        {
            throw new InvalidOperationException(
                "Every select request field must contain at least one option.");
        }

        IsPublished = true;
        PublishedAtUtc = publishedAtUtc;
    }
}
