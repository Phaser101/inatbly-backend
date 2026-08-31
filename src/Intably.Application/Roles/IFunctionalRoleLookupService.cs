namespace Intably.Application.Roles;

public interface IFunctionalRoleLookupService
{
    Task<IReadOnlyCollection<FunctionalRoleLookup>> GetAllAsync(
        CancellationToken cancellationToken);
}
