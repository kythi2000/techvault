using TechVault.Api.Responses;
using TechVault.Application.Common.Results;
using TechVault.Infrastructure.Persistence;

namespace TechVault.Api.Middleware;

// Only the versioned JSON API uses these envelopes; health and OpenAPI keep their existing contracts.
public sealed class ApiErrorHandlingMiddleware(RequestDelegate next, ILogger<ApiErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = ApiResponses.TraceId(context);
        context.Response.Headers["X-Trace-Id"] = traceId;
        try
        {
            await next(context);
            if (context.Request.Path.StartsWithSegments("/api/v1") && !context.Response.HasStarted && context.Response.StatusCode is 404 or 405)
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
        catch (BadHttpRequestException exception) when (!context.Response.HasStarted)
        {
            var tooLarge = exception.StatusCode == 413;
            await WriteErrorAsync(context, tooLarge ? 413 : 400, tooLarge ? "PAYLOAD_TOO_LARGE" : "VALIDATION_ERROR",
                tooLarge ? "Request body exceeds the configured limit." : "Invalid request parameters.", traceId);
        }
        catch (Exception exception) when (!context.Response.HasStarted &&
            context.Request.Path.StartsWithSegments("/api/v1/admin") && CatalogPersistenceErrors.Classify(exception) is { } error)
        {
            await WriteErrorAsync(context, error.Type == ErrorType.Conflict ? 409 : 400, error.Code, error.Message, traceId);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            // Exception messages/inner exceptions can contain connection strings, SQL, or request content.
            logger.LogError("Request failed. ErrorType: {ErrorType}, FailureSite: {FailureSite}, TraceId: {TraceId}",
                exception.GetType().FullName, exception.TargetSite?.DeclaringType?.FullName, traceId);
            await WriteErrorAsync(context, 500, "UNEXPECTED_ERROR", "An unexpected error occurred.", traceId);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int status, string code, string message, string traceId)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(new(code, message, traceId)), context.RequestAborted);
    }
}
