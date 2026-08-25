namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// A logically organized section of context within a PromptPackage.
/// Sections are assembled by the MemoryContextComposer and presented
/// in a structured order for downstream consumption.
/// </summary>
public class ContextSection
{
    /// <summary>
    /// The section identifier (e.g., "system_rules", "project_context", "relevant_memory").
    /// </summary>
    public string SectionId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable section heading.
    /// </summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>
    /// Ordered priority — lower values appear earlier in the prompt.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The content items in this section.
    /// </summary>
    public List<ContextItem> Items { get; set; } = [];

    /// <summary>
    /// Estimated tokens consumed by this section.
    /// </summary>
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// A single item within a context section.
/// </summary>
public class ContextItem
{
    /// <summary>
    /// The content text.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional label or source attribution.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The memory ID this item is derived from, if applicable.
    /// </summary>
    public Guid? SourceMemoryId { get; set; }

    /// <summary>
    /// Importance score of the source memory.
    /// </summary>
    public double Importance { get; set; }
}
