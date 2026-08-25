using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Evaluates prompt quality deterministically.
/// Provider-independent abstraction for future LLM/benchmark evaluators.
/// </summary>
public interface IPromptQualityEvaluator
{
    /// <summary>
    /// Evaluates the quality of an optimized prompt.
    /// </summary>
    PromptQualityScore Evaluate(
        string originalPrompt,
        string optimizedPrompt,
        IntentAnalysisResult? intent = null,
        int tokenBudget = 4000);

    /// <summary>Evaluator name.</summary>
    string EvaluatorName { get; }

    /// <summary>Evaluator version.</summary>
    string EvaluatorVersion { get; }
}
