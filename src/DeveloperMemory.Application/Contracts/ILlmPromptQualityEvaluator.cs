using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent LLM quality evaluator.
/// Evaluates prompt quality using an external LLM behind an abstraction.
/// LLM evaluation is optional and must never become a single point of failure.
/// </summary>
public interface ILlmPromptQualityEvaluator
{
    /// <summary>
    /// Evaluates prompt quality using an LLM.
    /// </summary>
    Task<PromptQualityScore> EvaluateAsync(
        string originalPrompt,
        string optimizedPrompt,
        IntentAnalysisResult? intent = null,
        int tokenBudget = 4000,
        CancellationToken ct = default);

    /// <summary>Whether this evaluator is available.</summary>
    bool IsAvailable { get; }

    /// <summary>Evaluator name.</summary>
    string EvaluatorName { get; }

    /// <summary>Evaluator version.</summary>
    string EvaluatorVersion { get; }
}
