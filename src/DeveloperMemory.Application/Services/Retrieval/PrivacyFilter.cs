using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Enforces privacy and isolation rules for memory retrieval.
/// A memory must never be returned merely because it is textually relevant —
/// it must first be eligible for the current context.
/// 
/// Privacy rules:
///   Global   — available across project contexts (subject to user model)
///   Project  — only when ProjectId matches the current context
///   Workspace — only when WorkspaceId matches the current context
///   Private  — only when UserId matches the requesting user
/// </summary>
public static class PrivacyFilter
{
    /// <summary>
    /// Filters memories by privacy and project isolation rules.
    /// Returns only memories that are eligible for the given context.
    /// </summary>
    public static List<(MemoryEntry Memory, string EligibilityReason)> FilterByPrivacy(
        List<MemoryEntry> memories,
        RetrievalRequest request,
        List<MemoryScope> eligibleScopes)
    {
        var results = new List<(MemoryEntry Memory, string EligibilityReason)>();

        foreach (var memory in memories)
        {
            // Rule 0: Owner isolation — mandatory, fail closed
            if (string.IsNullOrEmpty(request.OwnerId))
            {
                continue; // No owner context = no results
            }
            if (!string.Equals(memory.OwnerId, request.OwnerId, StringComparison.Ordinal))
            {
                continue;
            }

            // Rule 1: Scope must be eligible
            if (!eligibleScopes.Contains(memory.Scope))
            {
                continue;
            }

            // Rule 2: Project isolation — project-scoped memories must match project context
            if (memory.Scope == MemoryScope.Project)
            {
                if (!request.ProjectId.HasValue || memory.ProjectId != request.ProjectId.Value)
                {
                    continue;
                }
            }

            // Rule 3: Workspace isolation — workspace-scoped memories must match workspace identity
            if (memory.Scope == MemoryScope.Workspace)
            {
                if (string.IsNullOrEmpty(request.WorkspaceId))
                {
                    continue;
                }

                // Match the memory's stored WorkspaceId against the request's WorkspaceId
                if (!string.IsNullOrEmpty(memory.WorkspaceId) &&
                    !string.Equals(memory.WorkspaceId, request.WorkspaceId, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            // Rule 4: Private/User isolation — private memories belong to a specific user
            if (memory.Scope == MemoryScope.Private)
            {
                if (string.IsNullOrEmpty(request.UserId))
                {
                    continue;
                }

                // Match the memory's stored UserId against the request's UserId
                if (!string.IsNullOrEmpty(memory.UserId) &&
                    !string.Equals(memory.UserId, request.UserId, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            // Rule 5: Category exclusion
            if (request.ExcludedCategories != null && request.ExcludedCategories.Count > 0)
            {
                var memoryTags = memory.Tags;
                if (memoryTags.Any(t => request.ExcludedCategories.Contains(t, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            // Rule 6: Category inclusion (if required categories specified)
            if (request.RequiredCategories != null && request.RequiredCategories.Count > 0)
            {
                var memoryTags = memory.Tags;
                if (!memoryTags.Any(t => request.RequiredCategories.Contains(t, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            // Memory is eligible
            var reason = BuildEligibilityReason(memory.Scope, request);
            results.Add((memory, reason));
        }

        return results;
    }

    private static string BuildEligibilityReason(MemoryScope scope, RetrievalRequest request)
    {
        return scope switch
        {
            MemoryScope.Global => "Global scope — available across all project contexts",
            MemoryScope.Project when request.ProjectId.HasValue =>
                $"Project scope — matches project context {request.ProjectId}",
            MemoryScope.Workspace when !string.IsNullOrEmpty(request.WorkspaceId) =>
                $"Workspace scope — matches workspace context {request.WorkspaceId}",
            MemoryScope.Private when !string.IsNullOrEmpty(request.UserId) =>
                $"Private scope — belongs to user {request.UserId}",
            _ => "Eligible"
        };
    }
}
