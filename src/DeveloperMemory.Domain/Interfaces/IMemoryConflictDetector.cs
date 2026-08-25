using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Detects conflicts between a new memory and existing memories.
/// The baseline implementation uses deterministic rules. Future implementations
/// may use LLM-based semantic contradiction detection.
/// </summary>
public interface IMemoryConflictDetector
{
    /// <summary>
    /// Checks whether a new memory conflicts with any existing memories.
    /// Returns conflict information for each detected conflict.
    /// </summary>
    IReadOnlyList<MemoryConflict> DetectConflicts(
        MemoryEntry newMemory,
        IReadOnlyList<MemoryEntry> existingMemories);
}

/// <summary>
/// Information about a detected conflict between a new and existing memory.
/// </summary>
public class MemoryConflict
{
    /// <summary>The existing memory that conflicts.</summary>
    public MemoryEntry ExistingMemory { get; set; } = null!;

    /// <summary>The type of conflict detected.</summary>
    public MemoryConflictType ConflictType { get; set; }

    /// <summary>A human-readable explanation of the conflict.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Whether the new memory should supersede the existing one.</summary>
    public bool ShouldSupersede { get; set; }

    /// <summary>Confidence score for this conflict detection (0.0-1.0).</summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Types of memory conflicts.
/// </summary>
public enum MemoryConflictType
{
    /// <summary>Exact duplicate content.</summary>
    ExactDuplicate,

    /// <summary>Normalized duplicate (same after normalization).</summary>
    NormalizedDuplicate,

    /// <summary>Contradictory statements about the same topic.</summary>
    Contradiction,

    /// <summary>Updated version of the same information.</summary>
    UpdatedVersion,

    /// <summary>No conflict detected.</summary>
    NoConflict
}
