using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class TemplateRequestField : Entity
{
    private readonly List<TemplateRequestFieldOption> _options = [];

    private TemplateRequestField()
    {
    }

    private TemplateRequestField(
        Guid templateVersionId,
        int order,
        string label,
        RequestFieldType type,
        bool isRequired,
        string placeholder,
        RequestFieldSource source,
        ProcessInformationKind kind,
        bool pinned,
        Guid? producingTemplateStepId)
    {
        TemplateVersionId = templateVersionId;
        Order = order;
        Label = label;
        Type = type;
        IsRequired = isRequired;
        Placeholder = placeholder;
        Source = source;
        Kind = kind;
        Pinned = pinned;
        ProducingTemplateStepId = producingTemplateStepId;
    }

    public Guid TemplateVersionId { get; private set; }

    public int Order { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public RequestFieldType Type { get; private set; }

    public bool IsRequired { get; private set; }

    public string Placeholder { get; private set; } = string.Empty;

    public RequestFieldSource Source { get; private set; }

    public ProcessInformationKind Kind { get; private set; }

    public bool Pinned { get; private set; }

    public Guid? ProducingTemplateStepId { get; private set; }

    public IReadOnlyCollection<TemplateRequestFieldOption> Options => _options;

    internal static TemplateRequestField Create(
        Guid templateVersionId,
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
        var field = new TemplateRequestField(
            templateVersionId,
            order,
            label.Trim(),
            type,
            isRequired,
            placeholder.Trim(),
            source,
            kind,
            pinned,
            producingTemplateStepId);

        foreach (var (value, optionOrder) in options.Select((value, index) => (value, index)))
        {
            field._options.Add(
                TemplateRequestFieldOption.Create(field.Id, optionOrder, value));
        }

        return field;
    }
}
