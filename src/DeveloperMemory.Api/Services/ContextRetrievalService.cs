using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// Implements IMemoryRetriever by orchestrating retrieval from persistent memory
/// (via IMemoryService) and file-based knowledge documents (via KnowledgeService).
/// This replaces the retrieval logic previously embedded in the gateway controller.
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
        var memories = new List<MemoryDto>();
        var knowledgeResults = new List<SearchResult>();

        // Retrieve persistent memory entries — failures are non-fatal
        try
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                memories = await _memoryService.SearchAsync(
                    query,
                    scope: null,  // search all scopes
                    projectId: null,
                    ct: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve persistent memory; continuing without it");
        }

        // Retrieve knowledge documents — synchronous in-memory search
        if (!string.IsNullOrWhiteSpace(query))
        {
            knowledgeResults = _knowledgeService.SearchDocuments(query, project, tags);
        }

        return new MemoryRetrievalResult
        {
            Memories = memories,
            KnowledgeResults = knowledgeResults
        };
    }
}
