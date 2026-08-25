using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents a single constraint that the Prompt Intelligence Engine should respect.
/// Constraints are extracted from the current request, project rules, user preferences,
/// and persistent memory, and are resolved with deterministic precedence.
/// </summary>
public class PromptConstraint
{
    /// <summary>
    /// The category of constraint.
    /// </summary>
    public ConstraintType Type { get; set; }

    /// <summary>
    /// The constraint value or rule text.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Where this constraint originated.
    /// </summary>
    public ConstraintSource Source { get; set; }

    /// <summary>
    /// Precedence level — higher values override lower values when conflicts exist.
    /// Deterministic: System > ProjectRule > ExplicitCurrentRequest > UserPreference > GeneralMemory.
    /// </summary>
    public int Precedence { get; set; }

    /// <summary>
    /// Optional identifier of the memory or project rule that produced this constraint.
    /// </summary>
    public Guid? SourceMemoryId { get; set; }
}

/// <summary>
/// Where a constraint originated from, used for precedence resolution.
/// </summary>
public enum ConstraintSource
{
    /// <summary>System-level safety or architectural invariant.</summary>
    System = 100,

    /// <summary>Project-level rule from persistent memory or configuration.</summary>
    ProjectRule = 80,

    /// <summary>Explicit constraint in the current user request.</summary>
    ExplicitCurrentRequest = 60,

    /// <summary>Persistent user preference from profile or memory.</summary>
    UserPreference = 40,

    /// <summary>General context from retrieved memory.</summary>
    GeneralMemory = 20
}
