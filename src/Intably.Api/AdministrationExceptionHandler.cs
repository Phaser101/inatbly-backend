using Intably.Application.Administration;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Intably.Api;

public sealed class AdministrationExceptionHandler(
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            AdministrationValidationException =>
                StatusCodes.Status400BadRequest,
            AdministrationNotFoundException =>
                StatusCodes.Status404NotFound,
            AdministrationConflictException =>
                StatusCodes.Status409Conflict,
            _ => 0,
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = "The administration request could not be completed.",
                    Detail = exception.Message,
                },
                Exception = exception,
            });
    }
}
