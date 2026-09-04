using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// V2 context foundation: assembles a unified agent context from runtime
/// request information and persistent intelligence.
///
/// The assembler makes the V2 boundary explicit:
///   - Runtime context  — information relevant only to the current execution
///     (request text, conversation, active project/workspace identity, caller
///     supplied instructions). It is never persisted.
///   - Persistent intelligence — information that survives requests and can
///     influence future reasoning (retrieved memories, project knowledge).
///     Every persistent item retains provenance (memory id, scope, source,
///     project/workspace, confidence, relevance).
///
/// Assembly is deterministic and never calls an LLM. It delegates memory
/// retrieval to the existing pipeline (scope resolution, privacy/isolation,
/// lifecycle filtering, relevance ranking, budgeting) and reuses
/// IProjectContextProvider for persistent project knowledge. No memory or
/// runtime context is persisted by this service.
/// </summary>
public interface IContextAssemblyService
{
    /// <summary>
    /// Assembles a unified agent context for the given runtime request.
    /// Combines the runtime request with relevant persistent intelligence
    /// (memories + project knowledge) without conflating the two.
    /// </summary>
    Task<UnifiedAgentContext> AssembleAsync(
        UnifiedContextRequest request,
        string ownerId,
        CancellationToken ct = default);
}

/// <summary>
/// Runtime request boundary for V2 context assembly.
/// Everything in this request describes the current execution only and is
/// never treated as persistent intelligence.
/// </summary>
public class UnifiedContextRequest
{
    /// <summary>Current task/request text. Required.</summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>Optional explicit retrieval query. Derived from Task when absent.</summary>
    public string? Query { get; set; }

    /// <summary>Active project identity for this request (null when no project context).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Active workspace identity for this request (null when no workspace context).</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Tags for scoped retrieval.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Instructions/constraints explicitly supplied by the caller for this request.</summary>
    public List<string>? Constraints { get; set; }

    /// <summary>Conversation history for the current conversation.</summary>
    public List<string>? ConversationHistory { get; set; }

    /// <summary>Optional calling-agent identifier. The context foundation is agent-agnostic.</summary>
    public string? AgentId { get; set; }

    /// <summary>Optional explicit agent type. Ignored when AgentId is absent.</summary>
    public AgentType? AgentType { get; set; }

    /// <summary>Maximum memories to return.</summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>Token budget applied to retrieved memory context.</summary>
    public int ContextTokenBudget { get; set; } = 4000;
}

/// <summary>
/// The runtime component of a unified agent context.
/// Information relevant only to the current execution — never persisted and
/// never conflated with persistent intelligence.
/// </summary>
public class RuntimeContext
{
    /// <summary>The current request/task text.</summary>
    public string Request { get; set; } = string.Empty;

    /// <summary>The retrieval query actually used (explicit or derived).</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Server-controlled owner identity for all retrieval.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>The requesting user identity (Private scope matching).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Active project identity (null when not in a project context).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Active workspace identity (null when not in a workspace context).</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Optional calling-agent identity.</summary>
    public string? AgentId { get; set; }

    /// <summary>Optional resolved agent type.</summary>
    public AgentType? AgentType { get; set; }

    /// <summary>Conversation history for the current conversation.</summary>
    public List<string> ConversationHistory { get; set; } = [];

    /// <summary>Instructions/constraints explicitly supplied for this request.</summary>
    public List<string> ExplicitInstructions { get; set; } = [];

    /// <summary>Tags supplied for scoped retrieval.</summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// The persistent component of a unified agent context.
/// Information that survives requests and can influence future reasoning.
/// Every memory retains full provenance (MemoryId, Scope, Source,
/// ProjectId/WorkspaceId, Confidence, RelevanceScore, EligibilityReason).
/// </summary>
public class PersistentContext
{
    /// <summary>Relevant memories from the existing retrieval pipeline (ranked, budgeted, deduplicated).</summary>
    public List<RetrievedMemory> Memories { get; set; } = [];

    /// <summary>Persistent project knowledge (rules, stack, conventions, decisions) for the active project.</summary>
    public ProjectContext? ProjectKnowledge { get; set; }

    /// <summary>True when no persistent intelligence was available for this request.</summary>
    public bool IsEmpty => Memories.Count == 0 && ProjectKnowledge == null;
}

/// <summary>
/// Unified agent context produced by V2 context assembly.
///
/// Runtime request + Persistent Intelligence + Project Context + Workspace Context
/// + Relevant Memory = Unified Agent Context
///
/// Runtime and persistent information are kept in separate partitions so that
/// downstream orchestrators/agents can consume them without confusing
/// persistent memory with transient runtime state. Provenance for persistent
/// content is preserved on every RetrievedMemory item.
/// </summary>
public class UnifiedAgentContext
{
    /// <summary>Runtime context for the current execution.</summary>
    public RuntimeContext Runtime { get; set; } = new();

    /// <summary>Relevant persistent intelligence.</summary>
    public PersistentContext Persistent { get; set; } = new();

    /// <summary>Report of how the context was assembled (limits, provenance, suppression).</summary>
    public ContextAssemblyReport Assembly { get; set; } = new();

    /// <summary>True when no persistent intelligence was available.</summary>
    public bool IsEmpty => Persistent.IsEmpty;
}

/// <summary>
/// Deterministic report of how a unified agent context was assembled.
/// Provides observability over limits, provenance, and duplicate suppression
/// without exposing internal pipeline details.
/// </summary>
public class ContextAssemblyReport
{
    /// <summary>Scopes considered eligible for this context (from ScopeResolver).</summary>
    public List<MemoryScope> EligibleScopes { get; set; } = [];

    /// <summary>Candidate memories considered before filtering.</summary>
    public int CandidatesConsidered { get; set; }

    /// <summary>Memories eligible after privacy/lifecycle filtering.</summary>
    public int EligibleCount { get; set; }

    /// <summary>Memories selected after ranking and budgeting.</summary>
    public int SelectedCount { get; set; }

    /// <summary>Near-duplicate memories suppressed during assembly.</summary>
    public int DuplicatesSuppressed { get; set; }

    /// <summary>Memory ids suppressed as duplicates (provenance).</summary>
    public List<Guid> SuppressedMemoryIds { get; set; } = [];

    /// <summary>Estimated tokens consumed by the persistent component.</summary>
    public int EstimatedTokensUsed { get; set; }

    /// <summary>Maximum results limit applied.</summary>
    public int MaximumResults { get; set; }

    /// <summary>Token budget applied.</summary>
    public int TokenBudget { get; set; }

    /// <summary>Whether persistent project knowledge was included.</summary>
    public bool ProjectKnowledgeIncluded { get; set; }

    /// <summary>Non-fatal warnings produced during assembly.</summary>
    public List<string> Warnings { get; set; } = [];
}
