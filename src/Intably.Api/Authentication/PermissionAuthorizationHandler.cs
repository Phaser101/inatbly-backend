using Intably.Application.Permissions;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;

namespace Intably.Api.Authentication;

public sealed class PermissionAuthorizationHandler(
    ICurrentUserService currentUserService)
    : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var identity = context.User.GetExternalIdentity();
        if (identity is null)
        {
            return;
        }

        var user = await currentUserService.GetAsync(identity, CancellationToken.None);
        if (user is not { Active: true })
        {
            return;
        }

        foreach (var requirement in context.PendingRequirements.ToArray())
        {
            if (requirement is ActiveUserRequirement)
            {
                context.Succeed(requirement);
            }
            else if (
                requirement is PermissionRequirement permission
                && user.Permissions.Contains(
                    permission.Permission.ToContractName(),
                    StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
        }
    }
}
