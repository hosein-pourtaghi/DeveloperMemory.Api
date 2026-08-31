namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Persistent diagnostic log entry stored in PostgreSQL.
/// Captures HTTP request/response diagnostics for debugging and investigation.
///
/// Security: Never persist Authorization headers, API keys, bearer tokens,
/// passwords, cookies, or other secrets. Use an allowlist for safe fields.
/// </summary>
public class DiagnosticLogEntry : BaseEntity
{
    /// <summary>When the diagnostic event occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Log level (Information, Warning, Error, etc.).</summary>
    public string Level { get; set; } = "Information";

    /// <summary>Logger category (e.g., "RequestLogging", "GlobalException").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Type of event (Request, Exception, Intelligence, etc.).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Log message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Exception type name when applicable.</summary>
    public string? ExceptionType { get; set; }

    /// <summary>Exception message when applicable.</summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>Stack trace for exceptions (truncated to safe length).</summary>
    public string? StackTrace { get; set; }

    /// <summary>ASP.NET request TraceIdentifier for correlation.</summary>
    public string? RequestId { get; set; }

    /// <summary>OpenTelemetry trace ID if available.</summary>
    public string? TraceId { get; set; }

    /// <summary>HTTP method (GET, POST, etc.).</summary>
    public string? HttpMethod { get; set; }

    /// <summary>Request path (without query string).</summary>
    public string? RequestPath { get; set; }

    /// <summary>HTTP response status code.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Request duration in milliseconds.</summary>
    public double? DurationMs { get; set; }

    /// <summary>Authenticated user identifier when safely available.</summary>
    public string? UserId { get; set; }

    /// <summary>ASP.NET Core environment name.</summary>
    public string? Environment { get; set; }

    /// <summary>Additional structured metadata as JSON.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>Server-controlled owner identifier for authorization.</summary>
    public string OwnerId { get; set; } = string.Empty;
}
