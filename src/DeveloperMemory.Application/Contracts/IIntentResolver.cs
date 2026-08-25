using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Resolves intent from deterministic and LLM analysis results.
/// Produces a single effective intent.
/// </summary>
public interface IIntentResolver
{
    /// <summary>
    /// Resolves the effective intent from available analyses.
    /// </summary>
    IntentAnalysisResult Resolve(
        IntentAnalysisResult deterministic,
        IntentAnalysisResult? llm = null);
}

/// <summary>
/// Configuration for intent resolution behavior.
/// </summary>
public class IntentResolutionPolicy
{
    /// <summary>Minimum LLM confidence to consider overriding deterministic.</summary>
    public double MinLlmConfidenceToOverride { get; set; } = 0.85;

    /// <summary>Whether deterministic always wins on conflict.</summary>
    public bool DeterministicWinsOnConflict { get; set; } = true;

    /// <summary>Weight given to deterministic analysis (0.0-1.0).</summary>
    public double DeterministicWeight { get; set; } = 0.6;

    /// <summary>Weight given to LLM analysis (0.0-1.0).</summary>
    public double LlmWeight { get; set; } = 0.4;
}
