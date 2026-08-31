using Intably.Domain.Common;

namespace Intably.Domain.Roles;

public sealed class FunctionalRole : Entity
{
    private FunctionalRole()
    {
    }

    private FunctionalRole(
        string name,
        string description,
        DateTimeOffset createdAtUtc)
    {
        Name = name;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FunctionalRole Create(
        string name,
        string description,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new FunctionalRole(name.Trim(), description.Trim(), createdAtUtc);
    }

    public void Update(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (IsArchived)
        {
            throw new InvalidOperationException("Archived roles cannot be updated.");
        }

        Name = name.Trim();
        Description = description.Trim();
    }

    public void Archive()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("The role is already archived.");
        }

        IsArchived = true;
    }
}
