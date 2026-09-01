using Intably.Application.Templates;
using Intably.Domain.Templates;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Templates;

internal sealed partial class TemplateService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : ITemplateService
{
    private Task<ProcessTemplate?> LoadTemplateAsync(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        return dbContext.ProcessTemplates
            .AsSplitQuery()
            .Include(template => template.Versions)
                .ThenInclude(version => version.RequestFields)
                    .ThenInclude(field => field.Options)
            .Include(template => template.Versions)
                .ThenInclude(version => version.Steps)
            .SingleOrDefaultAsync(
                template => template.Id == ptrg,
                cancellationToken);
    }

    private DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    private static TemplateVersion GetCurrentVersion(ProcessTemplate template)
    {
        return template.Versions.SingleOrDefault(version => !version.IsPublished)
            ?? template.Versions.Single(version =>
                version.Version == template.PublishedVersion);
    }

    private static void PopulateVersion(
        TemplateVersion version,
        SaveTemplateRequest request)
    {
        foreach (var (field, order) in request.RequestFields.Select(
                     (field, index) => (field, index)))
        {
            version.AddRequestField(
                order,
                field.Label,
                ParseFieldType(field.Type),
                field.Required,
                field.Placeholder,
                ParseFieldSource(field.Source),
                field.Options);
        }

        foreach (var (step, order) in request.Steps.Select(
                     (step, index) => (step, index + 1)))
        {
            version.AddStep(
                order,
                step.Title,
                step.RequiredRoleFrrg,
                step.RequiredRoleFrrg.HasValue
                    ? step.RequiredRole
                    : "Any active user",
                step.Instructions,
                step.SupportingUrl,
                step.DefaultAssigneeGrg,
                step.DefaultAssignee,
                step.DueOffsetDays,
                step.NoteRequired);
        }
    }

    private static RequestFieldType ParseFieldType(string value)
    {
        return Enum.TryParse<RequestFieldType>(value, true, out var type)
            ? type
            : throw new ArgumentException($"Unknown request field type '{value}'.");
    }

    private static RequestFieldSource ParseFieldSource(string value)
    {
        return Enum.TryParse<RequestFieldSource>(value, true, out var source)
            ? source
            : throw new ArgumentException(
                $"Unknown request field source '{value}'.");
    }
}
