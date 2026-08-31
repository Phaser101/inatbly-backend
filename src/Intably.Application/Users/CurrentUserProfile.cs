namespace Intably.Application.Users;

public sealed record CurrentUserProfile(
    Guid Grg,
    string DisplayName,
    string Email,
    bool Active,
    IReadOnlyCollection<CurrentUserRole> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record CurrentUserRole(Guid Frrg, string Name);
