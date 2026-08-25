using System.Collections.Concurrent;
using DeveloperMemory.Application.Contracts;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// In-memory metrics collection for Prompt Intelligence.
/// Thread-safe implementation for testing and single-instance deployment.
/// Future implementations can use OpenTelemetry, Prometheus, etc.
/// </summary>
public class InMemoryPromptMetrics : IPromptIntelligenceMetrics
{
    private readonly ConcurrentBag<PromptProcessingMetric> _processingMetrics = [];
    private readonly ConcurrentBag<QualityEvaluationMetric> _evaluationMetrics = [];
    private readonly ConcurrentBag<ExperimentResultMetric> _experimentMetrics = [];
    private readonly object _lock = new();

    public void RecordProcessingRequest(PromptProcessingMetric metric)
    {
        _processingMetrics.Add(metric);
    }

    public void RecordQualityEvaluation(QualityEvaluationMetric metric)
    {
        _evaluationMetrics.Add(metric);
    }

    public void RecordExperimentResult(ExperimentResultMetric metric)
    {
        _experimentMetrics.Add(metric);
    }

    public PromptMetricsSummary GetSummary(DateTime? from = null, DateTime? to = null)
    {
        var processing = _processingMetrics
            .Where(m => (!from.HasValue || m.Timestamp >= from.Value) &&
                        (!to.HasValue || m.Timestamp <= to.Value))
            .ToList();

        var evaluations = _evaluationMetrics
            .Where(m => (!from.HasValue || m.Timestamp >= from.Value) &&
                        (!to.HasValue || m.Timestamp <= to.Value))
            .ToList();

        var summary = new PromptMetricsSummary
        {
            TotalRequests = processing.Count,
            SuccessfulRequests = processing.Count(m => m.QualityGatePassed),
            FailedRequests = processing.Count(m => !m.QualityGatePassed),
            FallbackCount = processing.Count(m => m.WasFallbackUsed),
            LlmUsageCount = processing.Count(m => m.WasLlmUsed),
            DeterministicCount = processing.Count(m => !m.WasLlmUsed),

            AverageProcessingDurationMs = processing.Count > 0
                ? processing.Average(m => m.ProcessingDurationMs) : 0,
            AverageIntentDurationMs = processing.Count > 0
                ? processing.Average(m => m.IntentDurationMs) : 0,
            AverageOptimizationDurationMs = processing.Count > 0
                ? processing.Average(m => m.OptimizationDurationMs) : 0,
            AverageEvaluationDurationMs = evaluations.Count > 0
                ? evaluations.Average(m => m.EvaluationDurationMs) : 0,

            AverageQualityScore = processing.Count > 0
                ? processing.Average(m => m.QualityScore) : 0,
            QualityGatePassRate = processing.Count > 0
                ? (double)processing.Count(m => m.QualityGatePassed) / processing.Count : 0,
            AverageConstraintPreservation = evaluations.Count > 0
                ? evaluations.Average(m => m.ConstraintPreservation) : 0,
            AverageSecurityScore = evaluations.Count > 0
                ? evaluations.Average(m => m.SecurityScore) : 0,

            TotalEstimatedInputTokens = processing.Sum(m => m.EstimatedInputTokens),
            TotalEstimatedOutputTokens = processing.Sum(m => m.EstimatedOutputTokens),
            AverageTokensPerRequest = processing.Count > 0
                ? processing.Average(m => m.EstimatedInputTokens + m.EstimatedOutputTokens) : 0,

            From = from ?? DateTime.MinValue,
            To = to ?? DateTime.UtcNow
        };

        return summary;
    }

    public ExperimentMetrics GetExperimentMetrics(Guid experimentId)
    {
        var results = _experimentMetrics
            .Where(m => m.ExperimentId == experimentId)
            .ToList();

        var variantGroups = results.GroupBy(r => r.VariantId);

        return new ExperimentMetrics
        {
            ExperimentId = experimentId,
            TotalRequests = results.Count,
            Variants = variantGroups.Select(g => new VariantMetrics
            {
                VariantId = g.Key,
                VariantName = g.First().VariantName,
                RequestCount = g.Count(),
                AverageQualityScore = g.Average(m => m.QualityScore ?? 0),
                AverageProcessingDurationMs = g.Average(m => m.ProcessingDurationMs),
                FallbackRate = (double)g.Count(m => m.WasFallbackUsed) / g.Count(),
                QualityGatePassRate = (double)g.Count(m => m.QualityGatePassed) / g.Count()
            }).ToList()
        };
    }
}
