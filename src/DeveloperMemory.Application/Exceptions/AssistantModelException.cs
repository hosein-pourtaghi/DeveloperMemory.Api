namespace DeveloperMemory.Application.Exceptions;

/// <summary>
/// Typed model/provider execution failure surfaced to the Assistant boundary.
///
/// Carries a client-safe message, a stable error code, and an HTTP status code
/// so the API layer can map model failures without leaking provider secrets,
/// credentials, or internal stack traces.
///
/// Categories (via ErrorCode):
///   "model_not_configured"  → 503 Service Unavailable
///   "model_timeout"         → 504 Gateway Timeout
///   "model_rate_limited"    → 429 Too Many Requests
///   "model_upstream_error"  → 502 Bad Gateway
/// </summary>
public class AssistantModelException : Exception
{
    /// <summary>Stable, client-safe error code.</summary>
    public string ErrorCode { get; }

    /// <summary>HTTP status code appropriate for this failure.</summary>
    public int StatusCode { get; }

    public AssistantModelException(string message, string errorCode, int statusCode)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}