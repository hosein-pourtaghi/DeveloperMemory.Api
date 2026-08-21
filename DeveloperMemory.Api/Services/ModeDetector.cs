using DeveloperMemory.Api.Models;
using System;
using System.Linq;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// Detects whether a Cline request is in plan mode or build (act) mode
/// by analyzing the system prompt content.
/// </summary>
public static class ModeDetector
{
    /// <summary>
    /// Detected task mode from Cline's request.
    /// </summary>
    public enum TaskMode
    {
        /// <summary>Plan/reasoning mode — user is asking for analysis, planning, or discussion.</summary>
        Plan,
        /// <summary>Build/act mode — user is asking for implementation, code changes, or tool use.</summary>
        Build,
        /// <summary>Unrecognized — use default model.</summary>
        Unknown
    }

    // Keywords that indicate Cline is in build/act mode (tool execution phase)
    private static readonly string[] BuildModeIndicators =
    [
        // Cline's system prompt for build mode includes tool definitions
        "You have access to a set of tools that are executed upon the user's approval",
        "You can use one tool per message",
        "execute_command",
        "write_to_file",
        "replace_in_file",
        "read_file",
        "list_files",
        "ask_followup_question",
        // Plan mode indicators (Cline's planning system prompt)
        "TASK",
        "Checklist",
        "task_progress"
    ];

    /// <summary>
    /// Detects the task mode from the request messages.
    /// Checks the system message content for mode-specific indicators.
    /// </summary>
    public static TaskMode DetectMode(OpenAIChatCompletionRequest request)
    {
        if (request?.Messages == null) return TaskMode.Unknown;

        var systemMessage = request.Messages
            .FirstOrDefault(m => m.Role == "system");

        if (systemMessage?.Content == null) return TaskMode.Unknown;

        var content = systemMessage.Content;

        // Check for build mode indicators
        bool hasToolDefinitions = content.Contains("execute_command") ||
                                  content.Contains("write_to_file") ||
                                  content.Contains("replace_in_file");

        // Check for plan mode indicators
        bool hasPlanIndicators = content.Contains("# TASK") ||
                                 content.Contains("Checklist") ||
                                 content.Contains("task_progress") ||
                                 content.Contains("## Plan") ||
                                 content.Contains("Goal:");

        if (hasToolDefinitions && !hasPlanIndicators)
            return TaskMode.Build;

        if (hasPlanIndicators && !hasToolDefinitions)
            return TaskMode.Plan;

        if (hasToolDefinitions && hasPlanIndicators)
            return TaskMode.Build; // If both, default to build (tool execution phase)

        return TaskMode.Unknown;
    }
}
