using Intably.Api.Authentication;
using Intably.Application.Templates;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ViewTemplates)]
[Route("api/templates")]
public sealed class TemplatesController(
    ITemplateService templateService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet(Name = "IN_002")]
    public async Task<ActionResult<IReadOnlyCollection<TemplateSummary>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await templateService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{ptrg:guid}", Name = "IN_003")]
    public async Task<ActionResult<TemplateDetails>> Get(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var template = await templateService.GetAsync(ptrg, cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpGet("{ptrg:guid}/published", Name = "IN_004")]
    public async Task<ActionResult<TemplateDetails>> GetPublished(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var template = await templateService.GetPublishedAsync(
            ptrg,
            cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost(Name = "IN_005")]
    [Authorize(Policy = AuthorizationPolicies.ManageTemplates)]
    public async Task<ActionResult<TemplateDetails>> Create(
        SaveTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Forbid();
        }

        try
        {
            var template = await templateService.CreateAsync(
                request,
                currentUser.Grg,
                cancellationToken);
            return CreatedAtRoute("IN_003", new { ptrg = template.Ptrg }, template);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    [HttpPut("{ptrg:guid}", Name = "IN_006")]
    [Authorize(Policy = AuthorizationPolicies.ManageTemplates)]
    public async Task<ActionResult<TemplateDetails>> Update(
        Guid ptrg,
        SaveTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await templateService.UpdateAsync(
                ptrg,
                request,
                cancellationToken);
            return template is null ? NotFound() : Ok(template);
        }
        catch (ArgumentException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    [HttpPost("{ptrg:guid}/publish", Name = "IN_007")]
    [Authorize(Policy = AuthorizationPolicies.ManageTemplates)]
    public async Task<ActionResult<TemplateDetails>> Publish(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await templateService.PublishAsync(
                ptrg,
                cancellationToken);
            return template is null ? NotFound() : Ok(template);
        }
        catch (InvalidOperationException exception)
        {
            return InvalidRequest(exception.Message);
        }
    }

    [HttpPost("{ptrg:guid}/duplicate", Name = "IN_008")]
    [Authorize(Policy = AuthorizationPolicies.ManageTemplates)]
    public async Task<ActionResult<TemplateDetails>> Duplicate(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
        {
            return Forbid();
        }

        var template = await templateService.DuplicateAsync(
            ptrg,
            currentUser.Grg,
            cancellationToken);
        return template is null
            ? NotFound()
            : CreatedAtRoute("IN_003", new { ptrg = template.Ptrg }, template);
    }

    [HttpDelete("{ptrg:guid}", Name = "IN_009")]
    [Authorize(Policy = AuthorizationPolicies.ManageTemplates)]
    public async Task<IActionResult> Archive(
        Guid ptrg,
        CancellationToken cancellationToken)
    {
        return await templateService.ArchiveAsync(ptrg, cancellationToken)
            ? NoContent()
            : NotFound();
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

    private BadRequestObjectResult InvalidRequest(string detail)
    {
        return BadRequest(new ProblemDetails
        {
            Title = "The template request is invalid.",
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
        });
    }
}
