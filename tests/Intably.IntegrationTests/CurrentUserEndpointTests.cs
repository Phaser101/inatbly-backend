using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Intably.Api.Authentication;
using Intably.Application.Permissions;
using Intably.Application.Users;
using Intably.Domain.Permissions;
using Intably.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.IntegrationTests;

public sealed class CurrentUserEndpointTests
{
    [Fact]
    public async Task TrustedGateway_FirstLogin_ProvisionsActiveUserWithoutAccess()
    {
        await using var factory = new IntablyApiFactory(
            autoProvisionUsers: false,
            backendTrustMode: nameof(BackendTrustMode.TrustedGateway),
            gatewayKey: "integration-gateway-key");
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/users/me");
        var profile =
            await response.Content.ReadFromJsonAsync<CurrentUserProfile>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal("Integration Test User", profile.DisplayName);
        Assert.Equal("integration@example.com", profile.Email);
        Assert.True(profile.Active);
        Assert.Empty(profile.Roles);
        Assert.Empty(profile.Permissions);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var user = await dbContext.Users.SingleAsync();
        Assert.True(user.IsActive);
        Assert.False(
            await dbContext.UserFunctionalRoles.AnyAsync(
                membership => membership.UserId == user.Id));
        Assert.False(
            await dbContext.PermissionGrants.AnyAsync(
                grant => grant.UserId == user.Id));
    }

    [Fact]
    public async Task TrustedGateway_RepeatedLogin_ReusesProvisionedUser()
    {
        await using var factory = new IntablyApiFactory(
            autoProvisionUsers: false,
            backendTrustMode: nameof(BackendTrustMode.TrustedGateway),
            gatewayKey: "integration-gateway-key");
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var first = await client.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        var second = await client.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Grg, second.Grg);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task TrustedGateway_ConcurrentFirstLogin_ReusesInsertWinner()
    {
        await using var factory = new IntablyApiFactory(
            autoProvisionUsers: false,
            backendTrustMode: nameof(BackendTrustMode.TrustedGateway),
            gatewayKey: "integration-gateway-key");
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var requests = Enumerable.Range(0, 8)
            .Select(_ => client.GetAsync("/api/users/me"))
            .ToArray();
        var responses = await Task.WhenAll(requests);

        Assert.All(
            responses,
            response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task DevelopmentHeaders_WithProvisioningDisabled_DoesNotCreateUser()
    {
        await using var factory = new IntablyApiFactory(autoProvisionUsers: false);
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("unknown-user");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        Assert.False(await dbContext.Users.AnyAsync());
    }

    [Fact]
    public async Task DevelopmentHeaders_OutsideDevelopment_DoesNotCreateUser()
    {
        await using var factory = new IntablyApiFactory(environment: "Production");
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("unknown-user");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        Assert.False(await dbContext.Users.AnyAsync());
    }

    [Fact]
    public async Task GetCurrentUser_ForConfiguredFirstAdmin_GrantsPermissionOnce()
    {
        await using var factory = new IntablyApiFactory(
            "integration-test-tenant",
            "integration-test-user",
            autoProvisionUsers: false,
            backendTrustMode: nameof(BackendTrustMode.TrustedGateway),
            gatewayKey: "integration-gateway-key");
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient(
            email: "any-current-address@example.com");

        var first = await client.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        var second = await client.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Contains(PermissionContracts.ManagePermissions, first.Permissions);
        Assert.Contains(PermissionContracts.ManagePermissions, second.Permissions);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var grants = await dbContext.PermissionGrants
            .Where(
                grant =>
                    grant.Permission
                    == ApplicationPermission.ManagePermissions)
            .ToArrayAsync();

        var grant = Assert.Single(grants);
        Assert.Equal(grant.UserId, grant.GrantedByUserId);
    }

    [Fact]
    public async Task ManagePermissionsPolicy_UsesActiveDatabaseUserGrant()
    {
        await using var factory = new IntablyApiFactory(
            "integration-test-tenant",
            "admin-user");
        await factory.MigrateDatabaseAsync();
        using var adminClient = factory.CreateAuthenticatedClient("admin-user");
        using var regularClient = factory.CreateAuthenticatedClient("regular-user");
        await adminClient.GetAsync("/api/users/me");
        await regularClient.GetAsync("/api/users/me");

        await using var scope = factory.Services.CreateAsyncScope();
        var authorization = scope.ServiceProvider
            .GetRequiredService<IAuthorizationService>();

        var adminResult = await authorization.AuthorizeAsync(
            CreatePrincipal("admin-user"),
            resource: null,
            AuthorizationPolicies.ManagePermissions);
        var regularResult = await authorization.AuthorizeAsync(
            CreatePrincipal("regular-user"),
            resource: null,
            AuthorizationPolicies.ManagePermissions);

        Assert.True(adminResult.Succeeded);
        Assert.False(regularResult.Succeeded);
    }

    [Fact]
    public async Task InactiveProvisionedUser_IsForbiddenAcrossProtectedApis()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("inactive-user");
        await client.GetAsync("/api/users/me");
        await factory.SetActiveAsync("inactive-user", false);

        string[] protectedReads =
        [
            "/api/users/me",
            "/api/users",
            "/api/functional-roles",
            "/api/templates",
            "/api/processes",
            "/api/my-work",
            $"/api/processes/{Guid.NewGuid()}/timeline",
            $"/api/processes/{Guid.NewGuid()}/export",
            $"/api/processes/{Guid.NewGuid()}/steps/{Guid.NewGuid()}/eligible-assignees",
            "/api/permission-grants",
        ];

        foreach (var path in protectedReads)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var templateMutation = await client.PostAsJsonAsync(
            "/api/templates",
            new { });
        var processMutation = await client.PostAsJsonAsync(
            "/api/processes",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, templateMutation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, processMutation.StatusCode);
    }

    private static ClaimsPrincipal CreatePrincipal(string objectId)
    {
        Claim[] claims =
        [
            new("tid", "integration-test-tenant"),
            new("oid", objectId),
            new("name", "Integration Test User"),
            new("preferred_username", "unrelated@example.com"),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
