using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Repository for persistent API key management.
/// Raw secrets are never stored — only salted hashes.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>Create a new API key. The raw secret is NOT stored.</summary>
    Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken ct = default);

    /// <summary>Look up a key by its hash (for authentication).</summary>
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);

    /// <summary>Get a key by ID.</summary>
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>List all keys for a given owner (without secrets).</summary>
    Task<List<ApiKey>> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default);

    /// <summary>Update key metadata (e.g., last used, revocation).</summary>
    Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default);

    /// <summary>Get expired keys older than the specified cutoff.</summary>
    Task<List<ApiKey>> GetExpiredKeysAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>Delete expired keys (for cleanup, preserves audit-trail records).</summary>
    Task<int> DeleteExpiredKeysAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>Look up a key by its prefix (first N chars of raw key). Returns the most likely match.</summary>
    Task<ApiKey?> GetByKeyPrefixAsync(string keyPrefix, CancellationToken ct = default);
}
