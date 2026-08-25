namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Audit event for Prompt Intelligence operations.
/// Records significant events without sensitive content.
/// </summary>
public class PromptAuditEvent
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Correlation ID for tracing.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>When this event occurred.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The event type.</summary>
    public PromptAuditEventType EventType { get; set; }

    /// <summary>Processing record ID (if applicable).</summary>
    public Guid? ProcessingRecordId { get; set; }

    /// <summary>Profile ID (if applicable).</summary>
    public Guid? ProfileId { get; set; }

    /// <summary>Event details (safe metadata only).</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>User identifier.</summary>
    public string? UserId { get; set; }
}

/// <summary>
/// Types of audit events.
/// </summary>
public enum PromptAuditEventType
{
    PromptAnalyzed,
    IntentResolved,
    MemoryContextSelected,
    ProfileSelected,
    ProfileVersionCreated,
    ProfileRollback,
    PromptOptimized,
    OptimizationRejected,
    FallbackActivated,
    PromptValidationFailed,
    QualityGateFailed,
    QualityGatePassed,
    ProcessingRecordCreated
}
