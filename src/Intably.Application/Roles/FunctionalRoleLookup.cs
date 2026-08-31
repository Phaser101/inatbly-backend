namespace Intably.Application.Roles;

public sealed record FunctionalRoleLookup(
    Guid Frrg,
    string Name,
    string Description,
    string Status,
    DateTimeOffset CreatedAtUtc);
