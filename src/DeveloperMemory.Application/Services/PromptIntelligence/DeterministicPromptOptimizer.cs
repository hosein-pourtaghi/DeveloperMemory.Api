using DeveloperMemory.Application.Contracts;
using System.Text;
using System.Text.RegularExpressions;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Deterministic, provider-independent prompt optimizer.
/// Removes duplicate instructions, normalizes whitespace, eliminates redundancy,
/// and preserves semantic meaning.
/// 
/// Does NOT call external LLMs. This is a text-processing optimization only.
/// 
/// Future: LlmPromptOptimizer can provide richer semantic optimization.
/// </summary>
public partial class DeterministicPromptOptimizer : IPromptOptimizer
{
    public string Optimize(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        var result = prompt;

        // Step 1: Normalize line endings
        result = result.Replace("\r\n", "\n").Replace("\r", "\n");

        // Step 2: Remove duplicate consecutive blank lines (collapse to single)
        result = DuplicateBlankLinesRegex().Replace(result, "\n\n");

        // Step 3: Remove duplicate lines (same content appearing twice)
        result = RemoveDuplicateLines(result);

        // Step 4: Trim trailing whitespace from each line
        result = TrailingWhitespaceRegex().Replace(result, "");

        // Step 5: Trim leading/trailing whitespace from the whole prompt
        result = result.Trim();

        return result;
    }

    /// <summary>
    /// Removes exact duplicate lines while preserving order and section structure.
    /// Only removes lines that appear multiple times with the same content.
    /// Section headers, markers, and the user request are never removed.
    /// </summary>
    private static string RemoveDuplicateLines(string text)
    {
        var lines = text.Split('\n');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Never remove section markers, headers, or blank lines
            if (string.IsNullOrEmpty(trimmed) ||
                trimmed.StartsWith("---") ||
                trimmed.StartsWith("## ") ||
                trimmed.StartsWith("[Task") ||
                trimmed.StartsWith("User Request:") ||
                trimmed.StartsWith("Goal:"))
            {
                result.AppendLine(line);
                continue;
            }

            // For content lines, check for exact duplicates
            if (!seen.Add(trimmed))
            {
                // Duplicate line — skip it
                continue;
            }

            result.AppendLine(line);
        }

        return result.ToString();
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex DuplicateBlankLinesRegex();

    [GeneratedRegex(@"[ \t]+$", RegexOptions.Multiline)]
    private static partial Regex TrailingWhitespaceRegex();
}
