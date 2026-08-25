namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Comparison of prompt quality across optimization strategies.
/// Provider-independent model for evaluating whether optimization actually improved a prompt.
/// </summary>
public class PromptQualityComparison
{
    /// <summary>Quality score of the original prompt.</summary>
    public double OriginalScore { get; set; }

    /// <summary>Quality score of the deterministically optimized prompt.</summary>
    public double? DeterministicScore { get; set; }

    /// <summary>Quality score of the LLM optimized prompt (null if LLM not used).</summary>
    public double? LlmScore { get; set; }

    /// <summary>Score of the final selected prompt.</summary>
    public double FinalScore { get; set; }

    /// <summary>Improvement over original (positive = better).</summary>
    public double Improvement { get; set; }

    /// <summary>Token delta (final - original, negative = more efficient).</summary>
    public int TokenDelta { get; set; }

    /// <summary>Constraint delta (0 = same, positive = more preserved).</summary>
    public double ConstraintDelta { get; set; }

    /// <summary>Security delta (0 = same, positive = more secure).</summary>
    public double SecurityDelta { get; set; }

    /// <summary>Which variant was selected as final.</summary>
    public string SelectedVariant { get; set; } = "deterministic";

    /// <summary>How the selection was made.</summary>
    public string SelectionReason { get; set; } = string.Empty;

    /// <summary>Whether deterministic fallback was used for the final selection.</summary>
    public bool FallbackUsed { get; set; }

    /// <summary>Whether the selection is deterministic for identical inputs.</summary>
    public bool IsDeterministic { get; set; } = true;
}

/// <summary>
/// Request for evaluating prompt quality.
/// </summary>
public class PromptEvaluationRequest
{
    /// <summary>The original user prompt.</summary>
    public string OriginalPrompt { get; set; } = string.Empty;

    /// <summary>The optimized prompt to evaluate.</summary>
    public string OptimizedPrompt { get; set; } = string.Empty;

    /// <summary>Optional intent analysis result for better evaluation.</summary>
    public IntentAnalysisResult? Intent { get; set; }

    /// <summary>Token budget for the prompt.</summary>
    public int TokenBudget { get; set; } = 4000;

    /// <summary>Evaluation mode: Deterministic, LLM, Hybrid, Auto.</summary>
    public string EvaluationMode { get; set; } = "Auto";

    /// <summary>Correlation ID for tracing.</summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Result of prompt quality evaluation.
/// </summary>
public class PromptEvaluationResult
{
    /// <summary>Final quality score.</summary>
    public PromptQualityScore Score { get; set; } = new();

    /// <summary>LLM quality score (null if LLM not used).</summary>
    public PromptQualityScore? LlmScore { get; set; }

    /// <summary>Whether LLM was used for evaluation.</summary>
    public bool LlmUsed { get; set; }

    /// <summary>Whether deterministic fallback was used.</summary>
    public bool FallbackUsed { get; set; }

    /// <summary>Evaluator name that produced the final score.</summary>
    public string EvaluatorUsed { get; set; } = "deterministic";

    /// <summary>Evaluation duration in milliseconds.</summary>
    public double EvaluationDurationMs { get; set; }

    /// <summary>Evaluation mode used.</summary>
    public string EvaluationMode { get; set; } = "Deterministic";

    /// <summary>Confidence in the evaluation (0.0-1.0).</summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>Issues found during evaluation.</summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>Recommendations for improvement.</summary>
    public List<string> Recommendations { get; set; } = [];
}

/// <summary>
/// Request for comparing prompt candidates.
/// </summary>
public class PromptComparisonRequest
{
    /// <summary>The original user prompt.</summary>
    public string OriginalPrompt { get; set; } = string.Empty;

    /// <summary>Candidate prompts to compare.</summary>
    public List<PromptCandidate> Candidates { get; set; } = [];

    /// <summary>Token budget for comparison.</summary>
    public int TokenBudget { get; set; } = 4000;

    /// <summary>Correlation ID for tracing.</summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// A prompt candidate for comparison.
/// </summary>
public class PromptCandidate
{
    /// <summary>Variant name (e.g., "deterministic", "llm").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The optimized prompt content.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Optimization mode used to produce this candidate.</summary>
    public string OptimizationMode { get; set; } = "Deterministic";

    /// <summary>Whether this candidate is from LLM optimization.</summary>
    public bool IsLlmGenerated { get; set; }
}

/// <summary>
/// Result of comparing prompt candidates.
/// </summary>
public class PromptComparisonResult
{
    /// <summary>Quality comparison across all candidates.</summary>
    public PromptQualityComparison Comparison { get; set; } = new();

    /// <summary>Individual candidate evaluation results.</summary>
    public List<CandidateEvaluation> Evaluations { get; set; } = [];

    /// <summary>The selected best candidate.</summary>
    public CandidateEvaluation? BestCandidate { get; set; }

    /// <summary>Whether the selection is deterministic.</summary>
    public bool IsDeterministic { get; set; } = true;

    /// <summary>Explanation of why this candidate was selected.</summary>
    public string SelectionExplanation { get; set; } = string.Empty;
}

/// <summary>
/// Evaluation result for a single candidate.
/// </summary>
public class CandidateEvaluation
{
    /// <summary>Candidate name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Quality score.</summary>
    public PromptQualityScore Score { get; set; } = new();

    /// <summary>Whether this candidate passed quality gates.</summary>
    public bool PassedQualityGate { get; set; }

    /// <summary>Whether security validation passed.</summary>
    public bool SecurityValidated { get; set; } = true;

    /// <summary>Whether constraints were preserved.</summary>
    public bool ConstraintsPreserved { get; set; } = true;

    /// <summary>Rejection reason (if rejected).</summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Types of experiment assignment strategies.
/// </summary>
public enum AssignmentStrategy
{
    /// <summary>Stable hash-based deterministic assignment.</summary>
    DeterministicHash,

    /// <summary>Round-robin assignment.</summary>
    RoundRobin
}
