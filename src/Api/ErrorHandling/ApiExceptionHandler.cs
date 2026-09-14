using BusinessLogic.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.ErrorHandling;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            InvalidRequestException => (StatusCodes.Status400BadRequest, "Invalid request"),
            GroupNotFoundException => (StatusCodes.Status404NotFound, "Group not found"),
            RestaurantBusyException => (StatusCodes.Status503ServiceUnavailable, "Restaurant busy"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
        if (status == StatusCodes.Status503ServiceUnavailable)
            context.Response.Headers.RetryAfter = "1";

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
            },
            Exception = exception
        });
    }
}
