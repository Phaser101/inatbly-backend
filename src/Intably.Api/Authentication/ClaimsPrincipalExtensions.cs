using System.Security.Claims;
using Intably.Application.Users;

namespace Intably.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static ExternalUserIdentity? GetExternalIdentity(
        this ClaimsPrincipal principal)
    {
        var tenantId = principal.FindFirstValue("tid");
        var objectId = principal.FindFirstValue("oid");
        var displayName = principal.FindFirstValue("name");
        var email = principal.FindFirstValue("preferred_username");

        return string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(objectId)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(email)
                ? null
                : new ExternalUserIdentity(
                    tenantId,
                    objectId,
                    displayName,
                    email);
    }
}
