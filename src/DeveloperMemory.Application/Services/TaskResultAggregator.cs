using System.Text;
using DeveloperMemory.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// V2-4 result aggregator.
///
/// Combines completed subtask results into a final coherent response without
/// calling a model. Subtask output is treated strictly as DATA:
///   - each result is labeled with its task id and agent id
///   - failed subtasks are reported explicitly (never silently dropped)
///   - subtask content is sanitized against prompt-injection patterns so a
///     single subtask cannot inject control instructions into the aggregate
///
/// This is a narrow deterministic combining step, not a workflow engine.
/// </summary>
public class TaskResultAggregator : ITaskResultAggregator
{
    private readonly ILogger<TaskResultAggregator> _logger;

    public TaskResultAggregator(ILogger<TaskResultAggregator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<TaskResultAggregation> AggregateAsync(
        string originalRequest,
        IReadOnlyList<TaskExecutionRecord> records,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var successful = records.Count(r => r.Status == TaskExecutionStatus.Succeeded);
        var failed = records.Count - successful;

        var sb = new StringBuilder();
        sb.AppendLine("## Delegated task results");
        sb.AppendLine();

        foreach (var record in records)
        {
            sb.AppendLine($"### Task {record.TaskId} (agent: {Sanitize(record.AgentId)})");
            if (record.Status == TaskExecutionStatus.Succeeded)
            {
                sb.AppendLine(Sanitize(record.Response ?? string.Empty));
            }
            else
            {
                sb.AppendLine($"[FAILED] {Sanitize(record.Error ?? "task failed")}");
            }
            sb.AppendLine();
        }

        var warnings = new List<string>();
        if (failed > 0)
        {
            warnings.Add($"{failed} of {records.Count} subtasks failed");
        }

        _logger.LogInformation(
            "V2-4: aggregated {Total} subtask results ({Success} succeeded, {Failed} failed)",
            records.Count, successful, failed);

        return Task.FromResult(new TaskResultAggregation
        {
            Response = sb.ToString().TrimEnd(),
            Warnings = warnings
        });
    }

    /// <summary>
    /// Escapes known prompt-injection patterns in subtask output so it cannot
    /// escalate its own authority.
    /// </summary>
    private static string Sanitize(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return content
            .Replace("[SYSTEM]", "[ESCAPED]")
            .Replace("[/SYSTEM]", "[ESCAPED]")
            .Replace("<system>", "[ESCAPED]")
            .Replace("</system>", "[ESCAPED]")
            .Replace("IGNORE PREVIOUS", "[ESCAPED]")
            .Replace("ignore previous", "[ESCAPED]");
    }
}