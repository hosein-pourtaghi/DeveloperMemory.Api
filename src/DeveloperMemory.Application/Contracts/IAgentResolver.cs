using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// V2-3 Agent boundary.
///
/// An Agent is a configured behavioral identity/capability boundary — it
/// defines WHO is speaking (stable identity) and HOW it should behave
/// (system/behavior instructions). It deliberately does NOT carry model
/// selection, tools, delegation, workflows, or credentials; those belong to
/// later V2 phases.
///
/// An Agent influences Assistant execution only through:
///   - its system/behavior instructions (separate from runtime context,
///     persistent intelligence, and the user request)
///   - an optional classification hint reused by the existing context
///     assembly boundary (AgentContextProvider / AgentType)
///
/// Agents are resolved by <see cref="IAgentResolver"/>; the resolution result
/// is applied by the Assistant orchestrator, which remains the single
/// orchestration engine.
/// </summary>
public class Agent
{
    /// <summary>Stable, unique agent identifier used for resolution.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Human-readable agent name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description of the agent's purpose/capabilities.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// System/behavior instructions governing this agent.
    /// These are instructions TO the model — never retrieved memory, never
    /// runtime context, never the user request.
    /// </summary>
    public string SystemInstructions { get; set; } = string.Empty;

    /// <summary>Whether this agent can execute. Disabled agents are rejected.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional classification hint reused by the existing context-assembly
    /// boundary (AgentType) to enrich memory retrieval when the request does
    /// not supply one.
    /// </summary>
    public AgentType? AgentType { get; set; }

    /// <summary>Optional metadata (future extensibility; not interpreted here).</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}

/// <summary>
/// Result of resolving an agent identifier.
/// </summary>
public class AgentResolution
{
    /// <summary>Resolution outcome.</summary>
    public AgentResolveStatus Status { get; set; }

    /// <summary>The resolved agent when <see cref="Status"/> is Resolved.</summary>
    public Agent? Agent { get; set; }
}

/// <summary>
/// Agent resolution outcomes. Unknown and Disabled are distinct so callers can
/// report clean, differentiated errors.
/// </summary>
public enum AgentResolveStatus
{
    /// <summary>The agent exists and is enabled.</summary>
    Resolved,

    /// <summary>No agent exists with the given identifier.</summary>
    Unknown,

    /// <summary>The agent exists but is disabled.</summary>
    Disabled
}

/// <summary>
/// Application-level agent resolution boundary.
/// Implementations are deterministic, provider-agnostic, and free of
/// persistence/HTTP concerns. A registry/configuration approach is the
/// expected implementation unless the architecture later requires persistence.
/// </summary>
public interface IAgentResolver
{
    /// <summary>
    /// Resolves an agent by identifier (case-insensitive).
    /// A null/empty identifier resolves to <see cref="AgentResolveStatus.Unknown"/>.
    /// </summary>
    AgentResolution Resolve(string? agentId);

    /// <summary>All registered agents (for diagnostics/administration).</summary>
    IReadOnlyList<Agent> GetAll();
}