using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// A memory that has been retrieved and scored by the retrieval pipeline.
/// Contains enough information for the Prompt Intelligence Engine
/// to understand what was retrieved, why it was eligible, and how relevant it is.
/// </summary>
public class RetrievedMemory
{
    /// <summary>
    /// The unique identifier of the source memory.
    /// </summary>
    public Guid MemoryId { get; set; }

    /// <summary>
    /// The memory title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The memory content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The scope of the memory.
    /// </summary>
    public MemoryScope Scope { get; set; }

    /// <summary>
    /// Category or tags associated with the memory.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// The lifecycle state of the memory.
    /// </summary>
    public MemoryState State { get; set; }

    /// <summary>
    /// The project this memory belongs to (null for global memories).
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The workspace this memory belongs to (null for non-workspace memories).
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// The user this private memory belongs to (null for non-private memories).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Data classification level.
    /// </summary>
    public DataClassification Classification { get; set; }

    /// <summary>
    /// How important this memory is (0.0 to 1.0).
    /// </summary>
    public double Importance { get; set; }

    /// <summary>
    /// The source or origin of this memory.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Tags associated with the memory.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// When the memory was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The type of memory (Code, Rule, Context, etc.).
    /// </summary>
    public MemoryType MemoryType { get; set; }

    /// <summary>
    /// Confidence score for this retrieval (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Optional semantic similarity supplied by a semantic retrieval provider.
    /// </summary>
    public double? SemanticRelevanceScore { get; set; }

    /// <summary>
    /// The final relevance score after ranking.
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// The component scores that contributed to the final relevance score.
    /// </summary>
    public RetrievalScoreBreakdown ScoreBreakdown { get; set; } = new();

    /// <summary>
    /// Why this memory was eligible for retrieval.
    /// </summary>
    public string EligibilityReason { get; set; } = string.Empty;

    /// <summary>
    /// Estimated tokens this memory would consume in context.
    /// </summary>
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// Breakdown of scoring components for explainability.
/// </summary>
public class RetrievalScoreBreakdown
{
    /// <summary>
    /// Score from text/keyword relevance to the query.
    /// </summary>
    public double TextRelevance { get; set; }

    /// <summary>
    /// Similarity score supplied by semantic retrieval, when available.
    /// </summary>
    public double SemanticRelevance { get; set; }

    /// <summary>
    /// Score from scope relevance (Global > Project > Workspace > Private).
    /// </summary>
    public double ScopeRelevance { get; set; }

    /// <summary>
    /// Score from project context match.
    /// </summary>
    public double ProjectRelevance { get; set; }

    /// <summary>
    /// Score from recency (more recent = higher).
    /// </summary>
    public double RecencyScore { get; set; }

    /// <summary>
    /// Score from importance rating.
    /// </summary>
    public double ImportanceScore { get; set; }

    /// <summary>
    /// Score from category/tag match.
    /// </summary>
    public double CategoryScore { get; set; }
}
