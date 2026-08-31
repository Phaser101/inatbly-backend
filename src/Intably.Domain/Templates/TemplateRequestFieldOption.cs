using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class TemplateRequestFieldOption : Entity
{
    private TemplateRequestFieldOption()
    {
    }

    private TemplateRequestFieldOption(
        Guid requestFieldId,
        int order,
        string value)
    {
        RequestFieldId = requestFieldId;
        Order = order;
        Value = value;
    }

    public Guid RequestFieldId { get; private set; }

    public int Order { get; private set; }

    public string Value { get; private set; } = string.Empty;

    internal static TemplateRequestFieldOption Create(
        Guid requestFieldId,
        int order,
        string value)
    {
        return new TemplateRequestFieldOption(
            requestFieldId,
            order,
            value.Trim());
    }
}
