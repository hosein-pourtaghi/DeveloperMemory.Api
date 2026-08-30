using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Normalizes heterogeneous knowledge sources (knowledge documents, developer profiles,
/// conversational capture) into a canonical memory representation suitable for
/// consolidation, duplicate detection, and persistent storage.
///
/// This service does NOT persist anything — it only normalizes input into a
/// standard intermediate form. Persistence is handled by DocumentConsolidationService
/// or the existing ingestion pipeline.
/// </summary>
public interface IMemoryNormalizationService
{
    /// <summary>
    /// Normalizes a knowledge document into a list of canonical memory candidates.
    /// A single document may produce multiple memories (e.g., separate facts per tag section).
    /// </summary>
    IReadOnlyList<CanonicalMemoryCandidate> NormalizeKnowledgeDocument(
        string title,
        string content,
        string? project = null,
        List<string>? tags = null,
        string? filePath = null);

    /// <summary>
    /// Normalizes a developer profile into a list of canonical memory candidates.
    /// Skills, experience, and role each become separate memories when meaningful.
    /// </summary>
    IReadOnlyList<CanonicalMemoryCandidate> NormalizeDeveloperProfile(
        string name,
        string role,
        string bio,
        List<string>? skills = null,
        string? experience = null,
        string? filePath = null);

    /// <summary>
    /// Normalizes raw content into a canonical memory candidate with inferred type.
    /// Used for ad-hoc normalization from any source.
    /// </summary>
    CanonicalMemoryCandidate NormalizeRaw(
        string title,
        string content,
        MemoryScope scope = MemoryScope.Global,
        Guid? projectId = null,
        string? source = null);
}

/// <summary>
/// A normalized memory candidate produced from any knowledge source.
/// This is the intermediate representation used for duplicate detection
/// and consolidation. It maps cleanly to MemoryEntry fields.
/// </summary>
public class CanonicalMemoryCandidate
{
    /// <summary>Normalized title derived from the source.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full content of the memory.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Content normalized for duplicate detection (lowered, punctuation-stripped, whitespace-collapsed).</summary>
    public string NormalizedContent { get; set; } = string.Empty;

    /// <summary>Inferred or specified memory type.</summary>
    public MemoryType MemoryType { get; set; } = MemoryType.Fact;

    /// <summary>Scope for the memory.</summary>
    public MemoryScope Scope { get; set; } = MemoryScope.Global;

    /// <summary>Data classification.</summary>
    public DataClassification Classification { get; set; } = DataClassification.Internal;

    /// <summary>Optional project association.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Optional workspace association.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Human-readable source identifier (e.g., "knowledge:architecture.md", "profile:developer.md").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Tags for retrieval and organization.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Confidence in this candidate (0.0-1.0). Knowledge docs default high.</summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>Importance score (0.0-1.0).</summary>
    public double Importance { get; set; } = 0.5;

    /// <summary>Optional expiration.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Optional metadata as JSON string.</summary>
    public string? MetadataJson { get; set; }
}
