namespace Intably.Application.Users;

public interface IUserAdministrationService
{
    Task<UserLookup> ReplaceFunctionalRolesAsync(
        Guid grg,
        ReplaceUserFunctionalRolesRequest request,
        CancellationToken cancellationToken);

    Task<UserLookup> SetActiveAsync(
        Guid grg,
        SetUserActiveRequest request,
        CancellationToken cancellationToken);
}

public sealed record ReplaceUserFunctionalRolesRequest(
    IReadOnlyCollection<Guid> FunctionalRoleIds);

public sealed record SetUserActiveRequest(bool Active);
