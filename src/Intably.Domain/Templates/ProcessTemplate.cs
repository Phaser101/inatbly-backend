using Intably.Domain.Common;

namespace Intably.Domain.Templates;

public sealed class ProcessTemplate : Entity
{
    private readonly List<TemplateVersion> _versions = [];

    private ProcessTemplate()
    {
    }

    private ProcessTemplate(
        string name,
        string description,
        Guid ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        Name = name;
        Description = description;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public TemplateStatus Status { get; private set; } = TemplateStatus.Draft;

    public int PublishedVersion { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<TemplateVersion> Versions => _versions;

    public static ProcessTemplate Create(
        string name,
        string description,
        Guid ownerUserId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ProcessTemplate(
            name.Trim(),
            description.Trim(),
            ownerUserId,
            createdAtUtc);
    }

    public TemplateVersion SaveDraft(
        string name,
        string description,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var existingDraft = _versions.SingleOrDefault(version => !version.IsPublished);
        if (existingDraft is not null)
        {
            _versions.Remove(existingDraft);
        }

        Name = name.Trim();
        Description = description.Trim();
        UpdatedAtUtc = updatedAtUtc;

        var draft = TemplateVersion.Create(
            Id,
            PublishedVersion + 1,
            Name,
            Description,
            updatedAtUtc);
        _versions.Add(draft);
        return draft;
    }

    public void Publish(DateTimeOffset publishedAtUtc)
    {
        var draft = _versions.SingleOrDefault(version => !version.IsPublished)
            ?? throw new InvalidOperationException(
                "The template does not have a draft to publish.");

        draft.Publish(publishedAtUtc);
        PublishedVersion = draft.Version;
        Status = TemplateStatus.Active;
        Name = draft.Name;
        Description = draft.Description;
        UpdatedAtUtc = publishedAtUtc;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        Status = TemplateStatus.Archived;
        UpdatedAtUtc = archivedAtUtc;
    }
}
