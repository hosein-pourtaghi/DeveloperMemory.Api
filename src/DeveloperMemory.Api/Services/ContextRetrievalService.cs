using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Api.Services;

public class ContextRetrievalService : IMemoryRetriever
{
    private readonly IMemoryService _memoryService;
    private readonly ICurrentUser _currentUser;
    private readonly KnowledgeService _knowledgeService;
    private readonly ILogger<ContextRetrievalService> _logger;

    public ContextRetrievalService(
        IMemoryService memoryService,
        KnowledgeService knowledgeService,
        ICurrentUser currentUser,
        ILogger<ContextRetrievalService> logger)
    {
        _memoryService = memoryService;
        _knowledgeService = knowledgeService;
        _currentUser = currentUser;
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
                    ownerId: _currentUser.UserId,
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
