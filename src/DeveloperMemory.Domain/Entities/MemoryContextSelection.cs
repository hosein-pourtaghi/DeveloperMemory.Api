using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Result of selecting memories for a prompt context.
/// Contains selected and skipped memories with reasons.
/// </summary>
public class MemoryContextSelection
{
    /// <summary>Memories selected for inclusion in the prompt context.</summary>
    public List<SelectedMemory> Selected { get; set; } = [];

    /// <summary>Memories that were candidates but were skipped.</summary>
    public List<SkippedMemory> Skipped { get; set; } = [];

    /// <summary>Total estimated tokens/characters of selected memories.</summary>
    public int EstimatedCost { get; set; }

    /// <summary>The budget that was applied.</summary>
    public int Budget { get; set; }

    /// <summary>Whether the budget was fully consumed.</summary>
    public bool BudgetExhausted { get; set; }
}

/// <summary>
/// A memory selected for inclusion in context.
/// </summary>
public class SelectedMemory
{
    public MemoryEntry Memory { get; set; } = null!;
    public double RelevanceScore { get; set; }
    public string SelectionReason { get; set; } = string.Empty;
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// A memory that was skipped during context selection.
/// </summary>
public class SkippedMemory
{
    public MemoryEntry Memory { get; set; } = null!;
    public string SkipReason { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
