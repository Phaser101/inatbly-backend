using System.Data;
using Intably.Application.Administration;
using Intably.Application.Permissions;
using Intably.Domain.Permissions;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Permissions;

internal sealed class PermissionGrantService(
    IntablyDbContext dbContext,
    TimeProvider timeProvider) : IPermissionGrantService
{
    public async Task<IReadOnlyCollection<PermissionGrantDetails>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var grants = await (
            from grant in dbContext.PermissionGrants.AsNoTracking()
            join target in dbContext.Users.AsNoTracking()
                on grant.UserId equals target.Id
            join actor in dbContext.Users.AsNoTracking()
                on grant.GrantedByUserId equals actor.Id
            orderby target.DisplayName, grant.Permission
            select new
            {
                Pgrg = grant.Id,
                Grg = target.Id,
                UserName = target.DisplayName,
                grant.Permission,
                GrantingActorGrg = actor.Id,
                GrantingActorName = actor.DisplayName,
                grant.GrantedAtUtc,
            }
        ).ToArrayAsync(cancellationToken);

        return grants
            .Select(grant => new PermissionGrantDetails(
                grant.Pgrg,
                grant.Grg,
                grant.UserName,
                grant.Permission.ToContractName(),
                grant.GrantingActorGrg,
                grant.GrantingActorName,
                grant.GrantedAtUtc))
            .ToArray();
    }

    public async Task<PermissionGrantDetails> GrantAsync(
        GrantPermissionRequest request,
        Guid grantingActorGrg,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!PermissionContracts.TryParse(request.Permission, out var permission))
        {
            throw new AdministrationValidationException(
                $"'{request.Permission}' is not a supported permission.");
        }

        var target = await dbContext.Users.FindAsync(
            [request.Grg],
            cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"User '{request.Grg}' was not found.");
        var actor = await dbContext.Users.FindAsync(
            [grantingActorGrg],
            cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"Granting user '{grantingActorGrg}' was not found.");

        if (!target.IsActive)
        {
            throw new AdministrationConflictException(
                "Permissions cannot be granted to an inactive user.");
        }

        if (await dbContext.PermissionGrants.AnyAsync(
            grant =>
                grant.UserId == request.Grg
                && grant.Permission == permission,
            cancellationToken))
        {
            throw new AdministrationConflictException(
                $"The user already has {permission.ToContractName()}.");
        }

        var grant = new PermissionGrant(
            target.Id,
            permission,
            actor.Id,
            timeProvider.GetUtcNow());
        dbContext.PermissionGrants.Add(grant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PermissionGrantDetails(
            grant.Id,
            target.Id,
            target.DisplayName,
            permission.ToContractName(),
            actor.Id,
            actor.DisplayName,
            grant.GrantedAtUtc);
    }

    public async Task RevokeAsync(
        Guid pgrg,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var grant = await dbContext.PermissionGrants.FindAsync(
            [pgrg],
            cancellationToken)
            ?? throw new AdministrationNotFoundException(
                $"Permission grant '{pgrg}' was not found.");

        if (grant.Permission == ApplicationPermission.ManagePermissions)
        {
            var targetIsActive = await dbContext.Users
                .Where(user => user.Id == grant.UserId)
                .Select(user => user.IsActive)
                .SingleAsync(cancellationToken);
            if (targetIsActive)
            {
                var anotherExists = await (
                    from otherGrant in dbContext.PermissionGrants
                    join user in dbContext.Users
                        on otherGrant.UserId equals user.Id
                    where
                        otherGrant.Id != pgrg
                        && otherGrant.Permission
                            == ApplicationPermission.ManagePermissions
                        && user.IsActive
                    select otherGrant.Id
                ).AnyAsync(cancellationToken);

                if (!anotherExists)
                {
                    throw new AdministrationConflictException(
                        "The final active MANAGE_PERMISSIONS grant cannot be revoked.");
                }
            }
        }

        dbContext.PermissionGrants.Remove(grant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
