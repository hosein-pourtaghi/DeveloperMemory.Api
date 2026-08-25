using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents a structured request to the centralized retrieval pipeline.
/// </summary>
public class RetrievalRequest
{
    /// <summary>
    /// The user or caller performing the retrieval.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The current project context. Memories scoped to this project are eligible.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The current workspace context. Workspace-scoped memories must match.
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// The search query for text/keyword matching.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Explicit scope filter. If null, all scopes are considered (subject to privacy rules).
    /// </summary>
    public List<MemoryScope>? RequestedScopes { get; set; }

    /// <summary>
    /// Maximum number of memories to return.
    /// </summary>
    public int MaximumResults { get; set; } = 20;

    /// <summary>
    /// Approximate token/character budget for the context block.
    /// </summary>
    public int ContextTokenBudget { get; set; } = 2000;

    /// <summary>
    /// If set, only memories in these categories are eligible.
    /// </summary>
    public List<string>? RequiredCategories { get; set; }

    /// <summary>
    /// Memories in these categories are excluded from results.
    /// </summary>
    public List<string>? ExcludedCategories { get; set; }
}
