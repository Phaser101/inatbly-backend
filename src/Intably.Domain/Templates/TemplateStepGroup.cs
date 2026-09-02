using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class TemplateStepGroup : Entity
{
    private readonly List<TemplateStepGroup> _prerequisiteGroups = [];

    private TemplateStepGroup()
    {
    }

    internal TemplateStepGroup(
        Guid id,
        Guid templateVersionId,
        string name,
        string description,
        int order,
        StepGroupExecutionMode executionMode)
    {
        Id = id;
        TemplateVersionId = templateVersionId;
        Name = name.Trim();
        Description = description.Trim();
        Order = order;
        ExecutionMode = executionMode;
    }

    public Guid TemplateVersionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public StepGroupExecutionMode ExecutionMode { get; private set; }

    public IReadOnlyCollection<TemplateStepGroup> PrerequisiteGroups =>
        _prerequisiteGroups;

    internal void AddPrerequisite(TemplateStepGroup group)
    {
        _prerequisiteGroups.Add(group);
    }
}
