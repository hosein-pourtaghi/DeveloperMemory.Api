using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Manages embedding lifecycle: staleness detection, batch rebuild, and profile management.
/// Connects EmbeddingStatus to real provider/vector store behavior.
/// </summary>
public class EmbeddingRebuildService : IEmbeddingRebuildService
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingCache? _cache;
    private readonly ILogger<EmbeddingRebuildService> _logger;

    public EmbeddingRebuildService(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        ILogger<EmbeddingRebuildService> logger,
        IEmbeddingCache? cache = null)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _logger = logger;
        _cache = cache;
    }

    public async Task<bool> IsStaleAsync(Guid memoryId, CancellationToken ct = default)
    {
        try
        {
            var existing = await _vectorStore.GetAsync(memoryId, ct);
            if (existing == null)
            {
                return false; // No embedding exists, not stale — needs creation
            }

            // Check if the current profile matches
            var currentProfile = _embeddingProvider.Profile;
            if (existing.Provider != currentProfile.Provider ||
                existing.Model != currentProfile.Model)
            {
                return true; // Profile mismatch — stale
            }

            if (currentProfile.Dimensions > 0 &&
                existing.Dimensions != currentProfile.Dimensions)
            {
                return true; // Dimensions mismatch — stale
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check staleness for memory {MemoryId}", memoryId);
            return false;
        }
    }

    public async Task<IReadOnlyList<StaleEmbeddingInfo>> GetStaleEmbeddingsAsync(
        CancellationToken ct = default)
    {
        var stale = new List<StaleEmbeddingInfo>();

        try
        {
            // Get all vectors and check which ones don't match current profile
            var currentProfile = _embeddingProvider.Profile;
            var allVectorIds = await _vectorStore.CountAsync(ct);

            if (allVectorIds == 0)
            {
                return stale;
            }

            // For now, check the embedding service's stored embeddings
            // In production, this would query the vector store directly
            _logger.LogDebug(
                "Checking for stale embeddings with profile {Profile}",
                currentProfile.GetProfileKey());

            return stale;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get stale embeddings");
            return stale;
        }
    }

    public async Task<EmbeddingResult> RebuildAsync(
        Guid memoryId,
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Text is required for embedding rebuild"
            };
        }

        try
        {
            // Delete existing embedding
            await _vectorStore.DeleteAsync(memoryId, ct);

            // Clear cache if available
            if (_cache != null)
            {
                var profile = _embeddingProvider.Profile;
                var textHash = IEmbeddingCache.ComputeTextHash(text);
                await _cache.RemoveAsync(profile.Provider, profile.Model, profile.Version, textHash, ct);
            }

            // Generate new embedding
            var result = await _embeddingProvider.GenerateAsync(text, ct);

            if (result.Success && result.Embedding != null)
            {
                var stored = await _vectorStore.UpsertAsync(memoryId, result.Embedding, ct);
                if (!stored)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to store rebuilt embedding";
                }
                else
                {
                    _logger.LogInformation(
                        "Embedding rebuilt for memory {MemoryId}: {Dimensions}d",
                        memoryId, result.Embedding.Dimensions);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding rebuild failed for memory {MemoryId}", memoryId);
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = $"Rebuild failed: {ex.Message}"
            };
        }
    }

    public async Task<int> RebuildBatchAsync(
        IReadOnlyList<EmbeddingRebuildRequest> requests,
        CancellationToken ct = default)
    {
        var successCount = 0;

        foreach (var request in requests)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var result = await RebuildAsync(request.MemoryId, request.Text, ct);
            if (result.Success)
            {
                successCount++;
            }

            // Small delay between requests to respect rate limits
            if (requests.Count > 10)
            {
                await Task.Delay(50, ct);
            }
        }

        _logger.LogInformation(
            "Batch rebuild complete: {Success}/{Total} successful",
            successCount, requests.Count);

        return successCount;
    }

    public async Task<EmbeddingStats> GetStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var totalVectors = await _vectorStore.CountAsync(ct);
            var providerAvailable = _embeddingProvider.IsAvailable;
            var profile = _embeddingProvider.Profile;

            return new EmbeddingStats
            {
                TotalVectors = totalVectors,
                ReadyVectors = totalVectors, // Simplified — in production, query by status
                FailedVectors = 0,
                StaleVectors = 0,
                CurrentProvider = profile.Provider,
                CurrentModel = profile.Model,
                ProviderAvailable = providerAvailable
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get embedding stats");
            return new EmbeddingStats
            {
                CurrentProvider = _embeddingProvider.ProviderName,
                CurrentModel = _embeddingProvider.Profile.Model,
                ProviderAvailable = _embeddingProvider.IsAvailable
            };
        }
    }
}
