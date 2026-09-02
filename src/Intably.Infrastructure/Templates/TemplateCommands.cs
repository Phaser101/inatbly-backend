using Intably.Application.Templates;
using Intably.Domain.Templates;

namespace Intably.Infrastructure.Templates;

internal sealed partial class TemplateService
{
    public async Task<TemplateDetails> CreateAsync(
        SaveTemplateRequest request,
        Guid ownerGrg,
        CancellationToken cancellationToken)
    {
        await EnsureNameAvailableAsync(
            request.Name,
            excludedPtrg: null,
            cancellationToken);
        var template = ProcessTemplate.Create(
            request.Name,
            request.Description,
            ownerGrg,
            UtcNow);
        var draft = template.SaveDraft(
            request.Name,
            request.Description,
            UtcNow);
        PopulateVersion(draft, request);

        dbContext.ProcessTemplates.Add(template);
        await SaveNameChangeAsync(request.Name, cancellationToken);
        return await ToDetailsAsync(template, cancellationToken);
    }

    public async Task<TemplateDetails?> UpdateAsync(
        Guid ptrg,
        SaveTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(ptrg, cancellationToken);
        if (template is null || template.Status == TemplateStatus.Archived)
        {
            return null;
        }

        await EnsureNameAvailableAsync(request.Name, ptrg, cancellationToken);
        var existingDraft = template.Versions.SingleOrDefault(
            version => !version.IsPublished);
        var draft = template.SaveDraft(
            request.Name,
            request.Description,
            UtcNow);
        PopulateVersion(draft, request);
        dbContext.TemplateVersions.Add(draft);

        if (existingDraft is not null)
        {
            dbContext.TemplateVersions.Remove(existingDraft);
        }

        await SaveNameChangeAsync(request.Name, cancellationToken);
        return await ToDetailsAsync(template, cancellationToken);
    }

    public async Task<TemplateDetails?> PublishAsync(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(ptrg, cancellationToken);
        if (template is null || template.Status == TemplateStatus.Archived)
        {
            return null;
        }

        if (template.Versions.Any(version => !version.IsPublished))
        {
            template.Publish(UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await ToDetailsAsync(template, cancellationToken);
    }

    public async Task<TemplateDetails?> DuplicateAsync(
        Guid ptrg,
        Guid ownerGrg,
        CancellationToken cancellationToken)
    {
        var source = await LoadTemplateAsync(ptrg, cancellationToken);
        if (source is null || source.Status == TemplateStatus.Archived)
        {
            return null;
        }

        var sourceVersion = GetCurrentVersion(source);
        var request = CopyAsRequest(sourceVersion);
        var duplicateName = $"{sourceVersion.Name} (copy)";
        await EnsureNameAvailableAsync(
            duplicateName,
            excludedPtrg: null,
            cancellationToken);
        var duplicate = ProcessTemplate.Create(
            duplicateName,
            sourceVersion.Description,
            ownerGrg,
            UtcNow);
        var draft = duplicate.SaveDraft(
            duplicate.Name,
            duplicate.Description,
            UtcNow);
        PopulateVersion(draft, request with { Name = duplicate.Name });

        dbContext.ProcessTemplates.Add(duplicate);
        await SaveNameChangeAsync(duplicateName, cancellationToken);
        return await ToDetailsAsync(duplicate, cancellationToken);
    }

    public async Task<bool> ArchiveAsync(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(ptrg, cancellationToken);
        if (template is null)
        {
            return false;
        }

        template.Archive(UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SaveTemplateRequest CopyAsRequest(TemplateVersion version)
    {
        return new SaveTemplateRequest(
            version.Name,
            version.Description,
            version.RequestFields
                .OrderBy(field => field.Order)
                .Select(field => new SaveTemplateInformationField(
                    field.Label,
                    field.Type.ToString(),
                    field.IsRequired,
                    field.Placeholder,
                    field.Source.ToString(),
                    field.Kind.ToString(),
                    field.Pinned,
                    field.ProducingTemplateStepId,
                    field.Options
                        .OrderBy(option => option.Order)
                        .Select(option => option.Value)
                        .ToArray()))
                .ToArray(),
            version.StepGroups
                .OrderBy(group => group.Order)
                .Select(group => new SaveTemplateStepGroup(
                    group.Id,
                    group.Name,
                    group.Description,
                    group.Order,
                    group.ExecutionMode.ToString(),
                    group.PrerequisiteGroups
                        .Select(prerequisite => prerequisite.Id)
                        .ToArray()))
                .ToArray(),
            version.Steps
                .OrderBy(step => step.TemplateStepGroupId)
                .ThenBy(step => step.Order)
                .Select(step => new SaveTemplateStep(
                    step.Id,
                    step.TemplateStepGroupId,
                    step.Order,
                    step.Title,
                    step.RequiredRoleId,
                    step.RequiredRoleName,
                    step.Instructions,
                    step.SupportingUrl,
                    step.DefaultAssigneeUserId,
                    step.DefaultAssigneeName,
                    step.DueOffsetDays,
                    step.NoteRequired))
                .ToArray());
    }
}
