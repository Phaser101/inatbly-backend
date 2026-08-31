namespace Intably.Application.Users;

public sealed record ExternalUserIdentity(
    string TenantId,
    string ObjectId,
    string DisplayName,
    string Email);
