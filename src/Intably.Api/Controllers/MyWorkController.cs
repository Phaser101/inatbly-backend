using Intably.Api.Authentication;
using Intably.Application.MyWork;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ViewMyWork)]
[Route("api/my-work")]
public sealed class MyWorkController(
    IMyWorkService myWorkService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet(Name = "IN_010")]
    public async Task<ActionResult<IReadOnlyCollection<MyWorkItem>>> Get(
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Forbid();
        }

        return Ok(await myWorkService.GetAsync(currentUser, cancellationToken));
    }

    private async Task<CurrentUserProfile?> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        var identity = User.GetExternalIdentity();
        if (identity is null)
        {
            return null;
        }

        var profile = await currentUserService.GetAsync(
            identity,
            cancellationToken);
        return profile is { Active: true } ? profile : null;
    }
}
