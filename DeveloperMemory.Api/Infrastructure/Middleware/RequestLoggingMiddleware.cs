using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Infrastructure.Middleware;

/// <summary>
/// Temporary diagnostic middleware that logs incoming request bodies for /v1/* endpoints.
/// Helps debug client compatibility issues (e.g., Cline sending unexpected formats).
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only log /v1/* POST requests (chat completions)
        if (context.Request.Path.StartsWithSegments("/v1") &&
            HttpMethods.IsPost(context.Request.Method))
        {
            // Enable buffering so the body can be read multiple times
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reset for the controller

            _logger.LogWarning("=== INCOMING REQUEST === {Method} {Path}", context.Request.Method, context.Request.Path);
            _logger.LogWarning("Content-Type: {ContentType}", context.Request.ContentType);
            _logger.LogWarning("Body: {Body}", body.Length > 2000 ? body[..2000] + "...(truncated)" : body);
        }

        await _next(context);
    }
}
