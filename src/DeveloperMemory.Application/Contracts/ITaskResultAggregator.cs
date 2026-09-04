namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// V2-4 result aggregation boundary.
///
/// Combines completed subtask results into a final coherent Assistant response
/// while:
///   - preserving subtask identity and Agent identity
///   - distinguishing successful and failed subtasks (never silently dropping
///     failures)
///   - preventing any single subtask from injecting control instructions into
///     the aggregated response (subtask output is treated as DATA)
///
/// This is NOT a general workflow engine — it is a narrow, deterministic
/// combining step.
/// </summary>
public interface ITaskResultAggregator
{
    /// <summary>
    /// Combines execution records into a final response.
    /// </summary>
    /// <param name="originalRequest">The parent request text (for context).</param>
    /// <param name="records">Execution records for all planned subtasks.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TaskResultAggregation> AggregateAsync(
        string originalRequest,
        IReadOnlyList<TaskExecutionRecord> records,
        CancellationToken ct = default);
}

/// <summary>
/// Result of aggregation: the combined response plus any non-fatal warnings.
/// </summary>
public class TaskResultAggregation
{
    /// <summary>The final coherent response text.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Non-fatal warnings produced during aggregation.</summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];
}