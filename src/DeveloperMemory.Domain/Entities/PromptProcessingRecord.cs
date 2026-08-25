namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Persistent record of a Prompt Intelligence operation.
/// Captures metadata for traceability without storing sensitive content.
/// </summary>
public class PromptProcessingRecord
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Correlation ID for tracing across the pipeline.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>When this record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The profile used for this processing.</summary>
    public Guid? ProfileId { get; set; }

    /// <summary>The profile version used.</summary>
    public int? ProfileVersion { get; set; }

    /// <summary>Detected intent type.</summary>
    public string Intent { get; set; } = string.Empty;

    /// <summary>Detected task type.</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>Optimization mode used.</summary>
    public string OptimizationMode { get; set; } = string.Empty;

    /// <summary>Which optimizer was used.</summary>
    public string Optimizer { get; set; } = string.Empty;

    /// <summary>Optimizer version.</summary>
    public string? OptimizerVersion { get; set; }

    /// <summary>LLM model used (if any).</summary>
    public string? Model { get; set; }

    /// <summary>Whether LLM was used.</summary>
    public bool WasLlmUsed { get; set; }

    /// <summary>Whether deterministic fallback was used.</summary>
    public bool WasFallbackUsed { get; set; }

    /// <summary>Token budget for this request.</summary>
    public int TokenBudget { get; set; }

    /// <summary>Estimated input tokens.</summary>
    public int EstimatedInputTokens { get; set; }

    /// <summary>Estimated output tokens.</summary>
    public int EstimatedOutputTokens { get; set; }

    /// <summary>Quality score (0.0-1.0).</summary>
    public double? QualityScore { get; set; }

    /// <summary>Validation status.</summary>
    public string ValidationStatus { get; set; } = string.Empty;

    /// <summary>Processing duration in milliseconds.</summary>
    public double ProcessingDurationMs { get; set; }

    /// <summary>Experiment ID for A/B testing (nullable).</summary>
    public string? ExperimentId { get; set; }

    /// <summary>Variant ID for A/B testing (nullable).</summary>
    public string? VariantId { get; set; }

    /// <summary>Memory IDs used in context.</summary>
    public string MemoryIdsUsed { get; set; } = "[]";

    /// <summary>Project context identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Workspace identifier.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>User identifier.</summary>
    public string? UserId { get; set; }

    /// <summary>Number of memories selected.</summary>
    public int MemoryCount { get; set; }

    /// <summary>Number of conflicts detected.</summary>
    public int ConflictsDetected { get; set; }

    /// <summary>Whether quality gate passed.</summary>
    public bool QualityGatePassed { get; set; } = true;

    /// <summary>Quality gate failure reason (if any).</summary>
    public string? QualityGateFailureReason { get; set; }
}
