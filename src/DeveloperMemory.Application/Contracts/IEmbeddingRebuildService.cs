using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Service for managing embedding lifecycle: staleness detection, batch rebuild, and profile management.
/// </summary>
public interface IEmbeddingRebuildService
{
    /// <summary>
    /// Checks if an embedding is stale for the current embedding profile.
    /// </summary>
    Task<bool> IsStaleAsync(Guid memoryId, CancellationToken ct = default);

    /// <summary>
    /// Gets stale embeddings that need rebuilding.
    /// </summary>
    Task<IReadOnlyList<StaleEmbeddingInfo>> GetStaleEmbeddingsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Rebuilds a single embedding.
    /// </summary>
    Task<EmbeddingResult> RebuildAsync(
        Guid memoryId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Rebuilds multiple embeddings in batch.
    /// Returns the number of successful rebuilds.
    /// </summary>
    Task<int> RebuildBatchAsync(
        IReadOnlyList<EmbeddingRebuildRequest> requests,
        CancellationToken ct = default);

    /// <summary>
    /// Gets statistics about the embedding system.
    /// </summary>
    Task<EmbeddingStats> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Information about a stale embedding that needs rebuilding.
/// </summary>
public class StaleEmbeddingInfo
{
    public Guid MemoryId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CurrentProvider { get; set; } = string.Empty;
    public string CurrentModel { get; set; } = string.Empty;
}

/// <summary>
/// Request to rebuild an embedding for a specific memory.
/// </summary>
public class EmbeddingRebuildRequest
{
    public Guid MemoryId { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Statistics about the embedding system.
/// </summary>
public class EmbeddingStats
{
    public int TotalVectors { get; set; }
    public int ReadyVectors { get; set; }
    public int FailedVectors { get; set; }
    public int StaleVectors { get; set; }
    public string CurrentProvider { get; set; } = string.Empty;
    public string CurrentModel { get; set; } = string.Empty;
    public bool ProviderAvailable { get; set; }
}
