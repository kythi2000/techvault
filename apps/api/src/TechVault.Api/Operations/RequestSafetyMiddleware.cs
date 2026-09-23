using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using TechVault.Api.Responses;

namespace TechVault.Api.Operations;

public sealed class RequestSafetyMiddleware(RequestDelegate next, IOptions<OperationalSettings> settings)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.Remove("Server");
            if (context.Request.IsHttps) headers.StrictTransportSecurity = "max-age=31536000";
            if (context.Request.Path.StartsWithSegments("/api/v1/admin")) headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        var health = context.Request.Path == "/health/live" || context.Request.Path == "/health/ready";
        if (!health && settings.Value.HttpsOrigin is { } origin && !context.Request.IsHttps)
        {
            if (context.Request.Method is "GET" or "HEAD" && !context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
                context.Response.Headers.Location = origin + context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            }
            else await Error(context, 400, "HTTPS_REQUIRED", "Use HTTPS for this request.");
            return;
        }
        var limit = settings.Value.MaxRequestBodyBytes;
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit) bodyLimit.MaxRequestBodySize = limit;
        if (context.Request.ContentLength > limit)
        {
            await Error(context, 413, "PAYLOAD_TOO_LARGE", "Request body exceeds the configured limit.");
            return;
        }
        // Also cap unknown-length/chunked bodies in TestServer and alternate hosts, without buffering.
        var original = context.Request.Body;
        context.Request.Body = new LimitedRequestStream(original, limit);
        try { await next(context); }
        finally { context.Request.Body = original; }
    }

    private static Task Error(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(new(code, message, ApiResponses.TraceId(context))), context.RequestAborted);
    }
}

// A read-only wrapper enforcing the same limit even when Content-Length is absent or incorrect.
internal sealed class LimitedRequestStream(Stream inner, long limit) : Stream
{
    private long _read;
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    private int Count(int count)
    {
        _read += count;
        if (_read > limit) throw new BadHttpRequestException("Request body limit exceeded.", 413);
        return count;
    }
    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, (int)Math.Min(count, limit - _read + 1)));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
        Count(await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, limit - _read + 1)], ct));
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
