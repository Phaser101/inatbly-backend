using Intably.Domain.Common;

namespace Intably.Domain.Processes;

public sealed class ProcessRequestValue : Entity
{
    private ProcessRequestValue()
    {
    }

    internal ProcessRequestValue(
        Guid processId,
        Guid sourceRequestFieldId,
        string label,
        string fieldType,
        bool isRequired,
        string value,
        int order)
    {
        ProcessId = processId;
        SourceRequestFieldId = sourceRequestFieldId;
        Label = label;
        FieldType = fieldType;
        IsRequired = isRequired;
        Value = value;
        Order = order;
    }

    public Guid ProcessId { get; private set; }

    public Guid SourceRequestFieldId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string FieldType { get; private set; } = string.Empty;

    public bool IsRequired { get; private set; }

    public string Value { get; private set; } = string.Empty;

    public int Order { get; private set; }
}
