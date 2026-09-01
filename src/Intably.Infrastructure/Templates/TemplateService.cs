using Intably.Application.Templates;
using Intably.Domain.Templates;
using Intably.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Templates;

internal sealed partial class TemplateService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : ITemplateService
{
    private const string TemplateNameIndex = "UX_ProcessTemplates_Name";

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

    private async Task EnsureNameAvailableAsync(
        string name,
        Guid? excludedPtrg,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        var exists = await dbContext.ProcessTemplates.AnyAsync(
            template =>
                template.Id != excludedPtrg
                && template.Name == normalizedName,
            cancellationToken);
        if (exists)
        {
            throw new TemplateNameConflictException(normalizedName);
        }
    }

    private async Task SaveNameChangeAsync(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException sqlException
                && sqlException.Errors.Cast<SqlError>().Any(
                    error =>
                        error.Number is 2601 or 2627
                        && error.Message.Contains(
                            TemplateNameIndex,
                            StringComparison.Ordinal)))
        {
            throw new TemplateNameConflictException(name);
        }
    }

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
