using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic prompt quality evaluator.
/// Scores prompts on multiple dimensions using rule-based analysis.
/// </summary>
public class DeterministicPromptQualityEvaluator : IPromptQualityEvaluator
{
    public string EvaluatorName => "deterministic";
    public string EvaluatorVersion => "1.0";

    public PromptQualityScore Evaluate(
        string originalPrompt,
        string optimizedPrompt,
        IntentAnalysisResult? intent = null,
        int tokenBudget = 4000)
    {
        var score = new PromptQualityScore
        {
            Evaluator = EvaluatorName,
            EvaluatorVersion = EvaluatorVersion
        };

        // ── Intent Preservation ──
        score.IntentPreservation = EvaluateIntentPreservation(originalPrompt, optimizedPrompt, intent);

        // ── Constraint Preservation ──
        score.ConstraintPreservation = EvaluateConstraintPreservation(originalPrompt, optimizedPrompt, intent);

        // ── Context Relevance ──
        score.ContextRelevance = EvaluateContextRelevance(optimizedPrompt);

        // ── Structure ──
        score.Structure = EvaluateStructure(optimizedPrompt);

        // ── Token Efficiency ──
        score.TokenEfficiency = EvaluateTokenEfficiency(optimizedPrompt, tokenBudget);

        // ── Security Validation ──
        score.SecurityValidation = EvaluateSecurity(optimizedPrompt);

        // ── Compute overall ──
        score.ComputeOverall();

        // ── Collect issues ──
        if (score.IntentPreservation < 0.7)
            score.Issues.Add("Intent may be partially lost");
        if (score.ConstraintPreservation < 0.9)
            score.Issues.Add("Constraints may not be fully preserved");
        if (score.SecurityValidation < 0.9)
            score.Issues.Add("Security boundary issues detected");
        if (score.TokenEfficiency < 0.5)
            score.Issues.Add("Significant token inefficiency");

        return score;
    }

    private static double EvaluateIntentPreservation(string original, string optimized, IntentAnalysisResult? intent)
    {
        double score = 1.0;

        // Check if key intent keywords are preserved
        if (intent?.Keywords.Count > 0)
        {
            var preservedCount = intent.Keywords.Count(k =>
                optimized.Contains(k, StringComparison.OrdinalIgnoreCase));

            var preservationRate = (double)preservedCount / intent.Keywords.Count;
            score *= preservationRate;
        }

        // Check if the original request words are mostly preserved
        var originalWords = original.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4)
            .Take(20)
            .ToList();

        if (originalWords.Count > 0)
        {
            var preservedWords = originalWords.Count(w =>
                optimized.Contains(w, StringComparison.OrdinalIgnoreCase));

            var wordPreservation = (double)preservedWords / originalWords.Count;
            score = (score + wordPreservation) / 2;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double EvaluateConstraintPreservation(string original, string optimized, IntentAnalysisResult? intent)
    {
        if (intent?.ExplicitConstraints == null || intent.ExplicitConstraints.Count == 0)
        {
            return 1.0; // No constraints to preserve
        }

        var preservedCount = intent.ExplicitConstraints.Count(c =>
            optimized.Contains(c, StringComparison.OrdinalIgnoreCase));

        return (double)preservedCount / intent.ExplicitConstraints.Count;
    }

    private static double EvaluateContextRelevance(string optimized)
    {
        double score = 1.0;

        // Check for context sections
        if (!optimized.Contains("---") && optimized.Length > 200)
        {
            score -= 0.2; // No clear section delimiters
        }

        // Check for RETRIEVED CONTEXT section (security marker)
        if (optimized.Contains("RETRIEVED CONTEXT"))
        {
            score = Math.Min(score + 0.1, 1.0);
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double EvaluateStructure(string optimized)
    {
        double score = 1.0;

        // Check for section delimiters
        var sectionCount = CountOccurrences(optimized, "---");
        if (sectionCount < 2 && optimized.Length > 500)
        {
            score -= 0.2;
        }

        // Check for excessive blank lines
        var blankLineCount = CountOccurrences(optimized, "\n\n\n");
        if (blankLineCount > 3)
        {
            score -= 0.1;
        }

        // Check for reasonable line lengths
        var lines = optimized.Split('\n');
        var longLines = lines.Count(l => l.Length > 200);
        if (longLines > lines.Length * 0.3)
        {
            score -= 0.1;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double EvaluateTokenEfficiency(string optimized, int tokenBudget)
    {
        var estimatedTokens = EstimateTokens(optimized);
        var ratio = (double)estimatedTokens / tokenBudget;

        if (ratio <= 0.8)
            return 1.0; // Well within budget
        if (ratio <= 1.0)
            return 0.8; // Within budget
        if (ratio <= 1.2)
            return 0.5; // Slightly over budget
        return 0.2; // Significantly over budget
    }

    private static double EvaluateSecurity(string optimized)
    {
        double score = 1.0;

        // Check for security boundaries
        if (optimized.Contains("RETRIEVED CONTEXT") || optimized.Contains("BEGIN RETRIEVED"))
        {
            // Good — security boundaries present
        }
        else if (optimized.Contains("memory") || optimized.Contains("context"))
        {
            score -= 0.1; // May lack explicit boundaries
        }

        // Check for potential injection patterns that survived
        if (optimized.Contains("[ESCAPED]"))
        {
            score -= 0.05; // Sanitization was applied (good but noted)
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
