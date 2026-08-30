using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// Orchestrates retrieval from both persistent memory and file-based knowledge,
/// with awareness of consolidated memories and owner context.
/// 
/// When knowledge documents have been consolidated into persistent memories,
/// the consolidated memories are preferred over raw knowledge results.
/// This avoids presenting duplicate context to the LLM.
/// </summary>
public class ContextRetrievalService : IMemoryRetriever
{
    private readonly IMemoryService _memoryService;
    private readonly KnowledgeService _knowledgeService;
    private readonly ILogger<ContextRetrievalService> _logger;

    public ContextRetrievalService(
        IMemoryService memoryService,
        KnowledgeService knowledgeService,
        ILogger<ContextRetrievalService> logger)
    {
        _memoryService = memoryService;
        _knowledgeService = knowledgeService;
        _logger = logger;
    }

    public async Task<MemoryRetrievalResult> RetrieveContextAsync(
        string query,
        string? project = null,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        return await RetrieveContextAsync(query, project, tags, ownerId: null, ct);
    }

    /// <summary>
    /// Retrieves context with owner context for persistent memory access.
    /// When ownerId is provided, persistent memories are included in results.
    /// When null, only knowledge documents are returned (legacy behavior).
    /// </summary>
    public async Task<MemoryRetrievalResult> RetrieveContextAsync(
        string query,
        string? project,
        List<string>? tags,
        string? ownerId,
        CancellationToken ct = default)
    {
        var memories = new List<MemoryDto>();
        var knowledgeResults = new List<SearchResult>();

        // Only search persistent memory when owner context is available
        if (!string.IsNullOrWhiteSpace(query) && !string.IsNullOrEmpty(ownerId))
        {
            try
            {
                memories = await _memoryService.SearchAsync(
                    query,
                    ownerId: ownerId,
                    scope: null,
                    projectId: null,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve persistent memory; continuing without it");
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            knowledgeResults = _knowledgeService.SearchDocuments(query, project, tags);
        }

        // Consolidation-aware deduplication:
        // If a knowledge result has been consolidated into a persistent memory,
        // prefer the memory (which has lifecycle, classification, and richer metadata).
        if (memories.Count > 0 && knowledgeResults.Count > 0)
        {
            knowledgeResults = FilterConsolidatedKnowledge(memories, knowledgeResults);
        }

        return new MemoryRetrievalResult
        {
            Memories = memories,
            KnowledgeResults = knowledgeResults
        };
    }

    /// <summary>
    /// Filters knowledge results that have been consolidated into persistent memory.
    /// A knowledge result is considered consolidated if a persistent memory exists
    /// with a Source field indicating it came from knowledge consolidation.
    /// 
    /// Detection strategy (in order of reliability):
    ///   1. Memory Source starts with "knowledge:" prefix
    ///   2. Memory has SupersedesId (consolidation via supersession)
    ///   3. Normalized content match between memory and knowledge document
    /// </summary>
    private static List<SearchResult> FilterConsolidatedKnowledge(
        List<MemoryDto> memories, List<SearchResult> knowledgeResults)
    {
        // Build set of knowledge sources represented in persistent memories
        var consolidatedSourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consolidatedNormalizedContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var memory in memories)
        {
            // Strategy 1: Parse Source field for "knowledge:" prefix
            if (!string.IsNullOrEmpty(memory.Source))
            {
                // Handle comma-separated sources from provenance merging
                foreach (var part in memory.Source.Split(',', StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase))
                    {
                        var sourceName = part["knowledge:".Length..].Trim();
                        if (!string.IsNullOrEmpty(sourceName))
                        {
                            consolidatedSourceNames.Add(sourceName);
                        }
                    }
                }
            }

            // Strategy 2: Track normalized content for fallback matching
            if (!string.IsNullOrEmpty(memory.Content))
            {
                var normalized = NormalizeForComparison(memory.Content);
                if (normalized.Length > 10) // Only track meaningful content
                {
                    consolidatedNormalizedContent.Add(normalized);
                }
            }
        }

        if (consolidatedSourceNames.Count == 0 && consolidatedNormalizedContent.Count == 0)
            return knowledgeResults;

        return knowledgeResults
            .Where(kr =>
            {
                // Check 1: Source name match (primary strategy)
                var docName = System.IO.Path.GetFileNameWithoutExtension(kr.FilePath);
                if (consolidatedSourceNames.Contains(docName))
                    return false;

                // Check 2: Normalized content match (fallback)
                if (!string.IsNullOrEmpty(kr.Content) && kr.Content.Length > 10)
                {
                    var normalized = NormalizeForComparison(kr.Content);
                    if (consolidatedNormalizedContent.Contains(normalized))
                        return false;
                }

                return true;
            })
            .ToList();
    }

    /// <summary>
    /// Normalizes content for comparison: lowercase, strip punctuation, collapse whitespace.
    /// Matches the normalization used by MemoryNormalizer and MemoryEntry.ComputeNormalizedContent.
    /// </summary>
    private static string NormalizeForComparison(string content)
    {
        var text = content.ToLowerInvariant();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
