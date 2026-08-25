using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Provider-independent abstraction for vector storage and similarity search.
/// The application layer must not know whether the implementation is:
/// - PostgreSQL + pgvector
/// - Qdrant, Weaviate, Milvus
/// - In-memory test implementation
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Whether this vector store is currently available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Upserts a vector for the given memory.
    /// If a vector already exists for this memory, it is replaced.
    /// </summary>
    Task<bool> UpsertAsync(
        Guid memoryId,
        Embedding embedding,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the vector for the given memory.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid memoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Searches for similar vectors.
    /// Returns results ordered by similarity (highest first).
    /// </summary>
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        float[] queryVector,
        int maxResults,
        double minimumScore = 0.0,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the stored embedding for a specific memory.
    /// Returns null if no embedding exists.
    /// </summary>
    Task<Embedding?> GetAsync(
        Guid memoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of stored vectors.
    /// </summary>
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>
/// A semantic search result with similarity score and metadata.
/// </summary>
public class SemanticSearchResult
{
    /// <summary>The memory ID this vector belongs to.</summary>
    public Guid MemoryId { get; set; }

    /// <summary>Similarity score (0.0-1.0, higher is more similar).</summary>
    public double SimilarityScore { get; set; }

    /// <summary>The embedding provider that generated the stored vector.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The embedding model used.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>The embedding profile key for compatibility checking.</summary>
    public string ProfileKey { get; set; } = string.Empty;
}
