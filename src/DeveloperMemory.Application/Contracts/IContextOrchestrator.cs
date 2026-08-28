using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Orchestrates context gathering from memory, project context, and rules.
/// Produces a unified context ready for prompt construction.
/// </summary>
public interface IContextOrchestrator
{
    /// <summary>
    /// Orchestrates context gathering and produces a unified result.
    /// </summary>
    Task<ContextOrchestrationResult> OrchestrateAsync(
        ContextOrchestrationRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Request for context orchestration.
/// </summary>
public class ContextOrchestrationRequest
{
    /// <summary>The original user input.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>The project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>The workspace identifier.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Server-controlled owner identifier for memory isolation.</summary>
    public string? OwnerId { get; set; }

    /// <summary>The user identifier.</summary>
    public string? UserId { get; set; }

    /// <summary>Maximum tokens for the context.</summary>
    public int TokenBudget { get; set; } = 4000;

    /// <summary>Whether to include memory context.</summary>
    public bool IncludeMemory { get; set; } = true;

    /// <summary>Whether to include project context.</summary>
    public bool IncludeProjectContext { get; set; } = true;

    /// <summary>The intent analysis result.</summary>
    public IntentAnalysisResult? IntentAnalysis { get; set; }
}

/// <summary>
/// Result of context orchestration.
/// </summary>
public class ContextOrchestrationResult
{
    /// <summary>Selected memory items.</summary>
    public List<ContextMemoryItem> SelectedMemories { get; set; } = [];

    /// <summary>Skipped memory items with reasons.</summary>
    public List<SkippedContextItem> SkippedMemories { get; set; } = [];

    /// <summary>Project context if available.</summary>
    public ProjectContext? ProjectContext { get; set; }

    /// <summary>Effective constraints applied.</summary>
    public List<string> EffectiveConstraints { get; set; } = [];

    /// <summary>Total estimated tokens.</summary>
    public int EstimatedTokens { get; set; }

    /// <summary>Token budget used.</summary>
    public int BudgetUsed { get; set; }

    /// <summary>Whether the budget was exceeded.</summary>
    public bool BudgetExceeded { get; set; }

    /// <summary>Number of conflicts detected.</summary>
    public int ConflictsDetected { get; set; }

    /// <summary>Warnings.</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// A selected memory item for context.
/// </summary>
public class ContextMemoryItem
{
    /// <summary>The memory identifier.</summary>
    public Guid MemoryId { get; set; }

    /// <summary>Memory content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Memory type.</summary>
    public string MemoryType { get; set; } = string.Empty;

    /// <summary>Relevance score.</summary>
    public double Score { get; set; }

    /// <summary>Why this memory was selected.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Priority level (higher = more important).</summary>
    public int Priority { get; set; }

    /// <summary>Estimated tokens.</summary>
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// A skipped context item.
/// </summary>
public class SkippedContextItem
{
    /// <summary>The memory identifier.</summary>
    public Guid MemoryId { get; set; }

    /// <summary>Why it was skipped.</summary>
    public string SkipReason { get; set; } = string.Empty;
}
