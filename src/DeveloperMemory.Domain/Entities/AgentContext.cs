using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents the resolved execution context of an agent making a request.
/// Deterministic and side-effect free — no database or network calls.
/// Not persisted to PostgreSQL — this is an in-request context model.
/// </summary>
public class AgentContext
{
    /// <summary>The type of agent making the request.</summary>
    public AgentType AgentType { get; set; } = AgentType.General;

    /// <summary>The intent of the task being performed.</summary>
    public TaskIntent TaskIntent { get; set; } = TaskIntent.Query;

    /// <summary>The project context, if available.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The workspace context, if available.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>The user context, if available.</summary>
    public string? UserId { get; set; }

    /// <summary>The resolved memory scope based on context.</summary>
    public MemoryScope ResolvedScope { get; set; } = MemoryScope.Global;

    /// <summary>Whether this context was explicitly provided by the caller.</summary>
    public bool IsExplicit { get; set; }

    /// <summary>Timestamp of context resolution.</summary>
    public DateTime ResolvedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Determines the effective memory scope from the available context.
    /// Explicit context always wins over inferred context.
    /// </summary>
    public static MemoryScope ResolveScope(
        Guid? projectId = null,
        string? workspaceId = null,
        string? userId = null)
    {
        // Most specific scope wins
        if (!string.IsNullOrEmpty(userId))
            return MemoryScope.Private;
        if (!string.IsNullOrEmpty(workspaceId))
            return MemoryScope.Workspace;
        if (projectId.HasValue)
            return MemoryScope.Project;
        return MemoryScope.Global;
    }
}
