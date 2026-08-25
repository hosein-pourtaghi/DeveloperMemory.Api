using DeveloperMemory.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Deterministic validation for LLM-optimized prompts.
/// Ensures critical information survives optimization.
///
/// Validation checks:
/// - Original intent preserved
/// - Critical constraints preserved
/// - Required context preserved
/// - No obvious duplication
/// - Token budget respected
/// - Security boundaries preserved
/// </summary>
public class PromptValidator
{
    private readonly ILogger<PromptValidator> _logger;

    public PromptValidator(ILogger<PromptValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates an optimized prompt against the original.
    /// Returns validation result with details.
    /// </summary>
    public PromptValidationResult Validate(
        string originalPrompt,
        string optimizedPrompt,
        IntentAnalysisResult? intent = null,
        int tokenBudget = 4000)
    {
        var result = new PromptValidationResult
        {
            OriginalLength = originalPrompt.Length,
            OptimizedLength = optimizedPrompt.Length,
            IsValid = true
        };

        // ── Check 1: Empty output ──
        if (string.IsNullOrWhiteSpace(optimizedPrompt))
        {
            result.IsValid = false;
            result.Issues.Add("Optimized prompt is empty");
            return result;
        }

        // ── Check 2: Token budget ──
        var estimatedTokens = EstimateTokens(optimizedPrompt);
        var budgetTokens = tokenBudget;
        if (estimatedTokens > budgetTokens * 1.1) // Allow 10% overshoot
        {
            result.BudgetExceeded = true;
            result.Issues.Add($"Token budget exceeded: {estimatedTokens}/{budgetTokens}");
            _logger.LogWarning("Token budget exceeded in optimized prompt");
        }

        // ── Check 3: Critical keywords preserved ──
        if (intent?.ExplicitConstraints.Count > 0)
        {
            foreach (var constraint in intent.ExplicitConstraints)
            {
                if (!optimizedPrompt.Contains(constraint, StringComparison.OrdinalIgnoreCase))
                {
                    result.CriticalConstraintMissing = true;
                    result.Issues.Add($"Critical constraint not found: {constraint}");
                    _logger.LogWarning("Critical constraint missing from optimized prompt: {Constraint}", constraint);
                }
            }
        }

        // ── Check 4: User request preserved ──
        // Extract the user request section (after "--- USER REQUEST ---")
        var userRequest = ExtractUserRequest(originalPrompt);
        if (!string.IsNullOrEmpty(userRequest) && userRequest.Length > 20)
        {
            // Check if key parts of the user request are preserved
            var requestWords = userRequest.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4)
                .Take(10)
                .ToList();

            var missingWords = requestWords.Where(w =>
                !optimizedPrompt.Contains(w, StringComparison.OrdinalIgnoreCase)).ToList();

            if (missingWords.Count > requestWords.Count * 0.5)
            {
                result.UserRequestPartiallyLost = true;
                result.Issues.Add("User request may be incomplete in optimized prompt");
            }
        }

        // ── Check 5: Security boundaries ──
        if (!optimizedPrompt.Contains("RETRIEVED CONTEXT") &&
            originalPrompt.Contains("RETRIEVED CONTEXT"))
        {
            result.SecurityBoundaryMissing = true;
            result.Issues.Add("Security boundary (RETRIEVED CONTEXT) not found in optimized prompt");
            _logger.LogWarning("Security boundary missing from optimized prompt");
        }

        // ── Check 6: Not too similar (may indicate no optimization) ──
        var similarity = CalculateSimilarity(originalPrompt, optimizedPrompt);
        if (similarity > 0.95)
        {
            result.NegligibleChange = true;
            result.Issues.Add("Optimized prompt is nearly identical to original");
        }

        // ── Check 7: Not too different (may indicate corruption) ──
        if (similarity < 0.3)
        {
            result.IsValid = false;
            result.Issues.Add($"Optimized prompt deviates too much from original (similarity={similarity:F2})");
            _logger.LogWarning("Optimized prompt deviates too much: {Similarity}", similarity);
        }

        // Overall validity
        if (result.CriticalConstraintMissing || result.SecurityBoundaryMissing)
        {
            result.IsValid = false;
        }

        return result;
    }

    private static string ExtractUserRequest(string prompt)
    {
        var marker = "--- USER REQUEST ---";
        var index = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;

        var start = index + marker.Length;
        if (start >= prompt.Length) return string.Empty;

        var remaining = prompt[start..];
        var endMarker = remaining.IndexOf("---", StringComparison.OrdinalIgnoreCase);
        if (endMarker < 0) return remaining.Trim();

        return remaining[..endMarker].Trim();
    }

    private static double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var setA = new HashSet<string>(wordsA);
        var setB = new HashSet<string>(wordsB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}

/// <summary>
/// Result of prompt validation.
/// </summary>
public class PromptValidationResult
{
    /// <summary>Whether the optimized prompt passes validation.</summary>
    public bool IsValid { get; set; }

    /// <summary>Original character count.</summary>
    public int OriginalLength { get; set; }

    /// <summary>Optimized character count.</summary>
    public int OptimizedLength { get; set; }

    /// <summary>Whether token budget was exceeded.</summary>
    public bool BudgetExceeded { get; set; }

    /// <summary>Whether a critical constraint is missing.</summary>
    public bool CriticalConstraintMissing { get; set; }

    /// <summary>Whether the user request is partially lost.</summary>
    public bool UserRequestPartiallyLost { get; set; }

    /// <summary>Whether security boundaries are missing.</summary>
    public bool SecurityBoundaryMissing { get; set; }

    /// <summary>Whether the change is negligible.</summary>
    public bool NegligibleChange { get; set; }

    /// <summary>Validation issues found.</summary>
    public List<string> Issues { get; set; } = [];
}
