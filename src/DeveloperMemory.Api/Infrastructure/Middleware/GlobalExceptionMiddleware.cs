using DeveloperMemory.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Infrastructure.Middleware;

/// <summary>
/// Global exception handler middleware. Catches unhandled exceptions and returns
/// OpenAI-compatible error responses for API endpoints, and standard problem details
/// for other endpoints.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // If the response has already started, we can't modify it
        if (context.Response.HasStarted)
        {
            return;
        }

        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        context.Response.ContentType = "application/json";
        var isV1Endpoint = context.Request.Path.StartsWithSegments("/v1");
        var statusCode = exception switch
        {
            TimeoutException => (int)HttpStatusCode.GatewayTimeout,
            TaskCanceledException => (int)HttpStatusCode.GatewayTimeout,
            HttpRequestException => (int)HttpStatusCode.BadGateway,
            _ => (int)HttpStatusCode.InternalServerError
        };
        var message = exception switch
        {
            TimeoutException or TaskCanceledException => "The request to the downstream provider timed out",
            HttpRequestException => "The downstream provider could not be reached",
            _ => "An internal server error occurred"
        };
        var errorType = exception switch
        {
            TimeoutException or TaskCanceledException => "timeout_error",
            HttpRequestException => "upstream_error",
            _ => "server_error"
        };

        context.Response.StatusCode = statusCode;

        if (isV1Endpoint)
        {
            await JsonSerializer.SerializeAsync(context.Response.Body, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = message,
                    Type = errorType
                }
            });
            return;
        }

        // Non-V1 endpoints also receive a generic response; internal exception details
        // remain available only through server-side diagnostics.
        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "Internal Server Error",
            status = statusCode,
            detail = message
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, problem);
    }
}
