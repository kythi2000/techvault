using System.Diagnostics;
using System.Security.Claims;
using TechVault.Api.Responses;

namespace TechVault.Api.Operations;

public sealed class RequestLogMiddleware(RequestDelegate next, ILogger<RequestLogMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var traceId = ApiResponses.TraceId(context);
        context.Response.Headers["X-Trace-Id"] = traceId;
        try { await next(context); }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            var method = context.Request.Method is "GET" or "HEAD" or "POST" or "PUT" or "DELETE" or "OPTIONS" or "PATCH" ? context.Request.Method : "OTHER";
            logger.LogInformation("HTTP {Method} {RequestPath} responded {StatusCode} in {DurationMs} ms. TraceId: {TraceId}, AdminUserId: {AdminUserId}",
                method, route, context.RequestAborted.IsCancellationRequested ? 499 : context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, traceId,
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) == "catalog-admin" ? "catalog-admin" : null);
        }
    }
}
