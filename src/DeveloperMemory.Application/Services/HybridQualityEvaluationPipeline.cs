using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Unified quality evaluation pipeline.
/// Combines deterministic and optional LLM evaluation.
/// Deterministic evaluator is always authoritative for security and constraints.
/// </summary>
public class HybridQualityEvaluationPipeline
{
    private readonly IPromptQualityEvaluator _deterministicEvaluator;
    private readonly ILlmPromptQualityEvaluator? _llmEvaluator;
    private readonly ILogger<HybridQualityEvaluationPipeline> _logger;

    public HybridQualityEvaluationPipeline(
        IPromptQualityEvaluator deterministicEvaluator,
        ILogger<HybridQualityEvaluationPipeline> logger,
        ILlmPromptQualityEvaluator? llmEvaluator = null)
    {
        _deterministicEvaluator = deterministicEvaluator;
        _llmEvaluator = llmEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates prompt quality using the specified mode.
    /// Deterministic is always the baseline. LLM is optional.
    /// </summary>
    public async Task<PromptEvaluationResult> EvaluateAsync(
        PromptEvaluationRequest request,
        string evaluationMode = "Auto",
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new PromptEvaluationResult
        {
            EvaluationMode = evaluationMode
        };

        // Step 1: Always run deterministic evaluation
        var deterministicScore = _deterministicEvaluator.Evaluate(
            request.OriginalPrompt,
            request.OptimizedPrompt,
            request.Intent,
            request.TokenBudget);

        result.Score = deterministicScore;
        result.EvaluatorUsed = "deterministic";

        // Step 2: Optionally run LLM evaluation
        if (ShouldUseLlmEvaluator(evaluationMode) && _llmEvaluator?.IsAvailable == true)
        {
            try
            {
                var llmScore = await _llmEvaluator.EvaluateAsync(
                    request.OriginalPrompt,
                    request.OptimizedPrompt,
                    request.Intent,
                    request.TokenBudget,
                    ct);

                result.LlmScore = llmScore;
                result.LlmUsed = true;

                // Combine scores: deterministic security/constraints always win
                result.Score = CombineScores(deterministicScore, llmScore);
                result.EvaluatorUsed = "hybrid";
                result.Confidence = CalculateConfidence(deterministicScore, llmScore);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM evaluation failed, using deterministic fallback");
                result.FallbackUsed = true;
                result.Issues.Add("LLM evaluation failed; using deterministic fallback");
            }
        }
        else if (evaluationMode == "LLM" && _llmEvaluator?.IsAvailable != true)
        {
            result.FallbackUsed = true;
            result.Issues.Add("LLM evaluator not available; using deterministic fallback");
            _logger.LogWarning("LLM evaluation requested but evaluator not available");
        }

        sw.Stop();
        result.EvaluationDurationMs = sw.ElapsedMilliseconds;

        // Collect issues
        if (deterministicScore.Issues.Count > 0)
            result.Issues.AddRange(deterministicScore.Issues);

        return result;
    }

    private bool ShouldUseLlmEvaluator(string mode)
    {
        return mode switch
        {
            "LLM" => true,
            "Hybrid" => true,
            "Auto" => _llmEvaluator?.IsAvailable == true,
            "Deterministic" => false,
            _ => false
        };
    }

    private static PromptQualityScore CombineScores(
        PromptQualityScore deterministic,
        PromptQualityScore llm)
    {
        // Deterministic security/constraints are always authoritative
        return new PromptQualityScore
        {
            IntentPreservation = (deterministic.IntentPreservation + llm.IntentPreservation) / 2,
            ConstraintPreservation = deterministic.ConstraintPreservation, // Deterministic wins for constraints
            ContextRelevance = (deterministic.ContextRelevance + llm.ContextRelevance) / 2,
            Structure = (deterministic.Structure + llm.Structure) / 2,
            TokenEfficiency = (deterministic.TokenEfficiency + llm.TokenEfficiency) / 2,
            SecurityValidation = deterministic.SecurityValidation, // Deterministic wins for security
            Evaluator = "hybrid",
            EvaluatorVersion = "1.0",
            Issues = [.. deterministic.Issues, .. llm.Issues]
        };
        // Note: ComputeOverall() is not called here; the caller should invoke it
    }

    private static double CalculateConfidence(
        PromptQualityScore deterministic,
        PromptQualityScore llm)
    {
        // Confidence based on agreement between evaluators
        var overallDelta = Math.Abs(deterministic.Overall - llm.Overall);
        if (overallDelta < 0.1) return 0.95;
        if (overallDelta < 0.2) return 0.85;
        if (overallDelta < 0.3) return 0.70;
        return 0.50;
    }
}
