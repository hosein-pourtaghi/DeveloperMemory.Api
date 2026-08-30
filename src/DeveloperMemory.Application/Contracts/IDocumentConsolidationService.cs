using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Orchestrates consolidation of normalized memory candidates against existing
/// persistent memories. Handles duplicate detection, conflict resolution,
/// provenance tracking, and lifecycle-aware consolidation.
///
/// This service sits above both the knowledge/profile sources and the
/// MemoryIngestionService. It evaluates candidates from any source and
/// decides whether to create new memories, update existing ones, or
/// route through supersession.
/// </summary>
public interface IDocumentConsolidationService
{
    /// <summary>
    /// Consolidates a single canonical memory candidate against existing memories.
    /// Returns the consolidation result including what action was taken.
    /// </summary>
    Task<ConsolidationResult> ConsolidateAsync(
        CanonicalMemoryCandidate candidate,
        string ownerId,
        CancellationToken ct = default);

    /// <summary>
    /// Consolidates a batch of canonical memory candidates.
    /// Returns per-candidate results.
    /// </summary>
    Task<IReadOnlyList<ConsolidationResult>> ConsolidateBatchAsync(
        IReadOnlyList<CanonicalMemoryCandidate> candidates,
        string ownerId,
        CancellationToken ct = default);

    /// <summary>
    /// Detects whether two canonical candidates represent the same underlying fact.
    /// Returns the similarity score and whether they should be consolidated.
    /// </summary>
    ConsolidationMatch FindMatch(
        CanonicalMemoryCandidate candidate,
        IReadOnlyList<MemoryEntry> existingMemories);
}

/// <summary>
/// Result of consolidating a single canonical candidate against existing memories.
/// </summary>
public class ConsolidationResult
{
    /// <summary>The action taken during consolidation.</summary>
    public ConsolidationAction Action { get; set; }

    /// <summary>The memory that was created or updated, if applicable.</summary>
    public MemoryEntry? Memory { get; set; }

    /// <summary>An existing memory this candidate was matched against.</summary>
    public MemoryEntry? MatchedMemory { get; set; }

    /// <summary>The original candidate that was consolidated.</summary>
    public CanonicalMemoryCandidate Candidate { get; set; } = null!;

    /// <summary>Human-readable description of what happened.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Whether a duplicate was detected.</summary>
    public bool DuplicateDetected { get; set; }

    /// <summary>Whether a conflict was detected and resolved.</summary>
    public bool ConflictResolved { get; set; }

    /// <summary>Whether provenance information was preserved.</summary>
    public bool ProvenancePreserved { get; set; }
}

/// <summary>
/// Describes the match between a candidate and an existing memory.
/// </summary>
public class ConsolidationMatch
{
    /// <summary>The best-matching existing memory, if any.</summary>
    public MemoryEntry? BestMatch { get; set; }

    /// <summary>Similarity score (0.0-1.0).</summary>
    public double Similarity { get; set; }

    /// <summary>Whether the match is exact.</summary>
    public bool IsExactMatch { get; set; }

    /// <summary>Whether the match is a normalized duplicate.</summary>
    public bool IsNormalizedMatch { get; set; }

    /// <summary>Whether the candidate appears to be an updated version.</summary>
    public bool IsUpdatedVersion { get; set; }

    /// <summary>Whether the candidate conflicts with the match.</summary>
    public bool IsConflict { get; set; }

    /// <summary>Explanation of the match.</summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Actions that consolidation can take.
/// </summary>
public enum ConsolidationAction
{
    /// <summary>A new memory was created (no match found).</summary>
    Created,

    /// <summary>An existing memory was updated with new content from the candidate.</summary>
    Updated,

    /// <summary>The candidate was identified as a duplicate and not persisted.</summary>
    DuplicateIgnored,

    /// <summary>The candidate supersedes an existing memory via the lifecycle mechanism.</summary>
    SupersededExisting,

    /// <summary>A potential conflict was detected but confidence is insufficient to auto-resolve.</summary>
    RequiresReview,

    /// <summary>The candidate was rejected (empty content, too short, etc.).</summary>
    Rejected
}
