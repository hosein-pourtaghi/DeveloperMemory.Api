namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Persistent security audit log entry. Append-only, never modified.
/// Does not store raw API keys, passwords, tokens, or other sensitive authentication material.
/// </summary>
public class SecurityAuditLogEntry : BaseEntity
{
    /// <summary>When the event occurred.</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Category of security event (Authentication, KeyLifecycle, Authorization, RateLimit, etc.).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Outcome of the event (Success, Failure, Denied).</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Owner/user ID when known (nullable for anonymous events).</summary>
    public string? OwnerId { get; set; }

    /// <summary>API key identifier when applicable (not the raw key).</summary>
    public string? KeyId { get; set; }

    /// <summary>Correlation/request ID for tracing (nullable).</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Source IP address when available (nullable).</summary>
    public string? SourceIp { get; set; }

    /// <summary>Human-readable failure reason code (nullable).</summary>
    public string? FailureReason { get; set; }

    /// <summary>Structured metadata as JSON (nullable). Must not contain raw secrets.</summary>
    public string? MetadataJson { get; set; }
}
