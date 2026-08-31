namespace Intably.Application.Users;

public interface ICurrentUserService
{
    Task<CurrentUserProfile?> GetAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken);
}
