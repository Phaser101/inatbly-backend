using Intably.Application.Permissions;
using Intably.Application.Users;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Users;

internal sealed class CurrentUserService(
    IntablyDbContext dbContext,
    UserProvisioningService provisioningService) : ICurrentUserService
{
    public async Task<CurrentUserProfile?> GetAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken)
    {
        var user = await provisioningService.FindOrProvisionAsync(
            identity,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        await provisioningService.BootstrapFirstAdminAsync(
            user,
            identity,
            cancellationToken);

        var roles = await (
            from membership in dbContext.UserFunctionalRoles
            join role in dbContext.FunctionalRoles
                on membership.FunctionalRoleId equals role.Id
            where membership.UserId == user.Id && !role.IsArchived
            orderby role.Name
            select new CurrentUserRole(role.Id, role.Name)
        ).ToArrayAsync(cancellationToken);

        var permissions = await dbContext.PermissionGrants
            .Where(grant => grant.UserId == user.Id)
            .Select(grant => grant.Permission)
            .ToArrayAsync(cancellationToken);

        return new CurrentUserProfile(
            user.Id,
            user.DisplayName,
            user.Email,
            user.IsActive,
            roles,
            permissions
                .GetEffectivePermissions()
                .Select(permission => permission.ToContractName())
                .ToArray());
    }
}
