using Intably.Domain.Common;

namespace Intably.Domain.Users;

public sealed class User : Entity
{
    private User()
    {
    }

    private User(
        string entraTenantId,
        string entraObjectId,
        string displayName,
        string email,
        DateTimeOffset createdAtUtc)
    {
        EntraTenantId = entraTenantId;
        EntraObjectId = entraObjectId;
        DisplayName = displayName;
        Email = email;
        CreatedAtUtc = createdAtUtc;
    }

    public string EntraTenantId { get; private set; } = string.Empty;

    public string EntraObjectId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static User Create(
        string entraTenantId,
        string entraObjectId,
        string displayName,
        string email,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entraTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entraObjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new User(
            entraTenantId.Trim(),
            entraObjectId.Trim(),
            displayName.Trim(),
            email.Trim().ToLowerInvariant(),
            createdAtUtc);
    }

    public void Activate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("The user is already active.");
        }

        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("The user is already inactive.");
        }

        IsActive = false;
    }
}
