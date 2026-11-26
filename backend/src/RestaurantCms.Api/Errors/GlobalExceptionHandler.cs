using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace RestaurantCms.Api.Errors;

/// <summary>Last line of defence: logs the exception and returns a problem response without leaking internals.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Someone else changed this at the same time. Refresh and try again."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request body is not valid."),
            OperationCanceledException => (499, "The request was cancelled."),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong on our side.")
        };
        if (status >= 500) logger.LogError(exception, "Unhandled exception for {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title }
        });
    }
}
