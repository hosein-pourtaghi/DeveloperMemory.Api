using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

/// <summary>
/// Request DTO for the centralized memory retrieval endpoint.
/// </summary>
public class RetrieveRequest
{
    /// <summary>
    /// The search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Retrieval strategy. Auto selects hybrid when semantic retrieval is available.
    /// </summary>
    public RetrievalMode Mode { get; set; } = RetrievalMode.Auto;

    /// <summary>
    /// The current project context.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The current workspace context.
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// The user performing the retrieval.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Limit results to these scopes. If null, all eligible scopes are considered.
    /// </summary>
    public List<MemoryScope>? RequestedScopes { get; set; }

    /// <summary>
    /// Maximum number of results. Defaults to 20.
    /// </summary>
    public int MaximumResults { get; set; } = 20;

    /// <summary>
    /// Approximate token budget. Defaults to 2000.
    /// </summary>
    public int ContextTokenBudget { get; set; } = 2000;

    /// <summary>
    /// If set, only memories in these categories are eligible.
    /// </summary>
    public List<string>? RequiredCategories { get; set; }

    /// <summary>
    /// Memories in these categories are excluded.
    /// </summary>
    public List<string>? ExcludedCategories { get; set; }
}
