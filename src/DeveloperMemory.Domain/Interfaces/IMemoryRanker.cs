using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Ranks memory candidates by relevance to a query/task.
/// The baseline implementation uses deterministic signals.
/// Future implementations may use embeddings, vector search, or LLM-based reranking.
/// </summary>
public interface IMemoryRanker
{
    /// <summary>
    /// Ranks candidate memories by relevance to the query.
    /// Returns candidates ordered by relevance (highest first).
    /// </summary>
    IReadOnlyList<RankedMemory> Rank(
        IReadOnlyList<MemoryEntry> candidates,
        string query,
        RankingContext? context = null);
}

/// <summary>
/// A memory with its computed relevance score.
/// </summary>
public class RankedMemory
{
    public MemoryEntry Memory { get; set; } = null!;
    public double RelevanceScore { get; set; }
    public RankingSignals Signals { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Individual ranking signals for explainability.
/// </summary>
public class RankingSignals
{
    public double TextRelevance { get; set; }
    public double TypeRelevance { get; set; }
    public double ImportanceScore { get; set; }
    public double ConfidenceScore { get; set; }
    public double RecencyScore { get; set; }
    public double AccessFrequencyScore { get; set; }
    public double ScopeSpecificityScore { get; set; }
}

/// <summary>
/// Additional context for ranking decisions.
/// </summary>
public class RankingContext
{
    public Guid? ProjectId { get; set; }
    public string? WorkspaceId { get; set; }
    public string? UserId { get; set; }
    public MemoryScope? PreferredScope { get; set; }
}
