using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Resolves effective intent from deterministic and LLM analyses.
///
/// Resolution rules:
/// - Deterministic result is always the baseline
/// - LLM may improve classification when confidence is high
/// - High-confidence deterministic signals are not casually overridden
/// - Conflicts are represented, not silently resolved
/// - Low-confidence LLM results fall back to deterministic
/// </summary>
public class IntentResolver : IIntentResolver
{
    private readonly IntentResolutionPolicy _policy;
    private readonly ILogger<IntentResolver> _logger;

    public IntentResolver(
        ILogger<IntentResolver> logger,
        IntentResolutionPolicy? policy = null)
    {
        _logger = logger;
        _policy = policy ?? new IntentResolutionPolicy();
    }

    public IntentAnalysisResult Resolve(
        IntentAnalysisResult deterministic,
        IntentAnalysisResult? llm = null)
    {
        // If no LLM result, use deterministic
        if (llm == null)
        {
            _logger.LogDebug("No LLM intent available; using deterministic only");
            return deterministic;
        }

        // If LLM and deterministic agree, use deterministic (authoritative)
        if (deterministic.Intent == llm.Intent && deterministic.TaskType == llm.TaskType)
        {
            _logger.LogDebug("Deterministic and LLM agree on intent: {Intent}", deterministic.Intent);
            return deterministic;
        }

        // If deterministic has high confidence in specific patterns, prefer it
        if (IsHighConfidenceDeterministic(deterministic))
        {
            _logger.LogDebug(
                "High-confidence deterministic signal ({Intent}); using deterministic",
                deterministic.Intent);
            return deterministic;
        }

        // If LLM confidence is very high and deterministic is general, prefer LLM
        var llmConfidence = EstimateLlmConfidence(llm);
        if (llmConfidence >= _policy.MinLlmConfidenceToOverride &&
            deterministic.Intent == IntentType.General)
        {
            _logger.LogDebug(
                "LLM high confidence ({Confidence:P0}) overrides general deterministic; using LLM",
                llmConfidence);
            return MergeResults(deterministic, llm, useLlmPrimary: true);
        }

        // Default: use deterministic as primary, merge useful LLM signals
        _logger.LogDebug(
            "Using deterministic ({DetIntent}) with LLM signals merged",
            deterministic.Intent);
        return MergeResults(deterministic, llm, useLlmPrimary: false);
    }

    private static bool IsHighConfidenceDeterministic(IntentAnalysisResult intent)
    {
        // Deterministic has high confidence when it detects specific patterns
        return intent.Intent != IntentType.General &&
               intent.Keywords.Count >= 2 &&
               intent.TechnicalContext.Count >= 1;
    }

    private static double EstimateLlmConfidence(IntentAnalysisResult intent)
    {
        // Estimate confidence from available signals
        double confidence = 0.5;

        if (intent.Intent != IntentType.General) confidence += 0.2;
        if (intent.Keywords.Count >= 2) confidence += 0.1;
        if (intent.TechnicalContext.Count >= 1) confidence += 0.1;
        if (intent.ExplicitConstraints.Count > 0) confidence += 0.1;

        return Math.Min(confidence, 1.0);
    }

    private IntentAnalysisResult MergeResults(
        IntentAnalysisResult deterministic,
        IntentAnalysisResult llm,
        bool useLlmPrimary)
    {
        var primary = useLlmPrimary ? llm : deterministic;
        var secondary = useLlmPrimary ? deterministic : llm;

        return new IntentAnalysisResult
        {
            OriginalInput = primary.OriginalInput,
            Intent = primary.Intent,
            TaskType = primary.TaskType,
            TechnicalDomain = !string.IsNullOrEmpty(primary.TechnicalDomain)
                ? primary.TechnicalDomain
                : secondary.TechnicalDomain,
            RequiredContext = primary.RequiredContext.Count > 0
                ? primary.RequiredContext
                : secondary.RequiredContext,
            RiskLevel = primary.RiskLevel,
            Complexity = primary.Complexity,
            Keywords = primary.Keywords.Concat(secondary.Keywords).Distinct().ToList(),
            TechnicalContext = primary.TechnicalContext.Concat(secondary.TechnicalContext).Distinct().ToList(),
            ExplicitConstraints = primary.ExplicitConstraints.Concat(secondary.ExplicitConstraints).Distinct().ToList(),
            GoalSummary = primary.GoalSummary,
            IsMemoryInstruction = primary.IsMemoryInstruction || secondary.IsMemoryInstruction,
            RequiresProjectContext = primary.RequiresProjectContext || secondary.RequiresProjectContext,
            IsSimpleQuery = primary.IsSimpleQuery && secondary.IsSimpleQuery
        };
    }
}
