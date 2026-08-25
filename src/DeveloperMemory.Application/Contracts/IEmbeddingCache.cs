using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent embedding cache abstraction.
/// Cache keys include provider/model/version/text-hash to prevent incorrect reuse.
/// The cache is an optimization only — never the source of truth.
/// </summary>
public interface IEmbeddingCache
{
    /// <summary>
    /// Gets a cached embedding if available and matching the current profile.
    /// Returns null if not cached or if the cached version doesn't match.
    /// </summary>
    Task<Embedding?> GetAsync(
        string provider,
        string model,
        string? version,
        string textHash,
        CancellationToken ct = default);

    /// <summary>
    /// Stores an embedding in the cache.
    /// </summary>
    Task SetAsync(
        string provider,
        string model,
        string? version,
        string textHash,
        Embedding embedding,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a cached embedding.
    /// </summary>
    Task<bool> RemoveAsync(
        string provider,
        string model,
        string? version,
        string textHash,
        CancellationToken ct = default);

    /// <summary>
    /// Clears all cached embeddings for a specific provider/model combination.
    /// Used when embedding models change.
    /// </summary>
    Task<int> ClearByProfileAsync(
        string provider,
        string model,
        CancellationToken ct = default);

    /// <summary>
    /// Computes a stable text hash suitable for cache keys.
    /// </summary>
    static string ComputeTextHash(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant());
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16]; // Truncate for key readability
    }
}
