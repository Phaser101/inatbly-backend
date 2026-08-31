namespace Intably.Application.Users;

public interface IUserLookupService
{
    Task<IReadOnlyCollection<UserLookup>> GetAllAsync(
        CancellationToken cancellationToken);
}
