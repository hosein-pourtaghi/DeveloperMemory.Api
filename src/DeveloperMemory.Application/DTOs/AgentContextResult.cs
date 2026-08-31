using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

/// <summary>
/// Result of agent-aware context retrieval.
/// Combines resolved agent context with ranked memories and context metadata.
/// </summary>
public class AgentContextResult
{
    /// <summary>The resolved agent context used for retrieval.</summary>
    public AgentContext AgentContext { get; set; } = null!;

    /// <summary>Ranked memories relevant to the agent's task.</summary>
    public List<RetrievedMemory> Memories { get; set; } = [];

    /// <summary>Context sections assembled for prompt enrichment.</summary>
    public List<AgentContextSection> ContextSections { get; set; } = [];

    /// <summary>Applicable instructions/constraints for this agent context.</summary>
    public List<string> Instructions { get; set; } = [];

    /// <summary>Retrieval metadata (timing, counts, etc.).</summary>
    public RetrievalMetadata Metadata { get; set; } = new();

    /// <summary>How many total candidates were considered.</summary>
    public int TotalCandidates { get; set; }

    /// <summary>How many memories were selected after ranking and budgeting.</summary>
    public int SelectedCount { get; set; }

    /// <summary>Estimated tokens used by selected memories.</summary>
    public int EstimatedTokensUsed { get; set; }
}

/// <summary>
/// A section of context assembled for the agent.
/// Structured representation of what context is available and why.
/// </summary>
public class AgentContextSection
{
    /// <summary>Section type (e.g., "project", "task", "preference", "constraint").</summary>
    public string SectionType { get; set; } = string.Empty;

    /// <summary>Section title for display.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Content of this context section.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Relevance score for this section (0.0-1.0).</summary>
    public double RelevanceScore { get; set; }

    /// <summary>Memory IDs that contributed to this section.</summary>
    public List<Guid> ContributingMemoryIds { get; set; } = [];
}

/// <summary>
/// Request for agent-aware context retrieval.
/// Extends basic retrieval with agent identity and task context.
/// </summary>
public class AgentContextRetrievalRequest
{
    /// <summary>Agent identifier.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Agent type (optional — inferred from AgentId if not provided).</summary>
    public AgentType? AgentType { get; set; }

    /// <summary>Current task description.</summary>
    public string? Task { get; set; }

    /// <summary>Search query (derived from task if not provided).</summary>
    public string? Query { get; set; }

    /// <summary>Explicit project ID.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Explicit workspace ID.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Tags for scoped retrieval.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Explicit constraints from the agent.</summary>
    public List<string>? Constraints { get; set; }

    /// <summary>Conversation history.</summary>
    public List<string>? ConversationHistory { get; set; }

    /// <summary>Maximum memories to return.</summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>Token budget for context.</summary>
    public int ContextTokenBudget { get; set; } = 4000;
}
