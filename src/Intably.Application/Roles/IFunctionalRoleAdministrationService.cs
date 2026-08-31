namespace Intably.Application.Roles;

public interface IFunctionalRoleAdministrationService
{
    Task<FunctionalRoleLookup> CreateAsync(
        SaveFunctionalRoleRequest request,
        CancellationToken cancellationToken);

    Task<FunctionalRoleLookup> UpdateAsync(
        Guid frrg,
        SaveFunctionalRoleRequest request,
        CancellationToken cancellationToken);

    Task ArchiveAsync(Guid frrg, CancellationToken cancellationToken);
}

public sealed record SaveFunctionalRoleRequest(string Name, string Description);
