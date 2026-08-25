using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Abstraction for memory retrieval providers.
/// The current implementation is keyword-based; future implementations
/// may use embeddings, vector search, or hybrid approaches.
/// </summary>
public interface IMemoryRetrievalProvider
{
    /// <summary>
    /// The name of this provider (e.g., "keyword", "embedding", "hybrid").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Retrieves candidate memories matching the request criteria.
    /// Returns unranked candidates — ranking is performed by the caller.
    /// </summary>
    Task<List<MemoryEntry>> GetCandidatesAsync(
        RetrievalRequest request,
        CancellationToken ct = default);
}
