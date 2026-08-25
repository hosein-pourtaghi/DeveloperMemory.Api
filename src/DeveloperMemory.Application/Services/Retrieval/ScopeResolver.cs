using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Resolves which memory scopes are eligible for a given retrieval request.
/// This is the first stage of the retrieval pipeline.
/// </summary>
public static class ScopeResolver
{
    /// <summary>
    /// Returns the set of memory scopes eligible for the given request context.
    /// </summary>
    public static List<MemoryScope> ResolveEligibleScopes(RetrievalRequest request)
    {
        var scopes = new List<MemoryScope>();

        // Global memories are always eligible
        scopes.Add(MemoryScope.Global);

        // Project memories are eligible when a project context is provided
        if (request.ProjectId.HasValue)
        {
            scopes.Add(MemoryScope.Project);
        }

        // Workspace memories are eligible when a workspace context is provided
        if (!string.IsNullOrEmpty(request.WorkspaceId))
        {
            scopes.Add(MemoryScope.Workspace);
        }

        // Private memories are eligible when a user context is provided
        if (!string.IsNullOrEmpty(request.UserId))
        {
            scopes.Add(MemoryScope.Private);
        }

        // If explicit scopes were requested, intersect with eligible scopes
        if (request.RequestedScopes != null && request.RequestedScopes.Count > 0)
        {
            scopes = scopes.Where(s => request.RequestedScopes.Contains(s)).ToList();
        }

        return scopes;
    }
}
