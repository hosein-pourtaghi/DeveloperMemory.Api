namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent abstraction for project/workspace context.
/// Supports architecture rules, technology stack, coding conventions, etc.
/// </summary>
public interface IProjectContextProvider
{
    /// <summary>
    /// Gets project context for the specified project.
    /// Returns null if no context is available.
    /// </summary>
    Task<ProjectContext?> GetContextAsync(
        Guid? projectId = null,
        string? workspaceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Whether project context is available.
    /// </summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Structured project context.
/// </summary>
public class ProjectContext
{
    /// <summary>The project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The workspace identifier.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Project name.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Architecture rules and constraints.</summary>
    public List<string> ArchitectureRules { get; set; } = [];

    /// <summary>Technology stack.</summary>
    public List<string> TechnologyStack { get; set; } = [];

    /// <summary>Coding conventions.</summary>
    public List<string> CodingConventions { get; set; } = [];

    /// <summary>Key architectural decisions.</summary>
    public List<string> ArchitecturalDecisions { get; set; } = [];

    /// <summary>Relevant project documents (summaries).</summary>
    public List<ProjectDocument> Documents { get; set; } = [];

    /// <summary>Estimated context tokens.</summary>
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// A project document summary.
/// </summary>
public class ProjectDocument
{
    /// <summary>Document title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Document content (may be truncated).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Document type (e.g., "architecture", "convention", "decision").</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Relevance score.</summary>
    public double RelevanceScore { get; set; }
}
