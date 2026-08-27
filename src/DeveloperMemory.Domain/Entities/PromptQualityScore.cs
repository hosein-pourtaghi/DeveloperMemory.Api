namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Deterministic prompt quality score.
/// Engineering diagnostic, not an objective measure of intelligence.
/// </summary>
public class PromptQualityScore
{
    /// <summary>Intent preservation score (0.0-1.0).</summary>
    public double IntentPreservation { get; set; } = 1.0;

    /// <summary>Constraint preservation score (0.0-1.0).</summary>
    public double ConstraintPreservation { get; set; } = 1.0;

    /// <summary>Context relevance score (0.0-1.0).</summary>
    public double ContextRelevance { get; set; } = 1.0;

    /// <summary>Structure score (0.0-1.0).</summary>
    public double Structure { get; set; } = 1.0;

    /// <summary>Token efficiency score (0.0-1.0).</summary>
    public double TokenEfficiency { get; set; } = 1.0;

    /// <summary>Security validation score (0.0-1.0).</summary>
    public double SecurityValidation { get; set; } = 1.0;

    /// <summary>Overall aggregate score (0.0-1.0).</summary>
    public double Overall { get; set; }

    /// <summary>When this score was computed.</summary>
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Evaluator name (e.g., "deterministic", "llm").</summary>
    public string Evaluator { get; set; } = "deterministic";

    /// <summary>Evaluator version.</summary>
    public string EvaluatorVersion { get; set; } = "1.0";

    /// <summary>Issues found during evaluation.</summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>Recommendations for improvement.</summary>
    public List<string> Recommendations { get; set; } = [];

    /// <summary>
    /// Computes the overall score from dimension scores.
    /// </summary>
    public void ComputeOverall()
    {
        Overall = (IntentPreservation * 0.25 +
                   ConstraintPreservation * 0.25 +
                   ContextRelevance * 0.15 +
                   Structure * 0.15 +
                   TokenEfficiency * 0.10 +
                   SecurityValidation * 0.10);
    }

    /// <summary>
    /// Whether the score meets minimum quality thresholds.
    /// </summary>
    public bool MeetsThresholds(double minOverall = 0.70, double minConstraint = 0.90, double minSecurity = 0.90)
    {
        return Overall >= minOverall &&
               ConstraintPreservation >= minConstraint &&
               SecurityValidation >= minSecurity;
    }
}
