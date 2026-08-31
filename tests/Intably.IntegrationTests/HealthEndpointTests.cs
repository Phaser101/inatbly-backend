using System.Net;

namespace Intably.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task GetHealth_ReturnsSuccess()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetCurrentUser_WithoutApiKey_ReturnsUnauthorized()
    {
        await using var factory = new IntablyApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
