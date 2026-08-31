using Intably.Application.Users;
using Intably.Domain.Permissions;
using Intably.Domain.Users;
using Intably.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Intably.Infrastructure.Users;

internal sealed class UserProvisioningService(
    IntablyDbContext dbContext,
    IConfiguration configuration,
    FirstAdminOptions firstAdminOptions,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider)
{
    private const string DevelopmentHeadersMode = "DevelopmentHeaders";
    private const string TrustedGatewayMode = "TrustedGateway";
    private const string UserIdentityIndex =
        "IX_Users_EntraTenantId_EntraObjectId";
    private const string PermissionGrantIndex =
        "IX_PermissionGrants_UserId_Permission";

    public async Task<User?> FindOrProvisionAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(identity, cancellationToken);
        if (user is not null || !CanAutoProvision())
        {
            return user;
        }

        user = User.Create(
            identity.TenantId,
            identity.ObjectId,
            identity.DisplayName,
            identity.Email,
            timeProvider.GetUtcNow());

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException exception)
            when (IsUniqueIndexViolation(exception, UserIdentityIndex))
        {
            dbContext.Entry(user).State = EntityState.Detached;
            var winner = await FindAsync(identity, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }
    }

    public async Task BootstrapFirstAdminAsync(
        User user,
        ExternalUserIdentity identity,
        CancellationToken cancellationToken)
    {
        if (!IsConfiguredFirstAdmin(user, identity))
        {
            return;
        }

        var alreadyGranted = await dbContext.PermissionGrants.AnyAsync(
            grant =>
                grant.UserId == user.Id
                && grant.Permission == ApplicationPermission.ManagePermissions,
            cancellationToken);

        if (alreadyGranted)
        {
            return;
        }

        var grant = new PermissionGrant(
            user.Id,
            ApplicationPermission.ManagePermissions,
            user.Id,
            timeProvider.GetUtcNow());
        dbContext.PermissionGrants.Add(grant);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueIndexViolation(exception, PermissionGrantIndex))
        {
            dbContext.Entry(grant).State = EntityState.Detached;
            var winnerExists = await dbContext.PermissionGrants.AnyAsync(
                candidate =>
                    candidate.UserId == user.Id
                    && candidate.Permission
                        == ApplicationPermission.ManagePermissions,
                cancellationToken);

            if (!winnerExists)
            {
                throw;
            }
        }
    }

    private Task<User?> FindAsync(
        ExternalUserIdentity identity,
        CancellationToken cancellationToken)
    {
        return dbContext.Users.SingleOrDefaultAsync(
            candidate =>
                candidate.EntraTenantId == identity.TenantId
                && candidate.EntraObjectId == identity.ObjectId,
            cancellationToken);
    }

    private bool CanAutoProvision()
    {
        var trustMode = configuration["BackendTrust:Mode"]?.Trim();
        if (string.Equals(
            trustMode,
            TrustedGatewayMode,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
                trustMode,
                DevelopmentHeadersMode,
                StringComparison.OrdinalIgnoreCase)
            && hostEnvironment.IsDevelopment()
            && bool.TryParse(
                configuration[
                    "UserProvisioning:AutoProvisionAuthenticatedUsers"],
                out var enabled)
            && enabled;
    }

    private bool IsConfiguredFirstAdmin(
        User user,
        ExternalUserIdentity identity)
    {
        return user.IsActive
            && !string.IsNullOrWhiteSpace(firstAdminOptions.EntraTenantId)
            && !string.IsNullOrWhiteSpace(firstAdminOptions.EntraObjectId)
            && string.Equals(
                firstAdminOptions.EntraTenantId.Trim(),
                identity.TenantId,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                firstAdminOptions.EntraObjectId.Trim(),
                identity.ObjectId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueIndexViolation(
        DbUpdateException exception,
        string indexName)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(
                error =>
                    error.Number is 2601 or 2627
                    && error.Message.Contains(
                        indexName,
                        StringComparison.Ordinal));
    }
}
