using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent analytics service for prompt experiments.
/// Computes aggregate metrics from persisted experiment results.
/// </summary>
public interface IExperimentAnalyticsService
{
    /// <summary>Gets aggregate analytics for an experiment.</summary>
    Task<ExperimentAnalyticsResult> GetExperimentAnalyticsAsync(
        Guid experimentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    /// <summary>Gets analytics broken down by variant.</summary>
    Task<IReadOnlyList<VariantAnalyticsResult>> GetVariantAnalyticsAsync(
        Guid experimentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}

/// <summary>
/// Aggregate analytics for an experiment.
/// </summary>
public class ExperimentAnalyticsResult
{
    public Guid ExperimentId { get; set; }
    public long TotalResults { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public double FallbackRate { get; set; }
    public double LlmUsageRate { get; set; }
    public double QualityGatePassRate { get; set; }
    public double AverageQualityScore { get; set; }
    public double AverageInputTokens { get; set; }
    public double AverageOutputTokens { get; set; }
    public double AverageProcessingLatencyMs { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

/// <summary>
/// Analytics for a single variant.
/// </summary>
public class VariantAnalyticsResult
{
    public Guid VariantId { get; set; }
    public string VariantName { get; set; } = string.Empty;
    public long ResultCount { get; set; }
    public double AverageQualityScore { get; set; }
    public double FallbackRate { get; set; }
    public double LlmUsageRate { get; set; }
    public double QualityGatePassRate { get; set; }
    public double AverageInputTokens { get; set; }
    public double AverageOutputTokens { get; set; }
    public double AverageProcessingLatencyMs { get; set; }
}
