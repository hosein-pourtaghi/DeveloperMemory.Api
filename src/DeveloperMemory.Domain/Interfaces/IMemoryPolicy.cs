using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Policy layer between extraction and persistence.
/// The LLM suggests candidates; the application decides.
/// This distinction is mandatory.
/// </summary>
public interface IMemoryPolicy
{
    /// <summary>
    /// Evaluates a memory candidate and returns a policy decision.
    /// </summary>
    MemoryPolicyDecision Evaluate(
        MemoryCandidate candidate,
        IReadOnlyList<MemoryEntry>? relatedMemories = null);
}

/// <summary>
/// A policy decision for a memory candidate.
/// </summary>
public class MemoryPolicyDecision
{
    /// <summary>The policy action to take.</summary>
    public MemoryPolicyAction Action { get; set; }

    /// <summary>Reason for the decision.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Final importance after policy evaluation.</summary>
    public double FinalImportance { get; set; }

    /// <summary>Final confidence after policy evaluation.</summary>
    public double FinalConfidence { get; set; }

    /// <summary>Whether this candidate should be persisted.</summary>
    public bool ShouldPersist => Action == MemoryPolicyAction.Persist ||
                                  Action == MemoryPolicyAction.Update ||
                                  Action == MemoryPolicyAction.Supersede;

    /// <summary>Whether this candidate requires human review.</summary>
    public bool RequiresReview => Action == MemoryPolicyAction.RequiresReview;

    /// <summary>Whether to ignore this candidate.</summary>
    public bool ShouldIgnore => Action == MemoryPolicyAction.Ignore;
}

/// <summary>
/// Policy actions for memory candidates.
/// </summary>
public enum MemoryPolicyAction
{
    /// <summary>Persist as new memory.</summary>
    Persist,

    /// <summary>Ignore (not worth remembering).</summary>
    Ignore,

    /// <summary>Update existing memory.</summary>
    Update,

    /// <summary>Supersede existing memory with new version.</summary>
    Supersede,

    /// <summary>Requires human review before persisting.</summary>
    RequiresReview
}
