using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Orchestrates extraction from multiple strategies (deterministic + LLM).
/// Combines, deduplicates, and validates candidates.
/// </summary>
public interface IExtractionOrchestrator
{
    /// <summary>
    /// Extracts memory candidates from input using all available strategies.
    /// </summary>
    Task<ExtractionOrchestrationResult> ExtractAsync(
        MemoryExtractionRequest request,
        ExtractionMode mode = ExtractionMode.Auto,
        CancellationToken ct = default);
}

/// <summary>
/// Extraction mode control.
/// </summary>
public enum ExtractionMode
{
    /// <summary>Use deterministic extraction only.</summary>
    Deterministic,

    /// <summary>Use LLM extraction only.</summary>
    LLM,

    /// <summary>Use both when LLM is available, fall back to deterministic.</summary>
    Auto
}

/// <summary>
/// Result of extraction orchestration.
/// </summary>
public class ExtractionOrchestrationResult
{
    /// <summary>All validated candidates from all strategies.</summary>
    public IReadOnlyList<MemoryCandidate> Candidates { get; set; } = [];

    /// <summary>Which strategy was actually used.</summary>
    public string StrategyUsed { get; set; } = string.Empty;

    /// <summary>Whether LLM was available and used.</summary>
    public bool LlmUsed { get; set; }

    /// <summary>Number of candidates from deterministic extraction.</summary>
    public int DeterministicCount { get; set; }

    /// <summary>Number of candidates from LLM extraction.</summary>
    public int LlmCount { get; set; }

    /// <summary>Number of candidates after deduplication.</summary>
    public int FinalCount { get; set; }

    /// <summary>Warnings or notes about the extraction.</summary>
    public List<string> Warnings { get; set; } = [];
}
