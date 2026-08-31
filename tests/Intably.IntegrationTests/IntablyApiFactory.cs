using Intably.Api.Authentication;
using Intably.Domain.Permissions;
using Intably.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Intably.IntegrationTests;

public sealed class IntablyApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database=IntablyIntegrationTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
    private readonly string _firstAdminTenantId;
    private readonly string _firstAdminObjectId;
    private readonly bool _autoProvisionUsers;
    private readonly string? _backendTrustMode;
    private readonly string? _developmentSubscriptionKey;
    private readonly string? _gatewayKey;
    private readonly string _environment;
    private bool _databaseMigrated;

    public IntablyApiFactory(
        string firstAdminTenantId = "",
        string firstAdminObjectId = "",
        bool autoProvisionUsers = true,
        string? backendTrustMode = nameof(BackendTrustMode.DevelopmentHeaders),
        string? developmentSubscriptionKey = "integration-test-key",
        string? gatewayKey = "",
        string environment = "Development")
    {
        _firstAdminTenantId = firstAdminTenantId;
        _firstAdminObjectId = firstAdminObjectId;
        _autoProvisionUsers = autoProvisionUsers;
        _backendTrustMode = backendTrustMode;
        _developmentSubscriptionKey = developmentSubscriptionKey;
        _gatewayKey = gatewayKey;
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Database", _connectionString);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] =
                        _connectionString,
                    ["BackendTrust:Mode"] = _backendTrustMode,
                    ["BackendTrust:DevelopmentSubscriptionKey"] =
                        _developmentSubscriptionKey,
                    ["BackendTrust:GatewayKey"] = _gatewayKey,
                    ["UserProvisioning:AutoProvisionAuthenticatedUsers"] =
                        _autoProvisionUsers.ToString(),
                    ["FirstAdmin:EntraTenantId"] = _firstAdminTenantId,
                    ["FirstAdmin:EntraObjectId"] = _firstAdminObjectId,
                });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IntablyDbContext>();
            services.RemoveAll<DbContextOptions<IntablyDbContext>>();
            services.AddDbContext<IntablyDbContext>(
                options => options.UseSqlServer(_connectionString));
        });
    }

    public async Task MigrateDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        await dbContext.Database.MigrateAsync();
        _databaseMigrated = true;
    }

    public HttpClient CreateAuthenticatedClient(
        string objectId = "integration-test-user",
        string displayName = "Integration Test User",
        string email = "integration@example.com")
    {
        var client = CreateClient();
        if (string.Equals(
            _backendTrustMode,
            nameof(BackendTrustMode.TrustedGateway),
            StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Add(
                BackendTrustAuthenticationHandler.GatewayKeyHeader,
                _gatewayKey);
        }
        else
        {
            client.DefaultRequestHeaders.Add(
                BackendTrustAuthenticationHandler.SubscriptionKeyHeader,
                _developmentSubscriptionKey);
        }
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.TenantIdHeader,
            "integration-test-tenant");
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.UserIdHeader,
            Uri.EscapeDataString(objectId));
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.UserNameHeader,
            Uri.EscapeDataString(displayName));
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.UserEmailHeader,
            Uri.EscapeDataString(email));
        return client;
    }

    public async Task GrantPermissionAsync(
        HttpClient client,
        string objectId,
        ApplicationPermission permission)
    {
        var provisionResponse = await client.GetAsync("/api/users/me");
        provisionResponse.EnsureSuccessStatusCode();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var user = await dbContext.Users.SingleAsync(
            candidate =>
                candidate.EntraTenantId == "integration-test-tenant"
                && candidate.EntraObjectId == objectId);
        dbContext.PermissionGrants.Add(
            new PermissionGrant(
                user.Id,
                permission,
                user.Id,
                DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    public async Task SetActiveAsync(string objectId, bool active)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var user = await dbContext.Users.SingleAsync(
            candidate =>
                candidate.EntraTenantId == "integration-test-tenant"
                && candidate.EntraObjectId == objectId);
        if (active != user.IsActive)
        {
            if (active)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }
        }
        await dbContext.SaveChangesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_databaseMigrated)
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext =
                scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
