using Microsoft.Extensions.Options;

namespace Intably.Api.Authentication;

public enum BackendTrustMode
{
    Unknown = 0,
    DevelopmentHeaders,
    TrustedGateway,
}

public sealed class BackendTrustOptions
{
    public const string SectionName = "BackendTrust";

    public BackendTrustMode Mode { get; init; }

    public string DevelopmentSubscriptionKey { get; init; } = string.Empty;

    public string GatewayKey { get; init; } = string.Empty;
}

public sealed class BackendTrustOptionsValidator
    : IValidateOptions<BackendTrustOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        BackendTrustOptions options)
    {
        if (
            options.Mode is not BackendTrustMode.DevelopmentHeaders
            and not BackendTrustMode.TrustedGateway)
        {
            return ValidateOptionsResult.Fail(
                "BackendTrust:Mode must be DevelopmentHeaders or TrustedGateway.");
        }

        if (
            options.Mode == BackendTrustMode.DevelopmentHeaders
            && string.IsNullOrWhiteSpace(options.DevelopmentSubscriptionKey))
        {
            return ValidateOptionsResult.Fail(
                "BackendTrust:DevelopmentSubscriptionKey is required in DevelopmentHeaders mode.");
        }

        if (
            options.Mode == BackendTrustMode.TrustedGateway
            && string.IsNullOrWhiteSpace(options.GatewayKey))
        {
            return ValidateOptionsResult.Fail(
                "BackendTrust:GatewayKey is required in TrustedGateway mode.");
        }

        return ValidateOptionsResult.Success;
    }
}
