using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Infrastructure.Middleware;

/// <summary>
/// Request logging middleware for /v1/* endpoints.
/// Logs method, path, status code, and duration for all requests.
/// Body logging is disabled by default and must be explicitly enabled
/// via configuration: "RequestLogging:LogBodies": true.
///
/// WARNING: Body logging exposes potentially sensitive prompt and memory data.
/// Only enable in development or when actively debugging.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly bool _logBodies;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _logBodies = configuration.GetValue<bool>("RequestLogging:LogBodies");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var path = context.Request.Path;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (_logBodies && HttpMethods.IsPost(context.Request.Method))
        {
            // Only read body if explicitly enabled
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body, encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 8192, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reset for the controller

            _logger.LogDebug(
                "[V1 Request] {Method} {Path} | Content-Type: {ContentType} | Body: {Body}",
                method, path,
                context.Request.ContentType,
                body.Length > 500 ? body[..500] + "...(truncated)" : body);
        }

        await _next(context);

        sw.Stop();
        _logger.LogInformation(
            "[V1 Request] {Method} {Path} | {StatusCode} | {DurationMs}ms",
            method, path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}
