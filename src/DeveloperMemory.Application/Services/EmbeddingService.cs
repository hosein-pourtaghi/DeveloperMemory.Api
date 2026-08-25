using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Manages embeddings: generation, storage, retrieval, and rebuilding.
/// Resilient to provider failures — never prevents memory operations.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingProvider _provider;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IEmbeddingProvider provider,
        IVectorStore vectorStore,
        ILogger<EmbeddingService> logger)
    {
        _provider = provider;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public bool IsSemanticAvailable => _provider.IsAvailable && _vectorStore.IsAvailable;

    public async Task<EmbeddingResult> GenerateAndStoreAsync(
        Guid memoryId,
        string text,
        CancellationToken ct = default)
    {
        if (!IsSemanticAvailable)
        {
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Semantic provider unavailable"
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Text is required"
            };
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var result = await _provider.GenerateAsync(text, ct);
            result.GenerationDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (result.Success && result.Embedding != null)
            {
                var stored = await _vectorStore.UpsertAsync(memoryId, result.Embedding, ct);
                if (!stored)
                {
                    _logger.LogWarning("Failed to store embedding for memory {MemoryId}", memoryId);
                    result.Success = false;
                    result.ErrorMessage = "Vector store upsert failed";
                }
                else
                {
                    _logger.LogDebug(
                        "Embedding generated and stored for memory {MemoryId}: {Dimensions}d, {Duration}ms",
                        memoryId, result.Embedding.Dimensions, result.GenerationDurationMs);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed for memory {MemoryId}", memoryId);
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = $"Embedding generation failed: {ex.Message}"
            };
        }
    }

    public async Task<Domain.Entities.Embedding?> GetAsync(
        Guid memoryId,
        CancellationToken ct = default)
    {
        if (!IsSemanticAvailable) return null;

        try
        {
            return await _vectorStore.GetAsync(memoryId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get embedding for memory {MemoryId}", memoryId);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(
        Guid memoryId,
        CancellationToken ct = default)
    {
        if (!IsSemanticAvailable) return false;

        try
        {
            return await _vectorStore.DeleteAsync(memoryId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete embedding for memory {MemoryId}", memoryId);
            return false;
        }
    }

    public async Task<EmbeddingResult> RebuildAsync(
        Guid memoryId,
        string text,
        CancellationToken ct = default)
    {
        // Delete existing embedding first
        await DeleteAsync(memoryId, ct);

        // Generate and store new embedding
        return await GenerateAndStoreAsync(memoryId, text, ct);
    }
}
