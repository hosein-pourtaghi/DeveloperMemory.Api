using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents the complete prompt intelligence context assembled by the retrieval pipeline.
/// This is the foundation object that the future Prompt Intelligence Engine will consume.
/// It contains everything needed to understand what was retrieved and why,
/// without knowledge of how memories were stored.
/// </summary>
public class PromptContext
{
    /// <summary>
    /// The original user request or query that triggered retrieval.
    /// </summary>
    public string OriginalQuery { get; set; } = string.Empty;

    /// <summary>
    /// The project context for this request.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The workspace context for this request.
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// The user context.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Retrieved memories, ordered by relevance.
    /// </summary>
    public List<RetrievedMemory> RetrievedMemories { get; set; } = [];

    /// <summary>
    /// Applicable constraints or rules for this context.
    /// </summary>
    public List<string> Constraints { get; set; } = [];

    /// <summary>
    /// Metadata about the retrieval process.
    /// </summary>
    public RetrievalMetadata Metadata { get; set; } = new();

    /// <summary>
    /// The context token budget that was applied.
    /// </summary>
    public int ContextTokenBudget { get; set; }
}

/// <summary>
/// Metadata about the retrieval and context-building process.
/// Used for observability, diagnostics, and explainability.
/// </summary>
public class RetrievalMetadata
{
    /// <summary>
    /// How many candidate memories were found before filtering.
    /// </summary>
    public int CandidateCount { get; set; }

    /// <summary>
    /// How many memories passed eligibility filtering.
    /// </summary>
    public int EligibleCount { get; set; }

    /// <summary>
    /// How many memories were selected after budgeting.
    /// </summary>
    public int SelectedCount { get; set; }

    /// <summary>
    /// Total estimated tokens of selected memories.
    /// </summary>
    public int EstimatedTokensUsed { get; set; }

    /// <summary>
    /// How long the retrieval pipeline took (milliseconds).
    /// </summary>
    public double RetrievalDurationMs { get; set; }

    /// <summary>
    /// How long the ranking step took (milliseconds).
    /// </summary>
    public double RankingDurationMs { get; set; }

    /// <summary>
    /// How long the context-building step took (milliseconds).
    /// </summary>
    public double ContextBuildingDurationMs { get; set; }

    /// <summary>
    /// The retrieval provider used (e.g., "keyword", "embedding", "hybrid").
    /// </summary>
    public string RetrievalProvider { get; set; } = string.Empty;

    /// <summary>
    /// The scopes that were considered eligible for this retrieval.
    /// </summary>
    public List<MemoryScope> EligibleScopes { get; set; } = [];
}
