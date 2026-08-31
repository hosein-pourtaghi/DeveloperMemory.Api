using System.Diagnostics.Metrics;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// OpenTelemetry-based implementation of IPromptIntelligenceMetrics.
/// Uses System.Diagnostics.Metrics for provider-independent instrumentation.
/// Falls back gracefully when instruments are unavailable.
/// </summary>
public class OpenTelemetryPromptMetrics : IPromptIntelligenceMetrics, IDisposable
{
    private const string MeterName = "DeveloperMemory.PromptIntelligence";

    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _requestsTotal;
    private readonly Counter<long> _successTotal;
    private readonly Counter<long> _failureTotal;
    private readonly Counter<long> _fallbackTotal;
    private readonly Counter<long> _llmUsageTotal;
    private readonly Counter<long> _qualityGatePassTotal;
    private readonly Counter<long> _qualityGateFailureTotal;
    private readonly Counter<long> _experimentAssignmentsTotal;
    private readonly Counter<long> _experimentResultsTotal;

    // Histograms
    private readonly Histogram<double> _processingDuration;
    private readonly Histogram<double> _intentDuration;
    private readonly Histogram<double> _contextDuration;
    private readonly Histogram<double> _optimizationDuration;
    private readonly Histogram<double> _evaluationDuration;
    private readonly Histogram<double> _qualityScore;
    private readonly Histogram<double> _inputTokens;
    private readonly Histogram<double> _outputTokens;

    // In-memory fallback for summary queries
    private readonly InMemoryPromptMetrics _fallbackMetrics;

    public OpenTelemetryPromptMetrics(InMemoryPromptMetrics fallbackMetrics)
    {
        _fallbackMetrics = fallbackMetrics;

        _meter = new Meter(MeterName);

        _requestsTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_requests_total",
            description: "Total prompt intelligence processing requests");

        _successTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_success_total",
            description: "Successful prompt intelligence requests");

        _failureTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_failure_total",
            description: "Failed prompt intelligence requests");

        _fallbackTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_fallback_total",
            description: "Fallback usage count");

        _llmUsageTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_llm_usage_total",
            description: "LLM usage count");

        _qualityGatePassTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_quality_gate_pass_total",
            description: "Quality gate pass count");

        _qualityGateFailureTotal = _meter.CreateCounter<long>(
            "prompt_intelligence_quality_gate_failure_total",
            description: "Quality gate failure count");

        _experimentAssignmentsTotal = _meter.CreateCounter<long>(
            "experiment_assignments_total",
            description: "Total experiment assignments");

        _experimentResultsTotal = _meter.CreateCounter<long>(
            "experiment_results_total",
            description: "Total experiment results recorded");

        _processingDuration = _meter.CreateHistogram<double>(
            "prompt_intelligence_processing_duration",
            unit: "ms",
            description: "Processing request duration");

        _intentDuration = _meter.CreateHistogram<double>(
            "prompt_intelligence_intent_duration",
            unit: "ms",
            description: "Intent analysis duration");

        _contextDuration = _meter.CreateHistogram<double>(
            "prompt_intelligence_context_duration",
            unit: "ms",
            description: "Context retrieval duration");

        _optimizationDuration = _meter.CreateHistogram<double>(
            "prompt_intelligence_optimization_duration",
            unit: "ms",
            description: "Prompt optimization duration");

        _evaluationDuration = _meter.CreateHistogram<double>(
            "prompt_intelligence_evaluation_duration",
            unit: "ms",
            description: "Quality evaluation duration");

        _qualityScore = _meter.CreateHistogram<double>(
            "prompt_intelligence_quality_score",
            description: "Quality score distribution");

        _inputTokens = _meter.CreateHistogram<double>(
            "prompt_intelligence_input_tokens",
            description: "Estimated input tokens");

        _outputTokens = _meter.CreateHistogram<double>(
            "prompt_intelligence_output_tokens",
            description: "Estimated output tags");
    }

    public void RecordProcessingRequest(PromptProcessingMetric metric)
    {
        // Record to in-memory fallback for summary queries
        _fallbackMetrics.RecordProcessingRequest(metric);

        // Emit OpenTelemetry instruments with low-cardinality tags
        var tags = new KeyValuePair<string, object?>[]
        {
            new("intent", metric.Intent),
            new("optimization_mode", metric.OptimizationMode),
            new("llm_used", metric.WasLlmUsed.ToString()),
            new("fallback", metric.WasFallbackUsed.ToString())
        };

        _requestsTotal.Add(1, tags);

        if (metric.QualityGatePassed)
            _successTotal.Add(1, tags);
        else
            _failureTotal.Add(1, tags);

        if (metric.WasFallbackUsed)
            _fallbackTotal.Add(1, tags);

        if (metric.WasLlmUsed)
            _llmUsageTotal.Add(1, tags);

        if (metric.QualityGatePassed)
            _qualityGatePassTotal.Add(1, tags);
        else
            _qualityGateFailureTotal.Add(1, tags);

        _processingDuration.Record(metric.ProcessingDurationMs, tags);
        _intentDuration.Record(metric.IntentDurationMs, tags);
        _optimizationDuration.Record(metric.OptimizationDurationMs, tags);
        _evaluationDuration.Record(metric.EvaluationDurationMs, tags);
        _qualityScore.Record(metric.QualityScore, tags);
        _inputTokens.Record(metric.EstimatedInputTokens, tags);
        _outputTokens.Record(metric.EstimatedOutputTokens, tags);
    }

    public void RecordQualityEvaluation(QualityEvaluationMetric metric)
    {
        _fallbackMetrics.RecordQualityEvaluation(metric);
        // Quality evaluations are emitted as processing-level histograms
    }

    public void RecordExperimentResult(ExperimentResultMetric metric)
    {
        _fallbackMetrics.RecordExperimentResult(metric);

        var tags = new KeyValuePair<string, object?>[]
        {
            new("experiment_id", metric.ExperimentId.ToString()),
            new("variant_id", metric.VariantId.ToString()),
            new("quality_gate_passed", metric.QualityGatePassed.ToString())
        };

        _experimentResultsTotal.Add(1, tags);
    }

    public PromptMetricsSummary GetSummary(DateTime? from = null, DateTime? to = null)
    {
        // Delegate to in-memory fallback for aggregation
        return _fallbackMetrics.GetSummary(from, to);
    }

    public ExperimentMetrics GetExperimentMetrics(Guid experimentId)
    {
        return _fallbackMetrics.GetExperimentMetrics(experimentId);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
