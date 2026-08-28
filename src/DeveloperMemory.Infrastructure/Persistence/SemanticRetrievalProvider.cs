using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// Semantic retrieval provider using embedding-based vector search.
/// Falls back gracefully when embedding provider or vector store is unavailable.
/// 
/// Retrieval pipeline:
///   1. Generate query embedding
///   2. Search vector store for similar memories
///   3. Load matching memories from database
///   4. Apply scope/project/workspace filtering
/// </summary>
public class SemanticRetrievalProvider : IMemoryRetrievalProvider
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<SemanticRetrievalProvider> _logger;

    public SemanticRetrievalProvider(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        DeveloperMemoryDbContext context,
        ILogger<SemanticRetrievalProvider> logger)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _context = context;
        _logger = logger;
    }

    public string ProviderName => "semantic";

    public bool IsAvailable => _embeddingProvider.IsAvailable && _vectorStore.IsAvailable;

    public async Task<List<MemoryEntry>> GetCandidatesAsync(
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Semantic provider unavailable, returning empty candidates");
            return [];
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return [];
        }

        try
        {
            // Step 1: Generate query embedding
            var embeddingResult = await _embeddingProvider.GenerateAsync(request.Query, ct);
            if (!embeddingResult.Success || embeddingResult.Embedding == null)
            {
                _logger.LogWarning("Query embedding failed: {Error}", embeddingResult.ErrorMessage);
                return [];
            }

            // Step 2: Search vector store
            var searchResults = await _vectorStore.SearchAsync(
                embeddingResult.Embedding.Values,
                maxResults: request.MaximumResults * 2, // Get extra candidates for filtering
                minimumScore: 0.1, // Low threshold — allow more candidates
                ct);

            if (searchResults.Count == 0)
            {
                return [];
            }

            // Step 3: Load matching memories from database (with owner filtering)
            var memoryIds = searchResults.Select(r => r.MemoryId).ToList();
            var memoriesQuery = _context.MemoryEntries
                .AsNoTracking()
                .Where(e => memoryIds.Contains(e.Id) && e.State != MemoryState.Deleted);

            // Owner isolation — mandatory at DB level, fail closed
            if (string.IsNullOrEmpty(request.OwnerId))
            {
                return [];
            }
            memoriesQuery = memoriesQuery.Where(e => e.OwnerId == request.OwnerId);

            var memories = await memoriesQuery.ToListAsync(ct);

            // Step 4: Apply scope/project/workspace filtering
            var filtered = ApplyScopeFilter(memories, request);

            _logger.LogDebug(
                "Semantic retrieval: {VectorResults} vector results, {Loaded} loaded, {Filtered} after scope filter",
                searchResults.Count, memories.Count, filtered.Count);

            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic retrieval failed");
            return [];
        }
    }

    private static List<MemoryEntry> ApplyScopeFilter(
        List<MemoryEntry> memories,
        RetrievalRequest request)
    {
        var result = new List<MemoryEntry>();

        foreach (var memory in memories)
        {
            // Global memories are always eligible
            if (memory.Scope == MemoryScope.Global)
            {
                result.Add(memory);
                continue;
            }

            // Project memories need matching project
            if (memory.Scope == MemoryScope.Project)
            {
                if (request.ProjectId.HasValue && memory.ProjectId == request.ProjectId.Value)
                {
                    result.Add(memory);
                }
                continue;
            }

            // Workspace memories need matching workspace
            if (memory.Scope == MemoryScope.Workspace)
            {
                if (!string.IsNullOrEmpty(request.WorkspaceId) &&
                    string.Equals(memory.WorkspaceId, request.WorkspaceId, StringComparison.Ordinal))
                {
                    result.Add(memory);
                }
                continue;
            }

            // Private memories need matching user
            if (memory.Scope == MemoryScope.Private)
            {
                if (!string.IsNullOrEmpty(request.UserId) &&
                    string.Equals(memory.UserId, request.UserId, StringComparison.Ordinal))
                {
                    result.Add(memory);
                }
                continue;
            }
        }

        return result;
    }
}
