using Intably.Api.Authentication;
using Intably.Application.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ApiAccess)]
[Route("api/functional-roles")]
public sealed class FunctionalRolesController(
    IFunctionalRoleLookupService lookupService,
    IFunctionalRoleAdministrationService administrationService) : ControllerBase
{
    [HttpGet(Name = "IN_021")]
    public async Task<ActionResult<IReadOnlyCollection<FunctionalRoleLookup>>>
        GetFunctionalRoles(CancellationToken cancellationToken)
    {
        return Ok(await lookupService.GetAllAsync(cancellationToken));
    }

    [HttpPost(Name = "IN_024")]
    [Authorize(Policy = AuthorizationPolicies.ManageRoles)]
    public async Task<ActionResult<FunctionalRoleLookup>> Create(
        SaveFunctionalRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = await administrationService.CreateAsync(
            request,
            cancellationToken);
        return Created($"/api/functional-roles/{role.Frrg}", role);
    }

    [HttpPut("{frrg:guid}", Name = "IN_025")]
    [Authorize(Policy = AuthorizationPolicies.ManageRoles)]
    public async Task<ActionResult<FunctionalRoleLookup>> Update(
        Guid frrg,
        SaveFunctionalRoleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await administrationService.UpdateAsync(
            frrg,
            request,
            cancellationToken));
    }

    [HttpDelete("{frrg:guid}", Name = "IN_026")]
    [Authorize(Policy = AuthorizationPolicies.ManageRoles)]
    public async Task<IActionResult> Archive(
        Guid frrg,
        CancellationToken cancellationToken)
    {
        await administrationService.ArchiveAsync(frrg, cancellationToken);
        return NoContent();
    }
}
