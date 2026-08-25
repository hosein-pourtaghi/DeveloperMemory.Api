using System.Collections.Concurrent;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// In-memory vector store for testing and development.
/// Uses cosine similarity for search.
/// NOT suitable for production — use only for testing.
/// </summary>
public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<Guid, Embedding> _store = new();

    public bool IsAvailable => true;

    public Task<bool> UpsertAsync(
        Guid memoryId,
        Embedding embedding,
        CancellationToken ct = default)
    {
        if (embedding == null || !embedding.IsValid())
        {
            return Task.FromResult(false);
        }

        _store[memoryId] = embedding;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(
        Guid memoryId,
        CancellationToken ct = default)
    {
        return Task.FromResult(_store.TryRemove(memoryId, out _));
    }

    public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        float[] queryVector,
        int maxResults,
        double minimumScore = 0.0,
        CancellationToken ct = default)
    {
        if (queryVector == null || queryVector.Length == 0 || _store.IsEmpty)
        {
            return Task.FromResult<IReadOnlyList<SemanticSearchResult>>([]);
        }

        var results = new List<SemanticSearchResult>();

        foreach (var kvp in _store)
        {
            var similarity = CosineSimilarity(queryVector, kvp.Value.Values);
            if (similarity >= minimumScore)
            {
                results.Add(new SemanticSearchResult
                {
                    MemoryId = kvp.Key,
                    SimilarityScore = similarity,
                    Provider = kvp.Value.Provider,
                    Model = kvp.Value.Model,
                    ProfileKey = $"{kvp.Value.Provider}/{kvp.Value.Model}"
                });
            }
        }

        return Task.FromResult<IReadOnlyList<SemanticSearchResult>>(
            results
                .OrderByDescending(r => r.SimilarityScore)
                .Take(maxResults)
                .ToList());
    }

    public Task<Embedding?> GetAsync(
        Guid memoryId,
        CancellationToken ct = default)
    {
        _store.TryGetValue(memoryId, out var embedding);
        return Task.FromResult(embedding);
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_store.Count);
    }

    /// <summary>
    /// Clears all stored vectors. Useful for test cleanup.
    /// </summary>
    public void Clear() => _store.Clear();

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0.0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(normA) * Math.Sqrt(normB);
        return magnitude > 0 ? dotProduct / magnitude : 0.0;
    }
}
