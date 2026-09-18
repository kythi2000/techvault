using TechVault.Api.Responses;
using TechVault.Application.Common.Results;
using TechVault.Infrastructure.Persistence;

namespace TechVault.Api.Middleware;

// Only the versioned JSON API uses these envelopes; health and OpenAPI keep their existing contracts.
public sealed class ApiErrorHandlingMiddleware(RequestDelegate next, ILogger<ApiErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        var traceId = ApiResponses.TraceId(context);
        context.Response.Headers["X-Trace-Id"] = traceId;
        try
        {
            await next(context);
            if (!context.Response.HasStarted && context.Response.StatusCode is 404 or 405)
            {
                var notFound = context.Response.StatusCode == 404;
                await WriteErrorAsync(context, context.Response.StatusCode,
                    notFound ? "NOT_FOUND" : "METHOD_NOT_ALLOWED",
                    notFound ? "Endpoint was not found." : "HTTP method is not supported.", traceId);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (BadHttpRequestException) when (!context.Response.HasStarted)
        {
            await WriteErrorAsync(context, 400, "VALIDATION_ERROR", "Invalid request parameters.", traceId);
        }
        catch (Exception exception) when (!context.Response.HasStarted &&
            context.Request.Path.StartsWithSegments("/api/v1/admin") && CatalogPersistenceErrors.Classify(exception) is { } error)
        {
            await WriteErrorAsync(context, error.Type == ErrorType.Conflict ? 409 : 400, error.Code, error.Message, traceId);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "Catalog request failed. TraceId: {TraceId}", traceId);
            await WriteErrorAsync(context, 500, "UNEXPECTED_ERROR", "An unexpected error occurred.", traceId);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int status, string code, string message, string traceId)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(new(code, message, traceId)), context.RequestAborted);
    }
}
