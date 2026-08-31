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
        Guid? sourceFieldSetId,
        string? sourceFieldSetName,
        int? sourceFieldSetVersion)
    {
        TemplateVersionId = templateVersionId;
        Order = order;
        Label = label;
        Type = type;
        IsRequired = isRequired;
        Placeholder = placeholder;
        Source = source;
        SourceFieldSetId = sourceFieldSetId;
        SourceFieldSetName = sourceFieldSetName;
        SourceFieldSetVersion = sourceFieldSetVersion;
    }

    public Guid TemplateVersionId { get; private set; }

    public int Order { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public RequestFieldType Type { get; private set; }

    public bool IsRequired { get; private set; }

    public string Placeholder { get; private set; } = string.Empty;

    public RequestFieldSource Source { get; private set; }

    public Guid? SourceFieldSetId { get; private set; }

    public string? SourceFieldSetName { get; private set; }

    public int? SourceFieldSetVersion { get; private set; }

    public IReadOnlyCollection<TemplateRequestFieldOption> Options => _options;

    internal static TemplateRequestField Create(
        Guid templateVersionId,
        int order,
        string label,
        RequestFieldType type,
        bool isRequired,
        string placeholder,
        RequestFieldSource source,
        Guid? sourceFieldSetId,
        string? sourceFieldSetName,
        int? sourceFieldSetVersion,
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
            sourceFieldSetId,
            sourceFieldSetName?.Trim(),
            sourceFieldSetVersion);

        foreach (var (value, optionOrder) in options.Select((value, index) => (value, index)))
        {
            field._options.Add(
                TemplateRequestFieldOption.Create(field.Id, optionOrder, value));
        }

        return field;
    }
}
