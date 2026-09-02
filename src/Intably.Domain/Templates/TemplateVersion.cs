using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class TemplateVersion : Entity
{
    private readonly List<TemplateRequestField> _requestFields = [];
    private readonly List<TemplateStepGroup> _stepGroups = [];
    private readonly List<TemplateStep> _steps = [];

    private TemplateVersion()
    {
    }

    private TemplateVersion(
        Guid templateId,
        int version,
        string name,
        string description,
        DateTimeOffset createdAtUtc)
    {
        TemplateId = templateId;
        Version = version;
        Name = name;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TemplateId { get; private set; }

    public int Version { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsPublished { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public IReadOnlyCollection<TemplateRequestField> RequestFields => _requestFields;

    public IReadOnlyCollection<TemplateStepGroup> StepGroups => _stepGroups;

    public IReadOnlyCollection<TemplateStep> Steps => _steps;

    internal static TemplateVersion Create(
        Guid templateId,
        int version,
        string name,
        string description,
        DateTimeOffset createdAtUtc)
    {
        return new TemplateVersion(
            templateId,
            version,
            name,
            description,
            createdAtUtc);
    }

    public TemplateStepGroup AddStepGroup(
        Guid id,
        string name,
        string description,
        int order,
        StepGroupExecutionMode executionMode)
    {
        var group = new TemplateStepGroup(
            id,
            Id,
            name,
            description,
            order,
            executionMode);
        _stepGroups.Add(group);
        return group;
    }

    public void AddStepGroupPrerequisite(Guid groupId, Guid prerequisiteGroupId)
    {
        var group = _stepGroups.Single(candidate => candidate.Id == groupId);
        var prerequisite = _stepGroups.Single(
            candidate => candidate.Id == prerequisiteGroupId);
        group.AddPrerequisite(prerequisite);
    }

    public void AddRequestField(
        int order,
        string label,
        RequestFieldType type,
        bool isRequired,
        string placeholder,
        RequestFieldSource source,
        ProcessInformationKind kind,
        bool pinned,
        Guid? producingTemplateStepId,
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
                kind,
                pinned,
                producingTemplateStepId,
                options));
    }

    public void AddStep(
        Guid id,
        Guid stepGroupId,
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
                id,
                Id,
                stepGroupId,
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
        if (_stepGroups.Count == 0)
        {
            throw new InvalidOperationException(
                "A template must contain at least one step group before publishing.");
        }

        if (_steps.Count == 0)
        {
            throw new InvalidOperationException(
                "A template must contain at least one step before publishing.");
        }

        if (_stepGroups.Any(group =>
                !_steps.Any(step => step.TemplateStepGroupId == group.Id)))
        {
            throw new InvalidOperationException(
                "Every template step group must contain at least one step.");
        }

        if (_steps.Any(step => string.IsNullOrWhiteSpace(step.Title)))
        {
            throw new InvalidOperationException(
                "Every template step must have a title.");
        }

        if (_stepGroups.Any(group => string.IsNullOrWhiteSpace(group.Name)))
        {
            throw new InvalidOperationException(
                "Every template step group must have a name.");
        }

        if (_stepGroups
            .GroupBy(group => group.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "Template step group names must be unique.");
        }

        if (_requestFields.Any(field => string.IsNullOrWhiteSpace(field.Label)))
        {
            throw new InvalidOperationException(
                "Every request field must have a label.");
        }

        if (_requestFields.Count(field => field.Pinned) > 4)
        {
            throw new InvalidOperationException(
                "No more than four process information fields can be pinned.");
        }

        if (_requestFields.Any(field =>
                field.Kind == ProcessInformationKind.LaunchInput
                && field.ProducingTemplateStepId.HasValue))
        {
            throw new InvalidOperationException(
                "Launch input fields cannot have a producing step.");
        }

        if (_requestFields.Any(field =>
                field.Kind == ProcessInformationKind.StepOutput
                && (!field.ProducingTemplateStepId.HasValue
                    || !_steps.Any(step =>
                        step.Id == field.ProducingTemplateStepId.Value))))
        {
            throw new InvalidOperationException(
                "Step output fields must reference a step in this template.");
        }

        if (_requestFields.Any(field =>
                field.Kind == ProcessInformationKind.StepOutput
                && field.Source != RequestFieldSource.Manual))
        {
            throw new InvalidOperationException(
                "Step output fields cannot use a current-user source.");
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
