using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Service for managing embeddings: generation, storage, retrieval, and rebuilding.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates and stores an embedding for a memory.
    /// Returns the embedding result without persisting the memory itself.
    /// </summary>
    Task<EmbeddingResult> GenerateAndStoreAsync(
        Guid memoryId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the stored embedding for a memory.
    /// </summary>
    Task<Domain.Entities.Embedding?> GetAsync(
        Guid memoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes the stored embedding for a memory.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid memoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Rebuilds an embedding for a memory (regenerate and store).
    /// </summary>
    Task<EmbeddingResult> RebuildAsync(
        Guid memoryId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if semantic retrieval is available.
    /// </summary>
    bool IsSemanticAvailable { get; }
}
