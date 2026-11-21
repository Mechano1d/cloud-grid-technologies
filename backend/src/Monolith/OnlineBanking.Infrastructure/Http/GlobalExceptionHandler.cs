using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace OnlineBanking.Infrastructure.Http;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path.Value ?? "/");

        var problemDetails = new ProblemDetails
        {
            Type = "https://httpstatuses.com/500",
            Title = "Internal server error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred while processing the request.",
            Instance = httpContext.Request.Path.Value
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string path);
}
