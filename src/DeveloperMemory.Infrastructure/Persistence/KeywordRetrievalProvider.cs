using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// Keyword-based memory retrieval provider.
/// Uses PostgreSQL text search (ILIKE/Contains) for candidate retrieval.
/// 
/// Filtering is performed at the database level where possible (scope, project, workspace, user, state).
/// Application-level filtering (PrivacyFilter, LifecycleFilter) provides defense-in-depth.
/// </summary>
public class KeywordRetrievalProvider : IMemoryRetrievalProvider
{
    private readonly DeveloperMemoryDbContext _context;

    public KeywordRetrievalProvider(DeveloperMemoryDbContext context)
    {
        _context = context;
    }

    public string ProviderName => "keyword";

    public async Task<List<MemoryEntry>> GetCandidatesAsync(
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        // Determine eligible scopes
        var eligibleScopes = GetEligibleScopes(request);

        var query = _context.MemoryEntries
            .AsNoTracking()
            .Where(e => e.State != MemoryState.Deleted);

        // ── Scope filtering (DB level) ──
        if (request.RequestedScopes != null && request.RequestedScopes.Count > 0)
        {
            var requestedScopes = request.RequestedScopes;
            query = query.Where(e => requestedScopes.Contains(e.Scope));
        }
        else
        {
            query = query.Where(e => eligibleScopes.Contains(e.Scope));
        }

        // ── Project isolation (DB level) ──
        if (request.ProjectId.HasValue)
        {
            query = query.Where(e =>
                e.Scope != MemoryScope.Project || e.ProjectId == request.ProjectId.Value);
        }
        else
        {
            query = query.Where(e => e.Scope != MemoryScope.Project);
        }

        // ── Workspace isolation (DB level) ──
        if (!string.IsNullOrEmpty(request.WorkspaceId))
        {
            // Workspace memories must match the workspace ID
            query = query.Where(e =>
                e.Scope != MemoryScope.Workspace ||
                e.WorkspaceId == request.WorkspaceId);
        }
        else
        {
            // No workspace context — exclude workspace-scoped memories
            query = query.Where(e => e.Scope != MemoryScope.Workspace);
        }

        // ── Private/User isolation (DB level) ──
        if (!string.IsNullOrEmpty(request.UserId))
        {
            // Private memories must belong to the requesting user
            query = query.Where(e =>
                e.Scope != MemoryScope.Private ||
                e.UserId == request.UserId);
        }
        else
        {
            // No user context — exclude private memories
            query = query.Where(e => e.Scope != MemoryScope.Private);
        }

        // ── Keyword search (DB level) ──
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var queryLower = request.Query.ToLowerInvariant();
            query = query.Where(e =>
                e.Title.ToLower().Contains(queryLower) ||
                e.Content.ToLower().Contains(queryLower) ||
                (e.TagsJson != null && e.TagsJson.ToLower().Contains(queryLower)));
        }

        // Order by importance and recency for initial candidate set
        var candidates = await query
            .OrderByDescending(e => e.Importance)
            .ThenByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);

        // ── Category filtering (in-memory for JSON tag matching) ──
        if (request.ExcludedCategories != null && request.ExcludedCategories.Count > 0)
        {
            candidates = candidates.Where(e =>
                !e.Tags.Any(t => request.ExcludedCategories.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

        if (request.RequiredCategories != null && request.RequiredCategories.Count > 0)
        {
            candidates = candidates.Where(e =>
                e.Tags.Any(t => request.RequiredCategories.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

        return candidates;
    }

    private static List<MemoryScope> GetEligibleScopes(RetrievalRequest request)
    {
        var scopes = new List<MemoryScope> { MemoryScope.Global };

        if (request.ProjectId.HasValue)
            scopes.Add(MemoryScope.Project);

        if (!string.IsNullOrEmpty(request.WorkspaceId))
            scopes.Add(MemoryScope.Workspace);

        if (!string.IsNullOrEmpty(request.UserId))
            scopes.Add(MemoryScope.Private);

        return scopes;
    }
}
