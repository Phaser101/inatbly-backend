using System.Net;
using System.Net.Http.Json;
using Intably.Application.Permissions;
using Intably.Application.Roles;
using Intably.Application.Users;
using Intably.Domain.Permissions;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.IntegrationTests;

public sealed class AdministrationEndpointTests
{
    [Fact]
    public async Task FunctionalRoles_SupportCreateUpdateArchiveAndValidation()
    {
        await using var factory = CreateAdminFactory();
        await factory.MigrateDatabaseAsync();
        using var admin = factory.CreateAuthenticatedClient("admin", "Ada Admin");
        await ProvisionAsync(admin);
        await GrantDirectlyAsync(
            factory,
            "admin",
            ApplicationPermission.ManageRoles);

        var createResponse = await admin.PostAsJsonAsync(
            "/api/functional-roles",
            new SaveFunctionalRoleRequest("Approver", "Approves requests."));
        var created = await createResponse.Content
            .ReadFromJsonAsync<FunctionalRoleLookup>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Active", created.Status);

        var duplicateResponse = await admin.PostAsJsonAsync(
            "/api/functional-roles",
            new SaveFunctionalRoleRequest("Approver", "Duplicate."));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var updateResponse = await admin.PutAsJsonAsync(
            $"/api/functional-roles/{created.Frrg}",
            new SaveFunctionalRoleRequest("Senior Approver", "Approves all."));
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<FunctionalRoleLookup>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Senior Approver", updated!.Name);

        var archiveResponse = await admin.DeleteAsync(
            $"/api/functional-roles/{created.Frrg}");
        var archiveAgainResponse = await admin.DeleteAsync(
            $"/api/functional-roles/{created.Frrg}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, archiveAgainResponse.StatusCode);

        var roles = await admin.GetFromJsonAsync<FunctionalRoleLookup[]>(
            "/api/functional-roles");
        Assert.Equal("Archived", Assert.Single(roles!).Status);
    }

    [Fact]
    public async Task Users_SupportRoleReplacementAndActivationChanges()
    {
        await using var factory = CreateAdminFactory();
        await factory.MigrateDatabaseAsync();
        using var admin = factory.CreateAuthenticatedClient("admin", "Ada Admin");
        using var target = factory.CreateAuthenticatedClient("target", "Terry Target");
        await ProvisionAsync(admin);
        await ProvisionAsync(target);
        await GrantDirectlyAsync(
            factory,
            "admin",
            ApplicationPermission.ManageRoles);

        var roleResponse = await admin.PostAsJsonAsync(
            "/api/functional-roles",
            new SaveFunctionalRoleRequest("Reviewer", "Reviews work."));
        var role = await roleResponse.Content.ReadFromJsonAsync<FunctionalRoleLookup>();
        var targetId = await GetUserIdAsync(factory, "target");

        var replaceResponse = await admin.PutAsJsonAsync(
            $"/api/users/{targetId}/functional-roles",
            new ReplaceUserFunctionalRolesRequest([role!.Frrg]));
        var replaced = await replaceResponse.Content.ReadFromJsonAsync<UserLookup>();
        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        Assert.Equal(role.Frrg, Assert.Single(replaced!.Roles).Frrg);

        var duplicateResponse = await admin.PutAsJsonAsync(
            $"/api/users/{targetId}/functional-roles",
            new ReplaceUserFunctionalRolesRequest([role.Frrg, role.Frrg]));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var deactivateResponse = await admin.PatchAsJsonAsync(
            $"/api/users/{targetId}/active",
            new SetUserActiveRequest(false));
        var deactivated = await deactivateResponse.Content
            .ReadFromJsonAsync<UserLookup>();
        Assert.False(deactivated!.Active);

        var activateResponse = await admin.PatchAsJsonAsync(
            $"/api/users/{targetId}/active",
            new SetUserActiveRequest(true));
        var activated = await activateResponse.Content.ReadFromJsonAsync<UserLookup>();
        Assert.True(activated!.Active);
    }

    [Fact]
    public async Task PermissionGrants_RecordAuditDataAndSupportGrantAndRevoke()
    {
        await using var factory = CreateAdminFactory();
        await factory.MigrateDatabaseAsync();
        using var admin = factory.CreateAuthenticatedClient("admin", "Ada Admin");
        using var target = factory.CreateAuthenticatedClient("target", "Terry Target");
        await ProvisionAsync(admin);
        await ProvisionAsync(target);
        var adminId = await GetUserIdAsync(factory, "admin");
        var targetId = await GetUserIdAsync(factory, "target");

        var grantResponse = await admin.PostAsJsonAsync(
            "/api/permission-grants",
            new GrantPermissionRequest(
                targetId,
                PermissionContracts.ManageTemplates));
        var grant = await grantResponse.Content
            .ReadFromJsonAsync<PermissionGrantDetails>();

        Assert.Equal(HttpStatusCode.Created, grantResponse.StatusCode);
        Assert.NotNull(grant);
        Assert.NotEqual(Guid.Empty, grant.Pgrg);
        Assert.Equal(targetId, grant.Grg);
        Assert.Equal("Terry Target", grant.UserName);
        Assert.Equal(adminId, grant.GrantingActorGrg);
        Assert.Equal("Ada Admin", grant.GrantingActorName);
        Assert.NotEqual(default, grant.GrantedAtUtc);

        var duplicateResponse = await admin.PostAsJsonAsync(
            "/api/permission-grants",
            new GrantPermissionRequest(
                targetId,
                PermissionContracts.ManageTemplates));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        var grants = await admin.GetFromJsonAsync<PermissionGrantDetails[]>(
            "/api/permission-grants");
        var targetProfile = await target.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");

        Assert.NotNull(grants);
        Assert.Contains(grants, item => item.Pgrg == grant.Pgrg);
        Assert.DoesNotContain(
            grants,
            item =>
                item.Grg == targetId
                && item.Permission == PermissionContracts.ViewTemplates);
        Assert.Contains(
            PermissionContracts.ManageTemplates,
            targetProfile!.Permissions);
        Assert.Contains(
            PermissionContracts.ViewTemplates,
            targetProfile.Permissions);

        var revokeResponse = await admin.DeleteAsync(
            $"/api/permission-grants/{grant.Pgrg}");
        var revokeAgainResponse = await admin.DeleteAsync(
            $"/api/permission-grants/{grant.Pgrg}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revokeAgainResponse.StatusCode);
    }

    [Fact]
    public async Task FinalActivePermissionsAdministrator_CannotLoseAccess()
    {
        await using var factory = CreateAdminFactory();
        await factory.MigrateDatabaseAsync();
        using var admin = factory.CreateAuthenticatedClient("admin", "Ada Admin");
        await ProvisionAsync(admin);
        await GrantDirectlyAsync(
            factory,
            "admin",
            ApplicationPermission.ManageRoles);
        var adminId = await GetUserIdAsync(factory, "admin");

        Guid managePermissionsGrantId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext =
                scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
            managePermissionsGrantId = await dbContext.PermissionGrants
                .Where(
                    grant =>
                        grant.UserId == adminId
                        && grant.Permission
                            == ApplicationPermission.ManagePermissions)
                .Select(grant => grant.Id)
                .SingleAsync();
        }

        var revokeResponse = await admin.DeleteAsync(
            $"/api/permission-grants/{managePermissionsGrantId}");
        var deactivateResponse = await admin.PatchAsJsonAsync(
            $"/api/users/{adminId}/active",
            new SetUserActiveRequest(false));

        Assert.Equal(HttpStatusCode.Conflict, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deactivateResponse.StatusCode);
    }

    [Fact]
    public async Task AdministrationEndpoints_RequireTheirCentralizedPolicies()
    {
        await using var factory = CreateAdminFactory();
        await factory.MigrateDatabaseAsync();
        using var regular = factory.CreateAuthenticatedClient("regular");
        await ProvisionAsync(regular);

        var roleResponse = await regular.PostAsJsonAsync(
            "/api/functional-roles",
            new SaveFunctionalRoleRequest("Denied", ""));
        var grantsResponse = await regular.GetAsync("/api/permission-grants");

        Assert.Equal(HttpStatusCode.Forbidden, roleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grantsResponse.StatusCode);
    }

    private static IntablyApiFactory CreateAdminFactory()
    {
        return new IntablyApiFactory("integration-test-tenant", "admin");
    }

    private static async Task ProvisionAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/users/me");
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> GetUserIdAsync(
        IntablyApiFactory factory,
        string objectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        return await dbContext.Users
            .Where(user => user.EntraObjectId == objectId)
            .Select(user => user.Id)
            .SingleAsync();
    }

    private static async Task GrantDirectlyAsync(
        IntablyApiFactory factory,
        string objectId,
        ApplicationPermission permission)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var user = await dbContext.Users.SingleAsync(
            candidate => candidate.EntraObjectId == objectId);
        dbContext.PermissionGrants.Add(
            new Intably.Domain.Permissions.PermissionGrant(
                user.Id,
                permission,
                user.Id,
                DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }
}
