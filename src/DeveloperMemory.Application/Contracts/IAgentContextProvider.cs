using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Resolves agent context from request data. Deterministic, no LLM calls.
/// Used to enrich memory retrieval with agent-aware relevance signals.
/// </summary>
public interface IAgentContextProvider
{
    /// <summary>
    /// Resolves the full agent context from a context request.
    /// All fields except AgentId are optional — the provider infers what it can.
    /// </summary>
    AgentContext Resolve(AgentContextRequest request);
}

/// <summary>
/// Agent context resolved from request data.
/// This is the input to agent-aware retrieval — it enriches the existing
/// RetrievalRequest with agent-specific signals without replacing it.
/// </summary>
public class AgentContext
{
    /// <summary>Agent identifier (e.g., "coding-agent-1", "cursor", "copilot").</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Agent type/classification for task-appropriate memory selection.</summary>
    public AgentType AgentType { get; set; } = AgentType.General;

    /// <summary>Current task description or intent.</summary>
    public string TaskDescription { get; set; } = string.Empty;

    /// <summary>Detected task intent category.</summary>
    public TaskIntent TaskIntent { get; set; } = TaskIntent.General;

    /// <summary>Project context (explicit or inferred).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Workspace context.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Tags provided by the agent for scoped retrieval.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Explicit constraints from the agent request.</summary>
    public List<string> Constraints { get; set; } = [];

    /// <summary>Conversation history for context-aware retrieval.</summary>
    public List<string> ConversationHistory { get; set; } = [];

    /// <summary>Whether the project was explicitly provided vs inferred.</summary>
    public bool ProjectExplicit { get; set; }

    /// <summary>Confidence in the resolved context (0.0-1.0).</summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>Human-readable explanation of how context was resolved.</summary>
    public string ResolutionExplanation { get; set; } = string.Empty;
}

/// <summary>
/// Agent type classification for task-appropriate memory selection.
/// Different agent types prioritize different memory categories.
/// </summary>
public enum AgentType
{
    /// <summary>General-purpose agent (default).</summary>
    General,

    /// <summary>Coding agent — prioritizes technical decisions, architecture, conventions.</summary>
    Coding,

    /// <summary>Documentation agent — prioritizes project knowledge, terminology, conventions.</summary>
    Documentation,

    /// <summary>Planning agent — prioritizes goals, constraints, historical decisions.</summary>
    Planning,

    /// <summary>Testing agent — prioritizes test conventions, quality patterns.</summary>
    Testing,

    /// <summary>DevOps agent — prioritizes deployment, infrastructure, operations.</summary>
    DevOps
}

/// <summary>
/// Task intent classification derived from the agent's request.
/// Used to select relevant memory categories and boost appropriate memory types.
/// </summary>
public enum TaskIntent
{
    /// <summary>General/unclassified intent.</summary>
    General,

    /// <summary>Implementing or building something.</summary>
    Implement,

    /// <summary>Debugging or fixing an issue.</summary>
    Debug,

    /// <summary>Architecture or design decision.</summary>
    Architecture,

    /// <summary>Memory/information capture.</summary>
    MemoryCapture,

    /// <summary>Querying or retrieving information.</summary>
    Query,

    /// <summary>Documentation or explanation.</summary>
    Documentation,

    /// <summary>Testing or validation.</summary>
    Testing,

    /// <summary>Deployment or operations.</summary>
    Deployment,

    /// <summary>Refactoring or improvement.</summary>
    Refactor
}

/// <summary>
/// Request to resolve agent context. All fields optional except AgentId.
/// </summary>
public class AgentContextRequest
{
    /// <summary>Agent identifier.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Agent type hint (optional — can be inferred).</summary>
    public AgentType? AgentType { get; set; }

    /// <summary>Current task description.</summary>
    public string? Task { get; set; }

    /// <summary>Explicit project ID.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Explicit workspace ID.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Tags for scoped retrieval.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Explicit constraints.</summary>
    public List<string>? Constraints { get; set; }

    /// <summary>Conversation history.</summary>
    public List<string>? ConversationHistory { get; set; }
}
