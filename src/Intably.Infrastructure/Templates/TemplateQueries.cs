using Intably.Application.Templates;
using Intably.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Templates;

internal sealed partial class TemplateService
{
    public async Task<IReadOnlyCollection<TemplateSummary>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var templates = await dbContext.ProcessTemplates
            .AsNoTracking()
            .Where(template => template.Status != TemplateStatus.Archived)
            .Include(template => template.Versions)
                .ThenInclude(version => version.Steps)
            .OrderByDescending(template => template.UpdatedAtUtc)
            .ToArrayAsync(cancellationToken);
        var ownerIds = templates
            .Select(template => template.OwnerUserId)
            .Distinct()
            .ToArray();
        var owners = await dbContext.Users
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.DisplayName,
                cancellationToken);

        return templates.Select(template =>
        {
            var version = GetCurrentVersion(template);
            return new TemplateSummary(
                template.Id,
                template.Name,
                template.Description,
                template.PublishedVersion == 0 ? 1 : template.PublishedVersion,
                template.Status.ToString(),
                template.Versions.Any(candidate => !candidate.IsPublished),
                version.Steps.Count,
                template.OwnerUserId,
                owners[template.OwnerUserId],
                template.UpdatedAtUtc);
        }).ToArray();
    }

    public async Task<TemplateDetails?> GetAsync(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(ptrg, cancellationToken);
        return template is null
            ? null
            : await ToDetailsAsync(template, cancellationToken);
    }

    public async Task<TemplateDetails?> GetPublishedAsync(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(ptrg, cancellationToken);
        var version = template?.Versions.SingleOrDefault(candidate =>
            candidate.Version == template.PublishedVersion);
        return template is null || version is null
            ? null
            : await ToDetailsAsync(template, version, cancellationToken);
    }

    private async Task<TemplateDetails> ToDetailsAsync(
        ProcessTemplate template,
        CancellationToken cancellationToken)
    {
        return await ToDetailsAsync(
            template,
            GetCurrentVersion(template),
            cancellationToken);
    }

    private async Task<TemplateDetails> ToDetailsAsync(
        ProcessTemplate template,
        TemplateVersion version,
        CancellationToken cancellationToken)
    {
        var userIds = version.Steps
            .Where(step => step.DefaultAssigneeUserId.HasValue)
            .Select(step => step.DefaultAssigneeUserId!.Value)
            .Append(template.OwnerUserId)
            .Distinct()
            .ToArray();
        var roleIds = version.Steps
            .Where(step => step.RequiredRoleId.HasValue)
            .Select(step => step.RequiredRoleId!.Value)
            .Distinct()
            .ToArray();
        var users = await dbContext.Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.DisplayName,
                cancellationToken);
        var roles = await dbContext.FunctionalRoles
            .Where(role => roleIds.Contains(role.Id))
            .ToDictionaryAsync(
                role => role.Id,
                role => role.Name,
                cancellationToken);

        return MapDetails(template, version, users, roles);
    }
}
