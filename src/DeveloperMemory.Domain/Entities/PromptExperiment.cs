namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents a prompt intelligence experiment.
/// Controls which optimization strategies are tested.
/// </summary>
public class PromptExperiment
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Experiment name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Experiment description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Associated profile ID.</summary>
    public Guid? ProfileId { get; set; }

    /// <summary>Experiment status.</summary>
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Draft;

    /// <summary>When the experiment starts (UTC).</summary>
    public DateTime? StartAt { get; set; }

    /// <summary>When the experiment ends (UTC).</summary>
    public DateTime? EndAt { get; set; }

    /// <summary>When this experiment was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Who created this experiment.</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>When this experiment was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Status of a prompt experiment.
/// </summary>
public enum ExperimentStatus
{
    Draft,
    Running,
    Paused,
    Completed,
    Cancelled
}

/// <summary>
/// Represents a variant within an experiment.
/// Each variant uses a different optimization strategy.
/// </summary>
public class PromptExperimentVariant
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Parent experiment ID.</summary>
    public Guid ExperimentId { get; set; }

    /// <summary>Variant name (e.g., "control", "llm-optimized").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Associated profile ID for this variant.</summary>
    public Guid? ProfileId { get; set; }

    /// <summary>Profile version used.</summary>
    public int? ProfileVersion { get; set; }

    /// <summary>Optimization mode for this variant.</summary>
    public string OptimizationMode { get; set; } = "Deterministic";

    /// <summary>Weight for assignment (0.0-1.0).</summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>Whether this variant is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When this variant was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Records a stable assignment of a key to a variant.
/// </summary>
public class PromptExperimentAssignment
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Experiment ID.</summary>
    public Guid ExperimentId { get; set; }

    /// <summary>Variant ID assigned.</summary>
    public Guid VariantId { get; set; }

    /// <summary>SHA-256 hash of the stable assignment key.</summary>
    public string AssignmentKeyHash { get; set; } = string.Empty;

    /// <summary>When this assignment was made.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Records the result of a processing operation within an experiment.
/// </summary>
public class PromptExperimentResult
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Experiment ID.</summary>
    public Guid ExperimentId { get; set; }

    /// <summary>Variant ID.</summary>
    public Guid VariantId { get; set; }

    /// <summary>Processing record ID.</summary>
    public Guid? ProcessingRecordId { get; set; }

    /// <summary>Quality score achieved (0.0-1.0).</summary>
    public double? QualityScore { get; set; }

    /// <summary>Whether quality gate passed.</summary>
    public bool QualityGatePassed { get; set; }

    /// <summary>Estimated input tokens.</summary>
    public int EstimatedInputTokens { get; set; }

    /// <summary>Estimated output tokens.</summary>
    public int EstimatedOutputTokens { get; set; }

    /// <summary>Processing duration in milliseconds.</summary>
    public double ProcessingDurationMs { get; set; }

    /// <summary>Whether fallback was used.</summary>
    public bool WasFallbackUsed { get; set; }

    /// <summary>Whether LLM was used.</summary>
    public bool WasLlmUsed { get; set; }

    /// <summary>When this result was recorded.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
