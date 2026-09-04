using DeveloperMemory.Application.Contracts;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// V2-4 task model and decomposition boundary.
///
/// A delegated task is a bounded unit of work: a description, an assigned
/// Agent (resolved through the existing IAgentResolver at execution time), and
/// optional ordering dependencies. The model intentionally carries NO
/// workflow-engine functionality (no persistence, no retries, no schedules,
/// no recursive subgraphs).
///
/// Responsibilities:
///   ITaskDecomposer — transforms a request into a bounded, validated list of
///     subtasks. It does NOT execute tasks, resolve providers, persist
///     anything, or aggregate final answers.
/// </summary>
public class DelegatedTask
{
    /// <summary>Stable identifier of this subtask within the plan.</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>The subtask description/request executed by its agent.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The registered Agent (via IAgentResolver) assigned to this subtask.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Task ids this subtask depends on (ordering only — no data passing in
    /// this phase). Must refer to existing tasks; must be acyclic.
    /// </summary>
    public IReadOnlyList<string> DependsOn { get; set; } = [];
}

/// <summary>
/// A validated decomposition plan: an ordered, bounded list of subtasks.
/// Empty plans are treated as "no decomposition" by the caller.
/// </summary>
public class TaskPlan
{
    /// <summary>The bounded list of subtasks.</summary>
    public IReadOnlyList<DelegatedTask> Tasks { get; set; } = [];

    /// <summary>Non-fatal warnings produced during decomposition.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

/// <summary>
/// Runtime record of one delegated subtask execution.
/// Failures are preserved — a failed subtask is never silently converted into
/// a successful result.
/// </summary>
public class TaskExecutionRecord
{
    /// <summary>The subtask identifier from the plan.</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>The agent that executed this subtask.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Execution outcome.</summary>
    public TaskExecutionStatus Status { get; set; }

    /// <summary>The subtask response, when successful.</summary>
    public string? Response { get; set; }

    /// <summary>Client-safe failure information, when failed.</summary>
    public string? Error { get; set; }

    /// <summary>Execution duration (ms).</summary>
    public double DurationMs { get; set; }
}

/// <summary>
/// Subtask execution outcome.
/// </summary>
public enum TaskExecutionStatus
{
    /// <summary>Subtask produced a response.</summary>
    Succeeded,

    /// <summary>Subtask failed (recorded, execution continues per policy).</summary>
    Failed
}

/// <summary>
/// Decomposition boundary. Implementations transform a request into a bounded
/// list of subtasks. They do NOT execute tasks.
/// </summary>
public interface ITaskDecomposer
{
    /// <summary>
    /// Deterministic gate used by "auto" execution mode: whether this request
    /// shows signals that decomposition is likely appropriate. Never calls a
    /// model. Explicit decompose mode bypasses this gate.
    /// </summary>
    bool ShouldDecompose(AssistantExecutionRequest request);

    /// <summary>
    /// Produces a bounded, validated list of subtasks for the request.
    /// </summary>
    /// <exception cref="InvalidTaskDecompositionException">
    /// When the (model-produced or derived) decomposition is invalid: empty,
    /// excessive, referencing unknown/disabled agents, invalid dependencies,
    /// cycles, or recursive-delegation instructions. Callers may fall back to
    /// direct execution.
    /// </exception>
    /// <exception cref="AssistantModelException">When the model provider cannot serve the decomposition.</exception>
    Task<TaskPlan> DecomposeAsync(
        AssistantExecutionRequest request,
        CancellationToken ct = default);
}