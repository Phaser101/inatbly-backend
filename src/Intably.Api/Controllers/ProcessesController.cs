using Intably.Api.Authentication;
using Intably.Application.Processes;
using Intably.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.ViewProcesses)]
[Route("api/processes")]
public sealed class ProcessesController(
    IProcessService processService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet(Name = "IN_011")]
    public async Task<ActionResult<IReadOnlyCollection<ProcessSummary>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await processService.GetAllAsync(cancellationToken));
    }

    [HttpPost(Name = "IN_012")]
    [Authorize(Policy = AuthorizationPolicies.StartProcesses)]
    public async Task<ActionResult<ProcessDetails>> Start(
        StartProcessRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await GetCurrentUserAsync(cancellationToken);
        if (actor is null)
        {
            return Forbid();
        }

        try
        {
            var process = await processService.StartAsync(
                request,
                actor,
                cancellationToken);
            return CreatedAtRoute("IN_013", new { pirg = process.Pirg }, process);
        }
        catch (Exception exception) when (IsProcessException(exception))
        {
            return MapException(exception);
        }
    }

    [HttpGet("{pirg:guid}", Name = "IN_013")]
    public async Task<ActionResult<ProcessDetails>> Get(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await processService.GetAsync(pirg, cancellationToken));
        }
        catch (ProcessNotFoundException exception)
        {
            return MapException(exception);
        }
    }

    [HttpPatch("{pirg:guid}/information/{rfrg:guid}", Name = "IN_020")]
    public async Task<ActionResult<ProcessDetails>> UpdateInformation(
        Guid pirg,
        Guid rfrg,
        UpdateProcessInformationRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await GetCurrentUserAsync(cancellationToken);
        if (actor is null)
        {
            return Forbid();
        }

        try
        {
            return Ok(await processService.UpdateInformationAsync(
                pirg,
                rfrg,
                request,
                actor,
                cancellationToken));
        }
        catch (Exception exception) when (IsProcessException(exception))
        {
            return MapException(exception);
        }
    }

    [HttpPatch("{pirg:guid}/steps/{psrg:guid}/status", Name = "IN_014")]
    public async Task<ActionResult<ProcessDetails>> SetStepStatus(
        Guid pirg,
        Guid psrg,
        SetProcessStepStatusRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await GetCurrentUserAsync(cancellationToken);
        if (actor is null)
        {
            return Forbid();
        }

        try
        {
            return Ok(await processService.SetStepStatusAsync(
                pirg,
                psrg,
                request,
                actor,
                cancellationToken));
        }
        catch (Exception exception) when (IsProcessException(exception))
        {
            return MapException(exception);
        }
    }

    [HttpPatch("{pirg:guid}/steps/{psrg:guid}/assignment", Name = "IN_015")]
    public async Task<ActionResult<ProcessDetails>> AssignStep(
        Guid pirg,
        Guid psrg,
        AssignProcessStepRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await GetCurrentUserAsync(cancellationToken);
        if (actor is null)
        {
            return Forbid();
        }

        try
        {
            return Ok(await processService.AssignStepAsync(
                pirg,
                psrg,
                request,
                actor,
                cancellationToken));
        }
        catch (Exception exception) when (IsProcessException(exception))
        {
            return MapException(exception);
        }
    }

    [HttpGet(
        "{pirg:guid}/steps/{psrg:guid}/eligible-assignees",
        Name = "IN_016")]
    public async Task<ActionResult<IReadOnlyCollection<EligibleAssignee>>>
        GetEligibleAssignees(
            Guid pirg,
            Guid psrg,
            CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await processService.GetEligibleAssigneesAsync(
                pirg,
                psrg,
                cancellationToken));
        }
        catch (ProcessNotFoundException exception)
        {
            return MapException(exception);
        }
    }

    [HttpPost("{pirg:guid}/close", Name = "IN_017")]
    public async Task<ActionResult<ProcessDetails>> Close(
        Guid pirg,
        CloseProcessRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await GetCurrentUserAsync(cancellationToken);
        if (actor is null)
        {
            return Forbid();
        }

        try
        {
            return Ok(await processService.CloseAsync(
                pirg,
                request,
                actor,
                cancellationToken));
        }
        catch (Exception exception) when (IsProcessException(exception))
        {
            return MapException(exception);
        }
    }

    [HttpGet("{pirg:guid}/timeline", Name = "IN_018")]
    public async Task<ActionResult<IReadOnlyCollection<ProcessTimelineEvent>>>
        GetTimeline(Guid pirg, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await processService.GetTimelineAsync(
                pirg,
                cancellationToken));
        }
        catch (ProcessNotFoundException exception)
        {
            return MapException(exception);
        }
    }

    [HttpGet("{pirg:guid}/export", Name = "IN_019")]
    public async Task<IActionResult> Export(
        Guid pirg,
        CancellationToken cancellationToken)
    {
        try
        {
            var markdown = await processService.ExportMarkdownAsync(
                pirg,
                cancellationToken);
            return Content(markdown, "text/markdown; charset=utf-8");
        }
        catch (ProcessNotFoundException exception)
        {
            return MapException(exception);
        }
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

    private static bool IsProcessException(Exception exception)
    {
        return exception is ProcessValidationException
            or ProcessForbiddenException
            or ProcessNotFoundException
            or ProcessConflictException;
    }

    private ObjectResult MapException(Exception exception)
    {
        var (status, title) = exception switch
        {
            ProcessValidationException =>
                (StatusCodes.Status400BadRequest, "The process request is invalid."),
            ProcessForbiddenException =>
                (StatusCodes.Status403Forbidden, "The process action is forbidden."),
            ProcessNotFoundException =>
                (StatusCodes.Status404NotFound, "The process resource was not found."),
            ProcessConflictException =>
                (StatusCodes.Status409Conflict, "The process action conflicts with its current state."),
            _ => throw exception,
        };

        return StatusCode(status, new ProblemDetails
        {
            Title = title,
            Detail = exception.Message,
            Status = status,
        });
    }
}
