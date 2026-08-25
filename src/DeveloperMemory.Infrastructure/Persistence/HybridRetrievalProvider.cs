using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// Hybrid retrieval provider combining lexical and semantic search.
/// 
/// Pipeline:
///   1. Run lexical retrieval (keyword-based)
///   2. Run semantic retrieval (embedding-based) if available
///   3. Merge candidates, deduplicating by memory ID
///   4. Return combined candidates for ranking
/// 
/// Fallback: If semantic retrieval fails, returns lexical-only results.
/// </summary>
public class HybridRetrievalProvider : IMemoryRetrievalProvider
{
    private readonly IMemoryRetrievalProvider _lexicalProvider;
    private readonly IMemoryRetrievalProvider _semanticProvider;
    private readonly ILogger<HybridRetrievalProvider> _logger;

    public HybridRetrievalProvider(
        KeywordRetrievalProvider lexicalProvider,
        SemanticRetrievalProvider semanticProvider,
        ILogger<HybridRetrievalProvider> logger)
    {
        _lexicalProvider = lexicalProvider;
        _semanticProvider = semanticProvider;
        _logger = logger;
    }

    public string ProviderName => "hybrid";

    public bool IsAvailable => _lexicalProvider.IsAvailable;

    public async Task<List<MemoryEntry>> GetCandidatesAsync(
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        var lexicalCandidates = new List<MemoryEntry>();
        var semanticCandidates = new List<MemoryEntry>();

        // Run lexical retrieval (always available)
        try
        {
            lexicalCandidates = await _lexicalProvider.GetCandidatesAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lexical retrieval failed");
        }

        // Run semantic retrieval (optional, may fail)
        if (_semanticProvider.IsAvailable)
        {
            try
            {
                semanticCandidates = await _semanticProvider.GetCandidatesAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Semantic retrieval failed; falling back to lexical only");
            }
        }

        // Merge candidates, deduplicating by memory ID
        var merged = MergeCandidates(lexicalCandidates, semanticCandidates);

        _logger.LogDebug(
            "Hybrid retrieval: {Lexical} lexical + {Semantic} semantic = {Merged} merged candidates",
            lexicalCandidates.Count, semanticCandidates.Count, merged.Count);

        return merged;
    }

    /// <summary>
    /// Merges lexical and semantic candidates, deduplicating by memory ID.
    /// Lexical results are prioritized (appear first) when both sources match.
    /// </summary>
    private static List<MemoryEntry> MergeCandidates(
        List<MemoryEntry> lexical,
        List<MemoryEntry> semantic)
    {
        var seen = new HashSet<Guid>();
        var result = new List<MemoryEntry>();

        // Add lexical candidates first (higher priority)
        foreach (var memory in lexical)
        {
            if (seen.Add(memory.Id))
            {
                result.Add(memory);
            }
        }

        // Add semantic candidates that weren't already found lexically
        foreach (var memory in semantic)
        {
            if (seen.Add(memory.Id))
            {
                result.Add(memory);
            }
        }

        return result;
    }
}
