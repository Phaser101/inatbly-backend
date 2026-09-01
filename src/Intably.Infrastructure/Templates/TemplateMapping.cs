using Intably.Application.Templates;
using Intably.Domain.Templates;

namespace Intably.Infrastructure.Templates;

internal sealed partial class TemplateService
{
    private static TemplateDetails MapDetails(
        ProcessTemplate template,
        TemplateVersion version,
        IReadOnlyDictionary<Guid, string> users,
        IReadOnlyDictionary<Guid, string> roles)
    {
        return new TemplateDetails(
            template.Id,
            version.Name,
            version.Description,
            version.Version,
            template.Status.ToString(),
            template.PublishedVersion > 0,
            template.OwnerUserId,
            users[template.OwnerUserId],
            template.UpdatedAtUtc,
            version.RequestFields
                .OrderBy(field => field.Order)
                .Select(MapRequestField)
                .ToArray(),
            version.Steps
                .OrderBy(step => step.Order)
                .Select(step => MapStep(step, users, roles))
                .ToArray());
    }

    private static TemplateRequestFieldDetails MapRequestField(
        TemplateRequestField field)
    {
        return new TemplateRequestFieldDetails(
            field.Id,
            field.Label,
            field.Type.ToString().ToLowerInvariant(),
            field.IsRequired,
            field.Placeholder,
            ToCamelCase(field.Source.ToString()),
            field.Options
                .OrderBy(option => option.Order)
                .Select(option => option.Value)
                .ToArray());
    }

    private static TemplateStepDetails MapStep(
        TemplateStep step,
        IReadOnlyDictionary<Guid, string> users,
        IReadOnlyDictionary<Guid, string> roles)
    {
        return new TemplateStepDetails(
            step.Id,
            step.Title,
            step.RequiredRoleId,
            GetName(roles, step.RequiredRoleId) ?? step.RequiredRoleName,
            step.Instructions,
            step.SupportingUrl,
            step.DefaultAssigneeUserId,
            GetName(users, step.DefaultAssigneeUserId) ?? step.DefaultAssigneeName,
            step.DueOffsetDays,
            step.NoteRequired);
    }

    private static string? GetName(
        IReadOnlyDictionary<Guid, string> names,
        Guid? id)
    {
        return id.HasValue && names.TryGetValue(id.Value, out var name)
            ? name
            : null;
    }

    private static string ToCamelCase(string value)
    {
        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
