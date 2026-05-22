using System.Text.Json;
using Pasukhi.Application.Exceptions;

namespace Pasukhi.API.Middleware;

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
        catch (PlanLimitExceededException ex)
        {
            _logger.LogInformation(
                "Plan limit exceeded: resource={Resource} tier={Tier} limit={Limit} suggested={Suggested}",
                ex.Resource, ex.CurrentTier, ex.Limit, ex.SuggestedTier);
            context.Response.StatusCode = 402;
            context.Response.ContentType = "application/json";
            var body = JsonSerializer.Serialize(new
            {
                error = "plan_limit_exceeded",
                resource = ex.Resource,
                limit = ex.Limit,
                currentTier = ex.CurrentTier.ToString(),
                suggestedTier = ex.SuggestedTier.ToString()
            });
            await context.Response.WriteAsync(body);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access");
            await WriteErrorAsync(context, 401, "Unauthorized.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Resource not found");
            await WriteErrorAsync(context, 404, "Resource not found.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation");
            await WriteErrorAsync(context, 400, "The request could not be completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, 500, "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(body);
    }
}
