using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;

namespace DeveloperMemory.Api.Services;

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

        try
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                memories = await _memoryService.SearchAsync(
                    query,
                    ownerId: string.Empty,  // Legacy path: no owner context
                    scope: null,
                    projectId: null,
                    ct: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve persistent memory; continuing without it");
        }

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
