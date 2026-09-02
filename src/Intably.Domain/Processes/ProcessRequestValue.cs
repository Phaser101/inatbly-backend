using Intably.Domain.Common;
using System.Text.Json;

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
        int order,
        ProcessInformationKind kind,
        bool pinned,
        Guid? producingProcessStepId,
        IEnumerable<string> options,
        Guid? modifiedByUserId,
        string? modifiedByDisplayName,
        DateTimeOffset? modifiedAtUtc)
    {
        ProcessId = processId;
        SourceRequestFieldId = sourceRequestFieldId;
        Label = label;
        FieldType = fieldType;
        IsRequired = isRequired;
        Value = value;
        Order = order;
        Kind = kind;
        Pinned = pinned;
        ProducingProcessStepId = producingProcessStepId;
        OptionsJson = JsonSerializer.Serialize(options);
        ModifiedByUserId = modifiedByUserId;
        ModifiedByDisplayName = modifiedByDisplayName;
        ModifiedAtUtc = modifiedAtUtc;
    }

    public Guid ProcessId { get; private set; }

    public Guid SourceRequestFieldId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string FieldType { get; private set; } = string.Empty;

    public bool IsRequired { get; private set; }

    public string Value { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public ProcessInformationKind Kind { get; private set; }

    public bool Pinned { get; private set; }

    public Guid? ProducingProcessStepId { get; private set; }

    public string OptionsJson { get; private set; } = "[]";

    public IReadOnlyCollection<string> Options =>
        JsonSerializer.Deserialize<string[]>(OptionsJson) ?? [];

    public Guid? ModifiedByUserId { get; private set; }

    public string? ModifiedByDisplayName { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal void Update(
        string value,
        Guid actorUserId,
        string actorDisplayName,
        DateTimeOffset modifiedAtUtc)
    {
        Value = value;
        ModifiedByUserId = actorUserId;
        ModifiedByDisplayName = actorDisplayName.Trim();
        ModifiedAtUtc = modifiedAtUtc;
    }
}
