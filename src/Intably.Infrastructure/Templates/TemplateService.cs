using Intably.Application.Templates;
using Intably.Domain.Common;
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
            .Include(template => template.Versions)
                .ThenInclude(version => version.StepGroups)
                    .ThenInclude(group => group.PrerequisiteGroups)
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
        ValidateGroups(request);
        ValidateInformationFields(request);

        var persistedGroupIds = request.Groups.ToDictionary(
            group => group.Ptsgrg,
            _ => Guid.NewGuid());
        foreach (var group in request.Groups.OrderBy(group => group.Order))
        {
            version.AddStepGroup(
                persistedGroupIds[group.Ptsgrg],
                group.Name,
                group.Description,
                group.Order,
                ParseExecutionMode(group.ExecutionMode));
        }

        foreach (var group in request.Groups)
        {
            foreach (var prerequisiteId in group.PrerequisitePtsgrgs)
            {
                version.AddStepGroupPrerequisite(
                    persistedGroupIds[group.Ptsgrg],
                    persistedGroupIds[prerequisiteId]);
            }
        }

        var persistedStepIds = request.Steps.ToDictionary(
            step => step.Ptsrg,
            _ => Guid.NewGuid());
        foreach (var step in request.Steps
                     .OrderBy(step => step.Ptsgrg)
                     .ThenBy(step => step.Order))
        {
            version.AddStep(
                persistedStepIds[step.Ptsrg],
                persistedGroupIds[step.Ptsgrg],
                step.Order,
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

        foreach (var (field, order) in request.InformationFields.Select(
                     (field, index) => (field, index)))
        {
            var kind = ParseInformationKind(field.Kind);
            version.AddRequestField(
                order,
                field.Label,
                ParseFieldType(field.Type),
                field.Required,
                field.Placeholder,
                kind,
                field.Pinned,
                field.ProducingPtsrg.HasValue
                    ? persistedStepIds[field.ProducingPtsrg.Value]
                    : null,
                field.Options);
        }
    }

    private static void ValidateGroups(SaveTemplateRequest request)
    {
        if (request.Groups.Select(group => group.Ptsgrg).Distinct().Count()
            != request.Groups.Count)
        {
            throw new ArgumentException("Step group rowguids must be unique.");
        }

        if (request.Groups.Any(group => group.Ptsgrg == Guid.Empty))
        {
            throw new ArgumentException(
                "Step group rowguids cannot be empty.");
        }

        if (request.Groups.Select(group => group.Order).Distinct().Count()
            != request.Groups.Count)
        {
            throw new ArgumentException("Step group orders must be unique.");
        }

        if (request.Groups.Any(group =>
                group.PrerequisitePtsgrgs.Distinct().Count()
                != group.PrerequisitePtsgrgs.Count))
        {
            throw new ArgumentException(
                "A prerequisite group can be referenced only once.");
        }

        var groupIds = request.Groups.Select(group => group.Ptsgrg).ToHashSet();
        if (request.Groups.SelectMany(group => group.PrerequisitePtsgrgs)
            .Any(id => !groupIds.Contains(id)))
        {
            throw new ArgumentException(
                "Every prerequisite must reference a step group in this template.");
        }

        if (request.Steps.Any(step => !groupIds.Contains(step.Ptsgrg)))
        {
            throw new ArgumentException(
                "Every step must belong to a step group in this template.");
        }

        if (request.Steps
            .GroupBy(step => step.Ptsgrg)
            .Any(group => group.Select(step => step.Order).Distinct().Count()
                != group.Count()))
        {
            throw new ArgumentException(
                "Step orders must be unique within each step group.");
        }

        EnsureAcyclic(request.Groups);
    }

    private static void ValidateInformationFields(SaveTemplateRequest request)
    {
        if (request.Steps.Select(step => step.Ptsrg).Distinct().Count()
            != request.Steps.Count)
        {
            throw new ArgumentException("Template step rowguids must be unique.");
        }

        if (request.Steps.Any(step => step.Ptsrg == Guid.Empty))
        {
            throw new ArgumentException(
                "Template step rowguids cannot be empty.");
        }

        var stepIds = request.Steps.Select(step => step.Ptsrg).ToHashSet();
        foreach (var field in request.InformationFields)
        {
            ParseInformationKind(field.Kind);
            ParseFieldType(field.Type);
            if (field.ProducingPtsrg.HasValue
                && !stepIds.Contains(field.ProducingPtsrg.Value))
            {
                throw new ArgumentException(
                    "Producing steps must reference a step in this template.");
            }
        }
    }

    private static void EnsureAcyclic(
        IReadOnlyCollection<SaveTemplateStepGroup> groups)
    {
        var prerequisites = groups.ToDictionary(
            group => group.Ptsgrg,
            group => group.PrerequisitePtsgrgs);
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();

        bool Visit(Guid id)
        {
            if (!visiting.Add(id))
            {
                return false;
            }

            if (visited.Contains(id))
            {
                visiting.Remove(id);
                return true;
            }

            if (prerequisites[id].Any(prerequisite => !Visit(prerequisite)))
            {
                return false;
            }

            visiting.Remove(id);
            visited.Add(id);
            return true;
        }

        if (groups.Any(group => !Visit(group.Ptsgrg)))
        {
            throw new ArgumentException(
                "Step group prerequisites cannot contain a cycle.");
        }
    }

    private static StepGroupExecutionMode ParseExecutionMode(string value)
    {
        return Enum.TryParse<StepGroupExecutionMode>(value, true, out var mode)
            && Enum.IsDefined(mode)
            ? mode
            : throw new ArgumentException(
                $"Unknown step group execution mode '{value}'.");
    }

    private static ProcessInformationKind ParseInformationKind(string value)
    {
        return Enum.TryParse<ProcessInformationKind>(value, true, out var kind)
            && Enum.IsDefined(kind)
            ? kind
            : throw new ArgumentException(
                $"Unknown process information kind '{value}'.");
    }

    private static RequestFieldType ParseFieldType(string value)
    {
        return Enum.TryParse<RequestFieldType>(value, true, out var type)
            && Enum.IsDefined(type)
            ? type
            : throw new ArgumentException($"Unknown request field type '{value}'.");
    }
}
