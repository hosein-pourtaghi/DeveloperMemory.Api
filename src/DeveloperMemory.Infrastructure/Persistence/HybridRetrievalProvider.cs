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
        var candidates = await GetScoredCandidatesAsync(request, ct);
        return candidates.Select(candidate => candidate.Memory).ToList();
    }

    public async Task<List<RetrievalCandidate>> GetScoredCandidatesAsync(
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        var lexicalCandidates = new List<RetrievalCandidate>();
        var semanticCandidates = new List<RetrievalCandidate>();

        // Run lexical retrieval (always available)
        try
        {
            lexicalCandidates = await _lexicalProvider.GetScoredCandidatesAsync(request, ct);
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
                semanticCandidates = await _semanticProvider.GetScoredCandidatesAsync(request, ct);
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
    /// Lexical metadata is retained while semantic scores are added when available.
    /// </summary>
    private static List<RetrievalCandidate> MergeCandidates(
        List<RetrievalCandidate> lexical,
        List<RetrievalCandidate> semantic)
    {
        var byId = new Dictionary<Guid, RetrievalCandidate>();
        var result = new List<RetrievalCandidate>();

        foreach (var candidate in lexical)
        {
            if (byId.TryAdd(candidate.Memory.Id, candidate))
            {
                result.Add(candidate);
            }
        }

        foreach (var candidate in semantic)
        {
            if (byId.TryGetValue(candidate.Memory.Id, out var existing))
            {
                if (candidate.SemanticScore.HasValue)
                {
                    existing.SemanticScore = candidate.SemanticScore;
                }
            }
            else
            {
                byId.Add(candidate.Memory.Id, candidate);
                result.Add(candidate);
            }
        }

        return result;
    }
}
