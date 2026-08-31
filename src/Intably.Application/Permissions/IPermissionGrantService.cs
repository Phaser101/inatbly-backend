namespace Intably.Application.Permissions;

public interface IPermissionGrantService
{
    Task<IReadOnlyCollection<PermissionGrantDetails>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<PermissionGrantDetails> GrantAsync(
        GrantPermissionRequest request,
        Guid grantingActorGrg,
        CancellationToken cancellationToken);

    Task RevokeAsync(Guid pgrg, CancellationToken cancellationToken);
}

public sealed record GrantPermissionRequest(Guid Grg, string Permission);

public sealed record PermissionGrantDetails(
    Guid Pgrg,
    Guid Grg,
    string UserName,
    string Permission,
    Guid GrantingActorGrg,
    string GrantingActorName,
    DateTimeOffset GrantedAtUtc);
