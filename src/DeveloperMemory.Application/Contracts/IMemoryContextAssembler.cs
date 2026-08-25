using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Assembles organized context sections from retrieved memories.
/// Handles deduplication, contradiction detection, and logical grouping.
/// Does NOT perform retrieval or bypass privacy boundaries.
/// </summary>
public interface IMemoryContextAssembler
{
    /// <summary>
    /// Assembles organized context sections from the prompt context.
    /// Deduplicates similar memories, detects contradictions, and groups
    /// related items into logical sections.
    /// </summary>
    ContextAssemblyResult Assemble(
        PromptContext context,
        PromptAnalysis analysis,
        List<PromptConstraint> constraints);
}

/// <summary>
/// Result of memory context assembly.
/// </summary>
public class ContextAssemblyResult
{
    /// <summary>
    /// Organized context sections.
    /// </summary>
    public List<ContextSection> Sections { get; set; } = [];

    /// <summary>
    /// Number of duplicates removed.
    /// </summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>
    /// Detected contradictions (for observability, not automatic resolution).
    /// </summary>
    public List<ContradictionInfo> Contradictions { get; set; } = [];
}

/// <summary>
/// Information about a detected contradiction between memories.
/// </summary>
public class ContradictionInfo
{
    /// <summary>First memory in the contradiction pair.</summary>
    public Guid MemoryId1 { get; set; }

    /// <summary>Second memory in the contradiction pair.</summary>
    public Guid MemoryId2 { get; set; }

    /// <summary>Human-readable description of the contradiction.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Which memory should win based on lifecycle/precedence rules.</summary>
    public Guid PreferredMemoryId { get; set; }
}
