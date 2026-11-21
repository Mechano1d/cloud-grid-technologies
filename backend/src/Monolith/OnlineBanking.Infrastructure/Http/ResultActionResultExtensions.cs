using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineBanking.Shared.Results;

namespace OnlineBanking.Infrastructure.Http;

public static class ResultActionResultExtensions
{
    public static IActionResult ToActionResult(this Result result, HttpContext httpContext) =>
        result.IsSuccess ? new NoContentResult() : ToProblem(result.Error, httpContext);

    public static IActionResult ToActionResult<TValue>(this Result<TValue> result, HttpContext httpContext) =>
        result.IsSuccess ? new OkObjectResult(result.Value) : ToProblem(result.Error, httpContext);

    public static IActionResult ToCreatedResult<TValue>(
        this Result<TValue> result,
        HttpContext httpContext,
        string location) =>
        result.IsSuccess
            ? new CreatedResult(location, result.Value)
            : ToProblem(result.Error, httpContext);

    public static IActionResult ToProblem(DomainError error, HttpContext httpContext)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Business rule conflict"),
            _ => (StatusCodes.Status400BadRequest, "Bad request")
        };

        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = error.Message,
            Instance = httpContext.Request.Path.Value
        };

        problemDetails.Extensions["code"] = error.Code;

        var objectResult = new ObjectResult(problemDetails)
        {
            StatusCode = statusCode
        };
        objectResult.ContentTypes.Add("application/problem+json");
        return objectResult;
    }
}
