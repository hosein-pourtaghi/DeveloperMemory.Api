using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// Phase 12/13: Prompt Intelligence Evaluation, Experimentation & Observability.
/// Provides quality evaluation, comparison, metrics, experiment management, analytics, and statistics.
/// </summary>
[ApiController]
[Route("api/prompt")]
public class PromptEvaluationController : ControllerBase
{
    private readonly HybridQualityEvaluationPipeline _evaluationPipeline;
    private readonly IPromptCandidateSelector _candidateSelector;
    private readonly IExperimentService _experimentService;
    private readonly IExperimentAnalyticsService _analyticsService;
    private readonly IExperimentStatisticsAnalyzer _statisticsAnalyzer;
    private readonly IPromptIntelligenceMetrics _metrics;
    private readonly IPromptQualityEvaluator _deterministicEvaluator;
    private readonly ILogger<PromptEvaluationController> _logger;

    public PromptEvaluationController(
        HybridQualityEvaluationPipeline evaluationPipeline,
        IPromptCandidateSelector candidateSelector,
        IExperimentService experimentService,
        IExperimentAnalyticsService analyticsService,
        IExperimentStatisticsAnalyzer statisticsAnalyzer,
        IPromptIntelligenceMetrics metrics,
        IPromptQualityEvaluator deterministicEvaluator,
        ILogger<PromptEvaluationController> logger)
    {
        _evaluationPipeline = evaluationPipeline;
        _candidateSelector = candidateSelector;
        _experimentService = experimentService;
        _analyticsService = analyticsService;
        _statisticsAnalyzer = statisticsAnalyzer;
        _metrics = metrics;
        _deterministicEvaluator = deterministicEvaluator;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // QUALITY EVALUATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Evaluate a prompt's quality independently.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<ActionResult<object>> Evaluate(
        [FromBody] PromptEvaluationRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalPrompt) ||
            string.IsNullOrWhiteSpace(request.OptimizedPrompt))
        {
            return BadRequest(new { error = new { message = "Both originalPrompt and optimizedPrompt are required.", code = "validation_error" } });
        }

        var result = await _evaluationPipeline.EvaluateAsync(request, request.EvaluationMode, ct);

        _metrics.RecordQualityEvaluation(new QualityEvaluationMetric
        {
            EvaluatorUsed = result.EvaluatorUsed,
            LlmUsed = result.LlmUsed,
            FallbackUsed = result.FallbackUsed,
            QualityScore = result.Score.Overall,
            IntentPreservation = result.Score.IntentPreservation,
            ConstraintPreservation = result.Score.ConstraintPreservation,
            SecurityScore = result.Score.SecurityValidation,
            EvaluationDurationMs = result.EvaluationDurationMs
        });

        return Ok(new
        {
            score = new
            {
                result.Score.IntentPreservation,
                result.Score.ConstraintPreservation,
                result.Score.ContextRelevance,
                result.Score.Structure,
                result.Score.TokenEfficiency,
                result.Score.SecurityValidation,
                result.Score.Overall,
                result.Score.Evaluator,
                result.Score.EvaluatorVersion
            },
            llmScore = result.LlmScore != null ? new
            {
                result.LlmScore.IntentPreservation,
                result.LlmScore.ConstraintPreservation,
                result.LlmScore.ContextRelevance,
                result.LlmScore.Structure,
                result.LlmScore.TokenEfficiency,
                result.LlmScore.SecurityValidation,
                result.LlmScore.Overall
            } : null,
            result.LlmUsed,
            result.FallbackUsed,
            result.EvaluatorUsed,
            result.EvaluationDurationMs,
            result.EvaluationMode,
            result.Confidence,
            result.Issues,
            result.Recommendations
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // PROMPT COMPARISON
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Compare multiple prompt candidates and select the best one.
    /// </summary>
    [HttpPost("compare")]
    public async Task<ActionResult<object>> Compare(
        [FromBody] PromptComparisonRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalPrompt))
        {
            return BadRequest(new { error = new { message = "originalPrompt is required.", code = "validation_error" } });
        }

        if (request.Candidates.Count == 0)
        {
            return BadRequest(new { error = new { message = "At least one candidate is required.", code = "validation_error" } });
        }

        var result = await _candidateSelector.CompareAndSelectAsync(request, ct);

        return Ok(new
        {
            comparison = new
            {
                result.Comparison.OriginalScore,
                result.Comparison.DeterministicScore,
                result.Comparison.LlmScore,
                result.Comparison.FinalScore,
                result.Comparison.Improvement,
                result.Comparison.SelectedVariant,
                result.Comparison.SelectionReason,
                result.Comparison.FallbackUsed,
                result.Comparison.IsDeterministic
            },
            evaluations = result.Evaluations.Select(e => new
            {
                e.Name,
                score = new
                {
                    e.Score.Overall,
                    e.Score.IntentPreservation,
                    e.Score.ConstraintPreservation,
                    e.Score.SecurityValidation
                },
                e.PassedQualityGate,
                e.SecurityValidated,
                e.ConstraintsPreserved,
                e.RejectionReason
            }),
            bestCandidate = result.BestCandidate != null ? new
            {
                result.BestCandidate.Name,
                result.BestCandidate.Score.Overall,
                result.BestCandidate.PassedQualityGate,
                result.BestCandidate.SecurityValidated
            } : null,
            result.IsDeterministic,
            result.SelectionExplanation
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // METRICS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get aggregated metrics.
    /// </summary>
    [HttpGet("metrics")]
    public ActionResult<object> GetMetrics(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var summary = _metrics.GetSummary(from, to);

        return Ok(new
        {
            summary.TotalRequests,
            summary.SuccessfulRequests,
            summary.FailedRequests,
            summary.FallbackCount,
            summary.LlmUsageCount,
            summary.DeterministicCount,
            latency = new
            {
                summary.AverageProcessingDurationMs,
                summary.AverageIntentDurationMs,
                summary.AverageOptimizationDurationMs,
                summary.AverageEvaluationDurationMs
            },
            quality = new
            {
                summary.AverageQualityScore,
                summary.QualityGatePassRate,
                summary.AverageConstraintPreservation,
                summary.AverageSecurityScore,
                summary.OptimizationImprovementRate
            },
            tokens = new
            {
                summary.TotalEstimatedInputTokens,
                summary.TotalEstimatedOutputTokens,
                summary.AverageTokensPerRequest
            },
            period = new
            {
                summary.From,
                summary.To
            }
        });
    }

    /// <summary>
    /// Get high-level performance summary.
    /// </summary>
    [HttpGet("metrics/summary")]
    public ActionResult<object> GetMetricsSummary()
    {
        var summary = _metrics.GetSummary();

        return Ok(new
        {
            totalRequests = summary.TotalRequests,
            successRate = summary.TotalRequests > 0
                ? (double)summary.SuccessfulRequests / summary.TotalRequests : 0,
            fallbackRate = summary.TotalRequests > 0
                ? (double)summary.FallbackCount / summary.TotalRequests : 0,
            llmUsageRate = summary.TotalRequests > 0
                ? (double)summary.LlmUsageCount / summary.TotalRequests : 0,
            averageQualityScore = summary.AverageQualityScore,
            averageLatencyMs = summary.AverageProcessingDurationMs,
            timestamp = DateTime.UtcNow
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // EXPERIMENTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a new experiment.
    /// </summary>
    [HttpPost("experiments")]
    public async Task<ActionResult<object>> CreateExperiment(
        [FromBody] CreateExperimentRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = new { message = "Name is required.", code = "validation_error" } });
        }

        var experiment = new PromptExperiment
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            ProfileId = request.ProfileId
        };

        var variants = request.Variants.Select(v => new PromptExperimentVariant
        {
            Name = v.Name,
            ProfileId = v.ProfileId,
            ProfileVersion = v.ProfileVersion,
            OptimizationMode = v.OptimizationMode,
            Weight = v.Weight,
            Enabled = v.Enabled
        }).ToList();

        var created = await _experimentService.CreateExperimentAsync(experiment, variants, ct);

        return CreatedAtAction(nameof(GetExperiment), new { id = created.Id }, new
        {
            created.Id,
            created.Name,
            created.Description,
            created.Status,
            created.CreatedAt,
            variants = variants.Select(v => new
            {
                v.Id,
                v.Name,
                v.OptimizationMode,
                v.Weight,
                v.Enabled
            })
        });
    }

    /// <summary>
    /// List experiments.
    /// </summary>
    [HttpGet("experiments")]
    public async Task<ActionResult<object>> GetExperiments(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        ExperimentStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ExperimentStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var experiments = await _experimentService.GetExperimentsAsync(statusFilter, ct);

        return Ok(experiments.Select(e => new
        {
            e.Id,
            e.Name,
            e.Description,
            Status = e.Status.ToString(),
            e.ProfileId,
            e.StartAt,
            e.EndAt,
            e.CreatedAt
        }));
    }

    /// <summary>
    /// Get experiment details.
    /// </summary>
    [HttpGet("experiments/{id:guid}")]
    public async Task<ActionResult<object>> GetExperiment(Guid id, CancellationToken ct)
    {
        var experiment = await _experimentService.GetExperimentAsync(id, ct);
        if (experiment == null) return NotFound();

        var variants = await _experimentService.GetVariantsAsync(id, ct);

        return Ok(new
        {
            experiment.Id,
            experiment.Name,
            experiment.Description,
            Status = experiment.Status.ToString(),
            experiment.ProfileId,
            experiment.StartAt,
            experiment.EndAt,
            experiment.CreatedAt,
            variants = variants.Select(v => new
            {
                v.Id,
                v.Name,
                v.OptimizationMode,
                v.Weight,
                v.Enabled
            })
        });
    }

    /// <summary>
    /// Start an experiment.
    /// </summary>
    [HttpPost("experiments/{id:guid}/start")]
    public async Task<ActionResult<object>> StartExperiment(Guid id, CancellationToken ct)
    {
        var result = await _experimentService.StartExperimentAsync(id, ct);
        if (!result) return NotFound();

        return Ok(new { id, status = "Running", startedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Pause an experiment.
    /// </summary>
    [HttpPost("experiments/{id:guid}/pause")]
    public async Task<ActionResult<object>> PauseExperiment(Guid id, CancellationToken ct)
    {
        var result = await _experimentService.PauseExperimentAsync(id, ct);
        if (!result) return NotFound();

        return Ok(new { id, status = "Paused", pausedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Complete an experiment.
    /// </summary>
    [HttpPost("experiments/{id:guid}/complete")]
    public async Task<ActionResult<object>> CompleteExperiment(Guid id, CancellationToken ct)
    {
        var result = await _experimentService.CompleteExperimentAsync(id, ct);
        if (!result) return NotFound();

        return Ok(new { id, status = "Completed", completedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Cancel an experiment.
    /// </summary>
    [HttpPost("experiments/{id:guid}/cancel")]
    public async Task<ActionResult<object>> CancelExperiment(Guid id, CancellationToken ct)
    {
        var result = await _experimentService.CancelExperimentAsync(id, ct);
        if (!result) return NotFound();

        return Ok(new { id, status = "Cancelled", cancelledAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Get experiment results.
    /// </summary>
    [HttpGet("experiments/{id:guid}/results")]
    public async Task<ActionResult<object>> GetExperimentResults(
        Guid id,
        [FromQuery] Guid? variantId,
        CancellationToken ct)
    {
        var results = await _experimentService.GetResultsAsync(id, variantId, ct);

        return Ok(results.Select(r => new
        {
            r.Id,
            r.ExperimentId,
            r.VariantId,
            r.ProcessingRecordId,
            r.QualityScore,
            r.QualityGatePassed,
            r.EstimatedInputTokens,
            r.EstimatedOutputTokens,
            r.ProcessingDurationMs,
            r.WasFallbackUsed,
            r.WasLlmUsed,
            r.CreatedAt
        }));
    }

    /// <summary>
    /// Get metrics for a specific experiment.
    /// </summary>
    [HttpGet("experiments/{id:guid}/metrics")]
    public ActionResult<object> GetExperimentMetrics(Guid id)
    {
        var metrics = _metrics.GetExperimentMetrics(id);

        return Ok(new
        {
            metrics.ExperimentId,
            metrics.ExperimentName,
            metrics.TotalRequests,
            variants = metrics.Variants.Select(v => new
            {
                v.VariantId,
                v.VariantName,
                v.RequestCount,
                v.AverageQualityScore,
                v.AverageProcessingDurationMs,
                v.FallbackRate,
                v.QualityGatePassRate
            })
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // PHASE 13: EXPERIMENT ANALYTICS & STATISTICS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Get aggregate analytics for an experiment.
    /// </summary>
    [HttpGet("experiments/{id:guid}/analysis")]
    public async Task<ActionResult<object>> GetExperimentAnalysis(
        Guid id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var analytics = await _analyticsService.GetExperimentAnalyticsAsync(id, from, to, ct);
        var variantAnalytics = await _analyticsService.GetVariantAnalyticsAsync(id, from, to, ct);

        return Ok(new
        {
            analytics.ExperimentId,
            analytics.TotalResults,
            analytics.SuccessCount,
            analytics.FailureCount,
            analytics.FallbackRate,
            analytics.LlmUsageRate,
            analytics.QualityGatePassRate,
            analytics.AverageQualityScore,
            analytics.AverageInputTokens,
            analytics.AverageOutputTokens,
            analytics.AverageProcessingLatencyMs,
            analytics.From,
            analytics.To,
            variants = variantAnalytics.Select(v => new
            {
                v.VariantId,
                v.VariantName,
                v.ResultCount,
                v.AverageQualityScore,
                v.FallbackRate,
                v.LlmUsageRate,
                v.QualityGatePassRate,
                v.AverageInputTokens,
                v.AverageOutputTokens,
                v.AverageProcessingLatencyMs
            })
        });
    }

    /// <summary>
    /// Compare statistical significance between two experiment variants.
    /// </summary>
    [HttpPost("experiments/{id:guid}/compare-variants")]
    public async Task<ActionResult<object>> CompareVariants(
        Guid id,
        [FromBody] CompareVariantsRequest request,
        CancellationToken ct)
    {
        if (request.VariantAId == Guid.Empty || request.VariantBId == Guid.Empty)
        {
            return BadRequest(new { error = new { message = "Both variantAId and variantBId are required.", code = "validation_error" } });
        }

        var resultsA = await _experimentService.GetResultsAsync(id, request.VariantAId, ct);
        var resultsB = await _experimentService.GetResultsAsync(id, request.VariantBId, ct);

        var scoresA = resultsA.Where(r => r.QualityScore.HasValue)
            .Select(r => r.QualityScore!.Value).ToList();
        var scoresB = resultsB.Where(r => r.QualityScore.HasValue)
            .Select(r => r.QualityScore!.Value).ToList();

        var comparison = _statisticsAnalyzer.CompareVariants(scoresA, scoresB);

        return Ok(new
        {
            comparison.SampleCountA,
            comparison.SampleCountB,
            comparison.MeanA,
            comparison.MeanB,
            comparison.VarianceA,
            comparison.VarianceB,
            comparison.StandardDeviationA,
            comparison.StandardDeviationB,
            comparison.MeanDifference,
            Significance = comparison.Significance.ToString(),
            comparison.PValue,
            comparison.ConfidenceIntervalLower,
            comparison.ConfidenceIntervalUpper,
            comparison.Summary
        });
    }

    /// <summary>
    /// Resume a paused experiment (moves to Running).
    /// </summary>
    [HttpPost("experiments/{id:guid}/resume")]
    public async Task<ActionResult<object>> ResumeExperiment(Guid id, CancellationToken ct)
    {
        // Resume = move from Paused to Running
        var experiment = await _experimentService.GetExperimentAsync(id, ct);
        if (experiment == null) return NotFound();

        if (experiment.Status != ExperimentStatus.Paused)
        {
            return BadRequest(new { error = new { message = $"Cannot resume experiment in {experiment.Status} state.", code = "invalid_transition" } });
        }

        var result = await _experimentService.StartExperimentAsync(id, ct);
        if (!result) return BadRequest(new { error = new { message = "Failed to resume experiment.", code = "resume_failed" } });

        return Ok(new { id, status = "Running", resumedAt = DateTime.UtcNow });
    }
}

/// <summary>
/// Request to compare two variants statistically.
/// </summary>
public class CompareVariantsRequest
{
    public Guid VariantAId { get; set; }
    public Guid VariantBId { get; set; }
    public double SignificanceLevel { get; set; } = 0.05;
}

/// <summary>
/// Request to create an experiment.
/// </summary>
public class CreateExperimentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProfileId { get; set; }
    public List<CreateVariantRequest> Variants { get; set; } = [];
}

/// <summary>
/// Request to create an experiment variant.
/// </summary>
public class CreateVariantRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? ProfileId { get; set; }
    public int? ProfileVersion { get; set; }
    public string OptimizationMode { get; set; } = "Deterministic";
    public double Weight { get; set; } = 1.0;
    public bool Enabled { get; set; } = true;
}
