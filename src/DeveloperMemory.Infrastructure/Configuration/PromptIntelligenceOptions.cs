namespace DeveloperMemory.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for Prompt Intelligence features.
/// </summary>
public class PromptIntelligenceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PromptIntelligence";

    /// <summary>Whether persistence is enabled.</summary>
    public bool PersistenceEnabled { get; set; } = true;

    /// <summary>Prompt logging mode: None, MetadataOnly, Redacted, Full.</summary>
    public string PromptLoggingMode { get; set; } = "MetadataOnly";

    /// <summary>Whether quality evaluation is enabled.</summary>
    public bool QualityEvaluationEnabled { get; set; } = true;

    /// <summary>Quality evaluation mode: Deterministic, LLM, Hybrid, Auto.</summary>
    public string QualityEvaluationMode { get; set; } = "Auto";

    /// <summary>Minimum overall quality score threshold.</summary>
    public double MinimumQualityScore { get; set; } = 0.70;

    /// <summary>Minimum constraint preservation score.</summary>
    public double MinimumConstraintPreservation { get; set; } = 0.90;

    /// <summary>Minimum security score.</summary>
    public double MinimumSecurityScore { get; set; } = 0.90;

    /// <summary>Whether experiments/A-B testing is enabled.</summary>
    public bool ExperimentsEnabled { get; set; } = true;

    /// <summary>Whether metrics collection is enabled.</summary>
    public bool MetricsEnabled { get; set; } = true;

    /// <summary>History retention configuration.</summary>
    public HistoryRetentionConfig HistoryRetention { get; set; } = new();

    /// <summary>LLM quality evaluation configuration.</summary>
    public LlmEvaluationConfig LlmEvaluation { get; set; } = new();
}

/// <summary>
/// Configuration for history retention background service.
/// </summary>
public class HistoryRetentionConfig
{
    /// <summary>Whether background retention is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often to run cleanup (minutes).</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Maximum age of history records (days).</summary>
    public int MaxHistoryRetentionDays { get; set; } = 30;

    /// <summary>Batch size for cleanup operations.</summary>
    public int BatchSize { get; set; } = 500;
}

/// <summary>
/// Configuration for LLM quality evaluation.
/// </summary>
public class LlmEvaluationConfig
{
    /// <summary>Whether LLM evaluation is enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>LLM provider for evaluation.</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>LLM model for evaluation.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Timeout for LLM evaluation (seconds).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Whether the LLM evaluation provider is available.</summary>
    public bool IsAvailable => Enabled && !string.IsNullOrEmpty(Provider) && !string.IsNullOrEmpty(Model);
}
