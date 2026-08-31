using Intably.Api.Authentication;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiAccess)]
[Route("api/users/me")]
public sealed class CurrentUserController(ICurrentUserService currentUserService)
    : ControllerBase
{
    [HttpGet(Name = "IN_001")]
    [ProducesResponseType<CurrentUserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CurrentUserProfile>> Get(
        CancellationToken cancellationToken)
    {
        var identity = User.GetExternalIdentity();
        if (identity is null)
        {
            return Unauthorized();
        }

        var profile = await currentUserService.GetAsync(
            identity,
            cancellationToken);

        return profile is null || !profile.Active ? Forbid() : Ok(profile);
    }
}
