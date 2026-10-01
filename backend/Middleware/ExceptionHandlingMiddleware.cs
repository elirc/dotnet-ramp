using System.Text.Json;
using ExpenseManagement.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Middleware;

/// <summary>
/// Translates domain exceptions into clean ProblemDetails responses, so services
/// can just throw and controllers stay free of error-mapping boilerplate.
/// Unexpected exceptions become a 500 without leaking internals.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblem(context, ex);
        }
    }

    private async Task WriteProblem(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business Rule Violation"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception.");

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : ex.Message
        };

        if (ex is DomainException dex && dex.Details.Count > 0)
            problem.Extensions["details"] = dex.Details;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
