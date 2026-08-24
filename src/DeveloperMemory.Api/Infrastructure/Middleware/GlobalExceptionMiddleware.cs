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

        context.Response.ContentType = "application/json";

        var isV1Endpoint = context.Request.Path.StartsWithSegments("/v1");

        if (isV1Endpoint)
        {
            // OpenAI-compatible error response for /v1/* endpoints
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var errorResponse = new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = exception switch
                    {
                        InvalidOperationException ex => ex.Message,
                        TimeoutException => "The request to the downstream provider timed out",
                        OperationCanceledException => "The request was cancelled",
                        _ => "An internal server error occurred"
                    },
                    Type = exception switch
                    {
                        InvalidOperationException => "invalid_request_error",
                        TimeoutException => "timeout_error",
                        OperationCanceledException => "request_cancelled",
                        _ => "server_error"
                    }
                }
            };

            await JsonSerializer.SerializeAsync(context.Response.Body, errorResponse, JsonOptions);
        }
        else
        {
            // Standard ASP.NET problem details for non-V1 endpoints
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Internal Server Error",
                status = 500,
                detail = exception.Message
            };

            await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
        }
    }
}
