using System.Diagnostics;
using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Api.Infrastructure.Middleware;

/// <summary>
/// Middleware that captures HTTP request/response diagnostics and persists them
/// to PostgreSQL when Diagnostics:PersistToDatabase is enabled.
///
/// Flow:
///   Request → timer → application pipeline → capture status/duration → persist
///
/// Security: Never persist Authorization headers, API keys, bearer tokens,
/// passwords, cookies, or other secrets.
///
/// Reliability: Diagnostic persistence failure must NEVER break the original request.
/// </summary>
public class DiagnosticLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DiagnosticsSettings _settings;
    private readonly ILogger<DiagnosticLoggingMiddleware> _logger;

    // Allowlist of safe headers to capture (none currently — keep empty for safety)
    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase);

    // Secret-like header patterns to explicitly exclude
    private static readonly string[] SecretHeaders =
    [
        "Authorization", "Cookie", "Set-Cookie", "X-Api-Key",
        "X-Auth-Token", "Proxy-Authorization"
    ];

    public DiagnosticLoggingMiddleware(
        RequestDelegate next,
        ILogger<DiagnosticLoggingMiddleware> logger,
        IOptions<DiagnosticsSettings> settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Fast path: if diagnostics are disabled, skip all overhead
        if (!_settings.PersistToDatabase)
        {
            await _next(context);
            return;
        }

        // Resolve scoped repository from request scope (middleware constructor
        // runs in root provider — scoped services cannot be injected there).
        var repository = context.RequestServices.GetService<IDiagnosticLogRepository>();
        if (repository == null)
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        Exception? capturedException = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw; // Re-throw — let GlobalExceptionMiddleware handle it
        }
        finally
        {
            sw.Stop();

            try
            {
                var entry = BuildLogEntry(context, sw.ElapsedMilliseconds, capturedException);
                // Fire-and-forget with explicit non-blocking
                _ = repository.TryLogAsync(entry);
            }
            catch (Exception ex)
            {
                // Diagnostic persistence failure must NEVER break the request
                _logger.LogDebug(ex, "Failed to queue diagnostic log entry (non-fatal)");
            }
        }
    }

    private static DiagnosticLogEntry BuildLogEntry(
        HttpContext context, double durationMs, Exception? exception)
    {
        var request = context.Request;
        var response = context.Response;

        var entry = new DiagnosticLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = exception != null ? "Error" : "Information",
            Category = "HttpRequest",
            EventType = exception != null ? "Exception" : "Request",
            Message = exception != null
                ? $"HTTP {request.Method} {request.Path} failed: {exception.Message}"
                : $"HTTP {request.Method} {request.Path} completed",
            HttpMethod = request.Method,
            RequestPath = request.Path.Value,
            StatusCode = response.StatusCode,
            DurationMs = durationMs,
            RequestId = request.HttpContext.TraceIdentifier,
            TraceId = Activity.Current?.TraceId.ToString(),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            StackTrace = exception?.StackTrace?.Length > 2000
                ? exception.StackTrace[..2000]
                : exception?.StackTrace
        };

        // Safely extract user identity from existing claims
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                entry.UserId = userId;
            }
        }

        return entry;
    }
}
