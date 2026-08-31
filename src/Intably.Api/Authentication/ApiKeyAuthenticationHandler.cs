using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Intably.Api.Authentication;

public sealed class BackendTrustAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<BackendTrustOptions> backendTrustOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Intably";
    public const string SubscriptionKeyHeader = "Ocp-Apim-Subscription-Key";
    public const string GatewayKeyHeader = "X-Intably-Gateway-Key";
    public const string TenantIdHeader = "X-Intably-Tenant-Id";
    public const string UserIdHeader = "X-Intably-User-Id";
    public const string UserNameHeader = "X-Intably-User-Name";
    public const string UserEmailHeader = "X-Intably-User-Email";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var trustResult = AuthenticateBackendTrust(backendTrustOptions.Value);
        if (trustResult is not null)
        {
            return Task.FromResult(trustResult);
        }

        var tenantId = GetDecodedHeader(TenantIdHeader);
        var userId = GetDecodedHeader(UserIdHeader);
        var userName = GetDecodedHeader(UserNameHeader);
        var userEmail = GetDecodedHeader(UserEmailHeader);

        if (
            string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(userId)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(userEmail))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("The Intably user headers are required."));
        }

        Claim[] claims =
        [
            new("tid", tenantId),
            new("oid", userId),
            new("name", userName),
            new("preferred_username", userEmail),
        ];
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private AuthenticateResult? AuthenticateBackendTrust(
        BackendTrustOptions trustOptions)
    {
        return trustOptions.Mode switch
        {
            BackendTrustMode.DevelopmentHeaders => AuthenticateSecret(
                trustOptions.DevelopmentSubscriptionKey,
                SubscriptionKeyHeader,
                "The API subscription key is invalid."),
            BackendTrustMode.TrustedGateway => AuthenticateSecret(
                trustOptions.GatewayKey,
                GatewayKeyHeader,
                "The Intably gateway key is invalid."),
            _ => AuthenticateResult.Fail(
                "The backend trust mode is not configured."),
        };
    }

    private AuthenticateResult? AuthenticateSecret(
        string configuredSecret,
        string headerName,
        string failureMessage)
    {
        var suppliedSecret = Request.Headers[headerName].ToString();

        if (string.IsNullOrWhiteSpace(suppliedSecret))
        {
            return AuthenticateResult.NoResult();
        }

        if (
            string.IsNullOrWhiteSpace(configuredSecret)
            || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(configuredSecret)),
                SHA256.HashData(Encoding.UTF8.GetBytes(suppliedSecret))))
        {
            return AuthenticateResult.Fail(failureMessage);
        }

        return null;
    }

    private string GetDecodedHeader(string name)
    {
        var value = Request.Headers[name].ToString();
        return string.IsNullOrWhiteSpace(value) ? value : Uri.UnescapeDataString(value);
    }
}
