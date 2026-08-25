using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Selects the best prompt candidate based on quality evaluation.
/// Deterministic when scores tie. Security failures always reject candidates.
/// </summary>
public class PromptCandidateSelector : IPromptCandidateSelector
{
    private readonly HybridQualityEvaluationPipeline _evaluationPipeline;
    private readonly IPromptQualityEvaluator _deterministicEvaluator;
    private readonly ILogger<PromptCandidateSelector> _logger;

    public PromptCandidateSelector(
        HybridQualityEvaluationPipeline evaluationPipeline,
        IPromptQualityEvaluator deterministicEvaluator,
        ILogger<PromptCandidateSelector> logger)
    {
        _evaluationPipeline = evaluationPipeline;
        _deterministicEvaluator = deterministicEvaluator;
        _logger = logger;
    }

    public async Task<PromptComparisonResult> CompareAndSelectAsync(
        PromptComparisonRequest request,
        CancellationToken ct = default)
    {
        var result = new PromptComparisonResult();

        // Evaluate original prompt
        var originalScore = _deterministicEvaluator.Evaluate(
            request.OriginalPrompt, request.OriginalPrompt, tokenBudget: request.TokenBudget);

        result.Comparison.OriginalScore = originalScore.Overall;

        // Evaluate each candidate
        CandidateEvaluation? bestCandidate = null;

        foreach (var candidate in request.Candidates)
        {
            var evaluation = await EvaluateCandidateAsync(candidate, request, ct);
            result.Evaluations.Add(evaluation);

            // Security failure = candidate rejected
            if (!evaluation.SecurityValidated)
            {
                evaluation.RejectionReason = "Security validation failed";
                _logger.LogWarning("Candidate {Name} rejected: security validation failed", candidate.Name);
                continue;
            }

            // Constraint failure = candidate rejected
            if (!evaluation.ConstraintsPreserved)
            {
                evaluation.RejectionReason = "Critical constraints not preserved";
                _logger.LogWarning("Candidate {Name} rejected: constraints not preserved", candidate.Name);
                continue;
            }

            // Quality gate failure = candidate rejected unless no better option
            if (!evaluation.PassedQualityGate)
            {
                evaluation.RejectionReason = "Quality gate failed";
                continue;
            }

            // Select best by score (deterministic tie-breaking: first wins)
            if (bestCandidate == null || evaluation.Score.Overall > bestCandidate.Score.Overall)
            {
                bestCandidate = evaluation;
            }
        }

        // If no candidate passed, fall back to original (deterministic safe)
        if (bestCandidate == null)
        {
            bestCandidate = new CandidateEvaluation
            {
                Name = "original",
                Score = originalScore,
                PassedQualityGate = true,
                SecurityValidated = true,
                ConstraintsPreserved = true
            };
            result.Comparison.FallbackUsed = true;
            result.Comparison.SelectedVariant = "original";
            result.Comparison.SelectionReason = "No valid optimized candidate; using original prompt";
        }
        else
        {
            result.Comparison.SelectedVariant = bestCandidate.Name;
            result.Comparison.FinalScore = bestCandidate.Score.Overall;
            result.Comparison.Improvement = bestCandidate.Score.Overall - originalScore.Overall;
            result.Comparison.SelectionReason = $"Selected {bestCandidate.Name} with highest validated quality score";
        }

        // Update comparison
        result.Comparison.FinalScore = bestCandidate.Score.Overall;
        result.Comparison.DeterministicScore = result.Evaluations
            .FirstOrDefault(e => e.Name == "deterministic")?.Score.Overall;
        result.Comparison.LlmScore = result.Evaluations
            .FirstOrDefault(e => e.Name == "llm")?.Score.Overall;

        result.BestCandidate = bestCandidate;
        result.SelectionExplanation = result.Comparison.SelectionReason;

        return result;
    }

    private async Task<CandidateEvaluation> EvaluateCandidateAsync(
        PromptCandidate candidate,
        PromptComparisonRequest request,
        CancellationToken ct)
    {
        var evaluationRequest = new PromptEvaluationRequest
        {
            OriginalPrompt = request.OriginalPrompt,
            OptimizedPrompt = candidate.Prompt,
            TokenBudget = request.TokenBudget,
            CorrelationId = request.CorrelationId
        };

        var evaluationResult = await _evaluationPipeline.EvaluateAsync(
            evaluationRequest, "Deterministic", ct);

        var candidateResult = new CandidateEvaluation
        {
            Name = candidate.Name,
            Score = evaluationResult.Score,
            PassedQualityGate = evaluationResult.Score.MeetsThresholds(),
            SecurityValidated = evaluationResult.Score.SecurityValidation >= 0.9,
            ConstraintsPreserved = evaluationResult.Score.ConstraintPreservation >= 0.9
        };

        return candidateResult;
    }
}
