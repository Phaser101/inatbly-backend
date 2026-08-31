namespace Intably.Application.Users;

public sealed record UserLookup(
    Guid Grg,
    string DisplayName,
    string Email,
    bool Active,
    IReadOnlyCollection<UserLookupRole> Roles);

public sealed record UserLookupRole(
    Guid Frrg,
    string Name,
    string Status);
