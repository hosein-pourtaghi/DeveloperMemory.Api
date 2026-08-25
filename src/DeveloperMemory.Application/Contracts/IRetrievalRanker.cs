using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Abstraction for ranking retrieved memory candidates by relevance.
/// The ranking system should be provider-independent and deterministic.
/// </summary>
public interface IRetrievalRanker
{
    /// <summary>
    /// Ranks candidate memories by relevance to the retrieval request.
    /// Returns candidates ordered by relevance score (highest first).
    /// </summary>
    Task<List<RetrievedMemory>> RankAsync(
        List<RetrievedMemory> candidates,
        RetrievalRequest request,
        CancellationToken ct = default);
}
