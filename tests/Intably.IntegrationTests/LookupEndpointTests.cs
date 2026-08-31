using System.Net;
using System.Net.Http.Json;
using Intably.Application.Roles;
using Intably.Application.Users;
using Intably.Domain.Roles;
using Intably.Domain.Users;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.IntegrationTests;

public sealed class LookupEndpointTests
{
    [Theory]
    [InlineData("/api/functional-roles")]
    [InlineData("/api/users")]
    public async Task LookupEndpoint_WithoutApiKey_ReturnsUnauthorized(string path)
    {
        await using var factory = new IntablyApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFunctionalRoles_ReturnsCompleteActiveAndArchivedDataset()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        var (_, activeRole, archivedRole) = await SeedLookupDataAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var roles = await client.GetFromJsonAsync<FunctionalRoleLookup[]>(
            "/api/functional-roles");

        Assert.NotNull(roles);
        Assert.Collection(
            roles,
            role =>
            {
                Assert.Equal(activeRole.Id, role.Frrg);
                Assert.Equal("Active", role.Status);
            },
            role =>
            {
                Assert.Equal(archivedRole.Id, role.Frrg);
                Assert.Equal("Archived", role.Status);
            });
    }

    [Fact]
    public async Task LookupEndpoints_IncludeProvisionedRequester()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var roles = await client.GetFromJsonAsync<FunctionalRoleLookup[]>(
            "/api/functional-roles");
        var users = await client.GetFromJsonAsync<UserLookup[]>("/api/users");

        Assert.Empty(roles!);
        Assert.Equal(
            "integration@example.com",
            Assert.Single(users!).Email);
    }

    [Fact]
    public async Task GetUsers_ReturnsCompleteDatasetWithRoleMemberships()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        var (user, activeRole, _) = await SeedLookupDataAsync(factory);
        using var client = factory.CreateAuthenticatedClient();

        var users = await client.GetFromJsonAsync<UserLookup[]>("/api/users");

        var result = Assert.Single(users!, item => item.Grg == user.Id);
        Assert.Equal(user.Id, result.Grg);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.True(result.Active);
        var role = Assert.Single(result.Roles);
        Assert.Equal(activeRole.Id, role.Frrg);
        Assert.Equal(activeRole.Name, role.Name);
        Assert.Equal("Active", role.Status);
    }

    private static async Task<(User User, FunctionalRole ActiveRole,
        FunctionalRole ArchivedRole)> SeedLookupDataAsync(IntablyApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var createdAt = new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);
        var activeRole = FunctionalRole.Create(
            "Approver",
            "Approves process steps.",
            createdAt);
        var archivedRole = FunctionalRole.Create(
            "Legacy",
            "Retained for historical templates.",
            createdAt.AddMinutes(1));
        var user = User.Create(
            "tenant",
            "object",
            "Avery Adams",
            "avery@example.com",
            createdAt);

        dbContext.AddRange(activeRole, archivedRole, user);
        dbContext.UserFunctionalRoles.Add(
            new UserFunctionalRole(user.Id, activeRole.Id, createdAt));
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE FunctionalRoles SET IsArchived = 1 WHERE frrg = {archivedRole.Id}");

        return (user, activeRole, archivedRole);
    }
}
