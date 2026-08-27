using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Analyzes user intent from raw input.
/// Provider-independent abstraction with replaceable implementations.
/// </summary>
public interface IIntentAnalyzer
{
    /// <summary>
    /// Analyzes the input and returns structured intent analysis.
    /// </summary>
    Task<IntentAnalysisResult> AnalyzeAsync(
        string input,
        PromptContext? context = null,
        CancellationToken ct = default);
}
