using System.Net;
using Intably.Api.Authentication;
using Microsoft.Extensions.Options;

namespace Intably.IntegrationTests;

public sealed class BackendTrustAuthenticationTests
{
    [Fact]
    public async Task DevelopmentHeaders_WithSubscriptionKey_AcceptsIdentity()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentHeaders_WithOnlyGatewayKey_IsUnauthorized()
    {
        await using var factory = new IntablyApiFactory(gatewayKey: "gateway-key");
        using var client = factory.CreateClient();
        AddIdentityHeaders(client);
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.GatewayKeyHeader,
            "gateway-key");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TrustedGateway_WithGatewayKey_AcceptsRewrittenIdentity()
    {
        await using var factory = new IntablyApiFactory(
            backendTrustMode: nameof(BackendTrustMode.TrustedGateway),
            gatewayKey: "integration-gateway-key");
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateClient();
        AddIdentityHeaders(client);
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.GatewayKeyHeader,
            "integration-gateway-key");
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.SubscriptionKeyHeader,
            "browser-subscription-key-is-not-backend-trust");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("incorrect-gateway-key")]
    public async Task TrustedGateway_WithoutValidGatewayKey_IsUnauthorized(
        string? suppliedGatewayKey)
    {
        await using var factory = new IntablyApiFactory(
            backendTrustMode: nameof(BackendTrustMode.TrustedGateway),
            gatewayKey: "integration-gateway-key");
        using var client = factory.CreateClient();
        AddIdentityHeaders(client);
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.SubscriptionKeyHeader,
            "integration-test-key");
        if (suppliedGatewayKey is not null)
        {
            client.DefaultRequestHeaders.Add(
                BackendTrustAuthenticationHandler.GatewayKeyHeader,
                suppliedGatewayKey);
        }

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(nameof(BackendTrustMode.Unknown), "development-key", "gateway-key")]
    [InlineData(
        nameof(BackendTrustMode.DevelopmentHeaders),
        "",
        "gateway-key")]
    [InlineData(nameof(BackendTrustMode.TrustedGateway), "development-key", "")]
    public void InvalidBackendTrustConfiguration_FailsAtStartup(
        string mode,
        string developmentSubscriptionKey,
        string gatewayKey)
    {
        using var factory = new IntablyApiFactory(
            backendTrustMode: mode,
            developmentSubscriptionKey: developmentSubscriptionKey,
            gatewayKey: gatewayKey);

        var exception = Assert.Throws<OptionsValidationException>(
            factory.CreateClient);

        Assert.Contains("BackendTrust:", exception.Message);
    }

    private static void AddIdentityHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.TenantIdHeader,
            "integration-test-tenant");
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.UserIdHeader,
            "integration-test-user");
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.UserNameHeader,
            "Integration%20Test%20User");
        client.DefaultRequestHeaders.Add(
            BackendTrustAuthenticationHandler.UserEmailHeader,
            "integration%40example.com");
    }
}
