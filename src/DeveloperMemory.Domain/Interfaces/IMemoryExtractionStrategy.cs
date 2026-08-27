using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Abstraction for extracting memory candidates from input.
/// Implementations may be rule-based, regex/heuristic, LLM-based, or hybrid.
/// 
/// Phase 5 provides a deterministic baseline. Future phases may add LLM-based strategies.
/// </summary>
public interface IMemoryExtractionStrategy
{
    /// <summary>
    /// The strategy name for observability and diagnostics.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Extracts memory candidates from the provided input.
    /// </summary>
    Task<IReadOnlyCollection<MemoryCandidate>> ExtractAsync(
        MemoryExtractionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for memory extraction.
/// </summary>
public class MemoryExtractionRequest
{
    /// <summary>The text content to extract memory from.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>The project context for extraction.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The workspace context.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>The user context.</summary>
    public string? UserId { get; set; }

    /// <summary>Optional hints about what type of memory to look for.</summary>
    public List<MemoryType>? PreferredTypes { get; set; }

    /// <summary>Source or origin of the content being extracted.</summary>
    public string? Source { get; set; }
}

/// <summary>
/// A memory candidate produced by an extraction strategy.
/// </summary>
public class MemoryCandidate
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryType MemoryType { get; set; } = MemoryType.Other;
    public double Importance { get; set; } = 0.5;
    public double Confidence { get; set; } = 0.5;
    public List<string> Tags { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ExtractionReason { get; set; } = string.Empty;
}
