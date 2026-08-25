namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent metrics abstraction for Prompt Intelligence.
/// Future implementations can use OpenTelemetry, Prometheus, Application Insights, etc.
/// </summary>
public interface IPromptIntelligenceMetrics
{
    /// <summary>
    /// Records a processing request metric.
    /// </summary>
    void RecordProcessingRequest(PromptProcessingMetric metric);

    /// <summary>
    /// Records a quality evaluation metric.
    /// </summary>
    void RecordQualityEvaluation(QualityEvaluationMetric metric);

    /// <summary>
    /// Records an experiment result metric.
    /// </summary>
    void RecordExperimentResult(ExperimentResultMetric metric);

    /// <summary>
    /// Gets aggregated metrics for a time range.
    /// </summary>
    PromptMetricsSummary GetSummary(DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Gets metrics for a specific experiment.
    /// </summary>
    ExperimentMetrics GetExperimentMetrics(Guid experimentId);
}

/// <summary>
/// Metric for a single processing request.
/// </summary>
public class PromptProcessingMetric
{
    public string Intent { get; set; } = string.Empty;
    public string OptimizationMode { get; set; } = string.Empty;
    public bool WasLlmUsed { get; set; }
    public bool WasFallbackUsed { get; set; }
    public double QualityScore { get; set; }
    public bool QualityGatePassed { get; set; }
    public double ProcessingDurationMs { get; set; }
    public double IntentDurationMs { get; set; }
    public double ContextDurationMs { get; set; }
    public double OptimizationDurationMs { get; set; }
    public double EvaluationDurationMs { get; set; }
    public int EstimatedInputTokens { get; set; }
    public int EstimatedOutputTokens { get; set; }
    public int MemoryCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metric for a quality evaluation.
/// </summary>
public class QualityEvaluationMetric
{
    public string EvaluatorUsed { get; set; } = string.Empty;
    public bool LlmUsed { get; set; }
    public bool FallbackUsed { get; set; }
    public double QualityScore { get; set; }
    public double IntentPreservation { get; set; }
    public double ConstraintPreservation { get; set; }
    public double SecurityScore { get; set; }
    public double EvaluationDurationMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metric for an experiment result.
/// </summary>
public class ExperimentResultMetric
{
    public Guid ExperimentId { get; set; }
    public Guid VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public double? QualityScore { get; set; }
    public bool QualityGatePassed { get; set; }
    public double ProcessingDurationMs { get; set; }
    public bool WasFallbackUsed { get; set; }
    public bool WasLlmUsed { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Aggregated metrics summary.
/// </summary>
public class PromptMetricsSummary
{
    // Processing
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public long FallbackCount { get; set; }
    public long LlmUsageCount { get; set; }
    public long DeterministicCount { get; set; }

    // Latency
    public double AverageProcessingDurationMs { get; set; }
    public double AverageIntentDurationMs { get; set; }
    public double AverageOptimizationDurationMs { get; set; }
    public double AverageEvaluationDurationMs { get; set; }

    // Quality
    public double AverageQualityScore { get; set; }
    public double QualityGatePassRate { get; set; }
    public double AverageConstraintPreservation { get; set; }
    public double AverageSecurityScore { get; set; }
    public double OptimizationImprovementRate { get; set; }

    // Token
    public long TotalEstimatedInputTokens { get; set; }
    public long TotalEstimatedOutputTokens { get; set; }
    public double AverageTokensPerRequest { get; set; }

    // Period
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

/// <summary>
/// Metrics for a specific experiment.
/// </summary>
public class ExperimentMetrics
{
    public Guid ExperimentId { get; set; }
    public string ExperimentName { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public List<VariantMetrics> Variants { get; set; } = [];
}

/// <summary>
/// Metrics for a specific experiment variant.
/// </summary>
public class VariantMetrics
{
    public Guid VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public double AverageQualityScore { get; set; }
    public double AverageProcessingDurationMs { get; set; }
    public double FallbackRate { get; set; }
    public double QualityGatePassRate { get; set; }
}
