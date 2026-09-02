using Intably.Domain.Common;

namespace Intably.Domain.Processes;

public sealed class ProcessStepGroup : Entity
{
    private readonly List<ProcessStepGroup> _prerequisiteGroups = [];

    private ProcessStepGroup()
    {
    }

    internal ProcessStepGroup(
        Guid processId,
        Guid sourceTemplateStepGroupId,
        string name,
        string description,
        int order,
        StepGroupExecutionMode executionMode)
    {
        ProcessId = processId;
        SourceTemplateStepGroupId = sourceTemplateStepGroupId;
        Name = name;
        Description = description;
        Order = order;
        ExecutionMode = executionMode;
    }

    public Guid ProcessId { get; private set; }

    public Guid SourceTemplateStepGroupId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public StepGroupExecutionMode ExecutionMode { get; private set; }

    public IReadOnlyCollection<ProcessStepGroup> PrerequisiteGroups =>
        _prerequisiteGroups;

    internal void AddPrerequisite(ProcessStepGroup group)
    {
        _prerequisiteGroups.Add(group);
    }
}
