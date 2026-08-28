namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// A retrieval candidate and optional provider-specific relevance signals.
/// </summary>
public sealed class RetrievalCandidate
{
    public MemoryEntry Memory { get; set; } = null!;
    public double? SemanticScore { get; set; }
}
