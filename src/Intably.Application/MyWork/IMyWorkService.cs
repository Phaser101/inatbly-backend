using Intably.Application.Users;

namespace Intably.Application.MyWork;

public interface IMyWorkService
{
    Task<IReadOnlyCollection<MyWorkItem>> GetAsync(
        CurrentUserProfile currentUser,
        CancellationToken cancellationToken);
}
