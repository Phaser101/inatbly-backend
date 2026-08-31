using Intably.Api.Authentication;
using Intably.Application.Permissions;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ManagePermissions)]
[Route("api/permission-grants")]
public sealed class PermissionGrantsController(
    IPermissionGrantService permissionGrantService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet(Name = "IN_029")]
    public async Task<ActionResult<IReadOnlyCollection<PermissionGrantDetails>>>
        GetAll(CancellationToken cancellationToken)
    {
        return Ok(await permissionGrantService.GetAllAsync(cancellationToken));
    }

    [HttpPost(Name = "IN_030")]
    public async Task<ActionResult<PermissionGrantDetails>> Grant(
        GrantPermissionRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Forbid();
        }

        var grant = await permissionGrantService.GrantAsync(
            request,
            currentUser.Grg,
            cancellationToken);
        return Created($"/api/permission-grants/{grant.Pgrg}", grant);
    }

    [HttpDelete("{pgrg:guid}", Name = "IN_031")]
    public async Task<IActionResult> Revoke(
        Guid pgrg,
        CancellationToken cancellationToken)
    {
        await permissionGrantService.RevokeAsync(pgrg, cancellationToken);
        return NoContent();
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
