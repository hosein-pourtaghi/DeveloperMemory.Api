using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

/// <summary>
/// Structured query request for memory retrieval.
/// Uses POST to support complex filter contracts.
/// </summary>
public class QueryMemoryRequest
{
    /// <summary>The search query text.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Filter by memory scope.</summary>
    public MemoryScope? Scope { get; set; }

    /// <summary>Filter by project ID.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Filter by workspace ID.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Filter by user ID.</summary>
    public string? UserId { get; set; }

    /// <summary>Filter by memory type(s).</summary>
    public List<MemoryType>? MemoryTypes { get; set; }

    /// <summary>Filter by lifecycle state(s). Default: Active only.</summary>
    public List<MemoryState>? States { get; set; }

    /// <summary>Filter by tags.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Maximum number of results. Default: 20, Max: 100.</summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>Minimum relevance score (0.0-1.0).</summary>
    public double MinRelevanceScore { get; set; } = 0.0;

    /// <summary>Include expired memories in results.</summary>
    public bool IncludeExpired { get; set; } = false;

    /// <summary>Include superseded memories in results.</summary>
    public bool IncludeSuperseded { get; set; } = false;

    /// <summary>Include archived memories in results.</summary>
    public bool IncludeArchived { get; set; } = false;
}

/// <summary>
/// Result of a structured memory query.
/// </summary>
public class QueryMemoryResult
{
    /// <summary>The retrieved memories with relevance scores.</summary>
    public List<RankedMemoryDto> Memories { get; set; } = [];

    /// <summary>Total candidates before filtering.</summary>
    public int TotalCandidates { get; set; }

    /// <summary>Number of memories returned.</summary>
    public int ReturnedCount { get; set; }
}

/// <summary>
/// A memory with its relevance score for query results.
/// </summary>
public class RankedMemoryDto
{
    public MemoryDto Memory { get; set; } = null!;
    public double RelevanceScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}
