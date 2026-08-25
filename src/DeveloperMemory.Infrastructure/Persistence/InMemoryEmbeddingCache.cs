using System.Collections.Concurrent;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// In-memory embedding cache for testing and development.
/// Thread-safe via ConcurrentDictionary with TTL expiration.
/// NOT suitable for production — use a distributed cache for production.
/// </summary>
public class InMemoryEmbeddingCache : IEmbeddingCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _expiration;

    public InMemoryEmbeddingCache(TimeSpan? expiration = null)
    {
        _expiration = expiration ?? TimeSpan.FromMinutes(1440); // 24 hours default
    }

    public Task<Embedding?> GetAsync(
        string provider,
        string model,
        string? version,
        string textHash,
        CancellationToken ct = default)
    {
        var key = MakeKey(provider, model, version, textHash);

        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            return Task.FromResult<Embedding?>(entry.Embedding);
        }

        // Remove expired entry if present
        if (entry != null)
        {
            _cache.TryRemove(key, out _);
        }

        return Task.FromResult<Embedding?>(null);
    }

    public Task SetAsync(
        string provider,
        string model,
        string? version,
        string textHash,
        Embedding embedding,
        CancellationToken ct = default)
    {
        var key = MakeKey(provider, model, version, textHash);
        _cache[key] = new CacheEntry(embedding, DateTime.UtcNow + _expiration);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(
        string provider,
        string model,
        string? version,
        string textHash,
        CancellationToken ct = default)
    {
        var key = MakeKey(provider, model, version, textHash);
        return Task.FromResult(_cache.TryRemove(key, out _));
    }

    public Task<int> ClearByProfileAsync(
        string provider,
        string model,
        CancellationToken ct = default)
    {
        var prefix = $"{provider}/{model}/";
        var keysToRemove = _cache.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var removed = 0;
        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out _))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }

    /// <summary>
    /// Clears all cached entries. Useful for test cleanup.
    /// </summary>
    public void Clear() => _cache.Clear();

    private static string MakeKey(string provider, string model, string? version, string textHash)
    {
        return $"{provider}/{model}/{version ?? "latest"}/{textHash}";
    }

    private sealed class CacheEntry
    {
        public Embedding Embedding { get; }
        public DateTime ExpiresAt { get; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        public CacheEntry(Embedding embedding, DateTime expiresAt)
        {
            Embedding = embedding;
            ExpiresAt = expiresAt;
        }
    }
}
