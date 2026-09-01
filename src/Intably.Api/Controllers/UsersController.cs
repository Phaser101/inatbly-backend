using Intably.Api.Authentication;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiAccess)]
[Route("api/users")]
public sealed class UsersController(
    IUserLookupService lookupService,
    IUserAdministrationService administrationService)
    : ControllerBase
{
    [HttpGet(Name = "IN_023")]
    public async Task<ActionResult<IReadOnlyCollection<UserLookup>>> GetUsers(
        CancellationToken cancellationToken)
    {
        return Ok(await lookupService.GetAllAsync(cancellationToken));
    }

    [HttpPut("{grg:guid}/functional-roles", Name = "IN_027")]
    [Authorize(Policy = AuthorizationPolicies.ManageMembership)]
    public async Task<ActionResult<UserLookup>> ReplaceFunctionalRoles(
        Guid grg,
        ReplaceUserFunctionalRolesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await administrationService.ReplaceFunctionalRolesAsync(
            grg,
            request,
            cancellationToken));
    }

    [HttpPatch("{grg:guid}/active", Name = "IN_028")]
    [Authorize(Policy = AuthorizationPolicies.ManageUserStatus)]
    public async Task<ActionResult<UserLookup>> SetActive(
        Guid grg,
        SetUserActiveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await administrationService.SetActiveAsync(
            grg,
            request,
            cancellationToken));
    }
}
