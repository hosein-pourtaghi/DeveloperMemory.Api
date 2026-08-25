using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Deterministic prompt optimizer.
/// Removes redundancy, normalizes whitespace, preserves critical constraints.
///
/// Does NOT:
/// - Change user intent
/// - Remove critical constraints
/// - Invent requirements
/// - Override instructions
/// - Call external LLMs
/// </summary>
public class DeterministicPromptOptimizer
{
    private readonly ILogger<DeterministicPromptOptimizer> _logger;

    public DeterministicPromptOptimizer(ILogger<DeterministicPromptOptimizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Optimizes a prompt for downstream consumption.
    /// </summary>
    public PromptOptimizationResult Optimize(PromptConstructionResult input)
    {
        var original = input.ComposedPrompt;
        var optimized = original;

        // ── Step 1: Remove duplicate lines ──
        optimized = RemoveDuplicateLines(optimized);

        // ── Step 2: Normalize whitespace ──
        optimized = NormalizeWhitespace(optimized);

        // ── Step 3: Remove redundant section headers ──
        optimized = RemoveRedundantHeaders(optimized);

        // ── Step 4: Compress repeated instructions ──
        optimized = CompressRepeatedInstructions(optimized);

        // ── Step 5: Ensure section delimiters are clear ──
        optimized = EnsureClearDelimiters(optimized);

        var savedTokens = EstimateTokens(original) - EstimateTokens(optimized);
        var changed = original != optimized;

        if (changed)
        {
            _logger.LogDebug(
                "Prompt optimized: saved ~{Tokens} tokens ({Original} → {Optimized} chars)",
                savedTokens, original.Length, optimized.Length);
        }

        return new PromptOptimizationResult
        {
            OptimizedPrompt = optimized,
            OriginalLength = original.Length,
            OptimizedLength = optimized.Length,
            EstimatedTokensSaved = Math.Max(0, savedTokens),
            OptimizationApplied = changed,
            Changes = changed ? ["deduplication", "whitespace", "compression"] : []
        };
    }

    private static string RemoveDuplicateLines(string text)
    {
        var lines = text.Split('\n');
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                result.Add(line);
                continue;
            }

            // Don't deduplicate section headers or important markers
            if (trimmed.StartsWith("---") || trimmed.StartsWith("[") || trimmed.StartsWith("SYSTEM"))
            {
                result.Add(line);
                continue;
            }

            if (seen.Add(trimmed))
            {
                result.Add(line);
            }
        }

        return string.Join('\n', result);
    }

    private static string NormalizeWhitespace(string text)
    {
        // Replace multiple blank lines with double newline
        var result = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return result.Trim();
    }

    private static string RemoveRedundantHeaders(string text)
    {
        // Remove duplicate section markers
        var result = System.Text.RegularExpressions.Regex.Replace(
            text, @"(--- .+ ---)\n\1", "$1");
        return result;
    }

    private static string CompressRepeatedInstructions(string text)
    {
        // If the same instruction appears multiple times, keep only the first
        var lines = text.Split('\n');
        var instructionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim().ToLowerInvariant();

            // Only compress security/repeated instructions
            if (trimmed.Contains("do not") || trimmed.Contains("treat") || trimmed.Contains("data only"))
            {
                if (instructionCounts.ContainsKey(trimmed))
                {
                    instructionCounts[trimmed]++;
                    if (instructionCounts[trimmed] <= 2) // Allow up to 2 occurrences
                    {
                        result.Add(line);
                    }
                    continue;
                }
                instructionCounts[trimmed] = 1;
            }

            result.Add(line);
        }

        return string.Join('\n', result);
    }

    private static string EnsureClearDelimiters(string text)
    {
        // Ensure section delimiters are on their own lines
        var result = System.Text.RegularExpressions.Regex.Replace(
            text, @"([^\n])(--- )", "$1\n$2");
        return result;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}

/// <summary>
/// Result of prompt optimization.
/// </summary>
public class PromptOptimizationResult
{
    /// <summary>The optimized prompt text.</summary>
    public string OptimizedPrompt { get; set; } = string.Empty;

    /// <summary>Original character count.</summary>
    public int OriginalLength { get; set; }

    /// <summary>Optimized character count.</summary>
    public int OptimizedLength { get; set; }

    /// <summary>Estimated tokens saved.</summary>
    public int EstimatedTokensSaved { get; set; }

    /// <summary>Whether optimization was applied.</summary>
    public bool OptimizationApplied { get; set; }

    /// <summary>What changes were made.</summary>
    public List<string> Changes { get; set; } = [];
}
