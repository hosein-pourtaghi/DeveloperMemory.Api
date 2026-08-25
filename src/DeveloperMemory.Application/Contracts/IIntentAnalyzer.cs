using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Analyzes user intent from raw input.
/// Provider-independent abstraction with replaceable implementations.
/// </summary>
public interface IIntentAnalyzer
{
    /// <summary>
    /// Analyzes the input and returns structured intent analysis.
    /// </summary>
    Task<IntentAnalysisResult> AnalyzeAsync(
        string input,
        PromptContext? context = null,
        CancellationToken ct = default);
}

/// <summary>
/// Structured result of intent analysis.
/// </summary>
public class IntentAnalysisResult
{
    /// <summary>The primary intent type.</summary>
    public IntentType Intent { get; set; } = IntentType.General;

    /// <summary>The task type classification.</summary>
    public TaskType TaskType { get; set; } = TaskType.General;

    /// <summary>Technical domain (e.g., "Database", "API", "Architecture").</summary>
    public string TechnicalDomain { get; set; } = string.Empty;

    /// <summary>Required context types for this request.</summary>
    public List<RequiredContextType> RequiredContext { get; set; } = [];

    /// <summary>Risk level of the request.</summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Normal;

    /// <summary>Estimated complexity.</summary>
    public ComplexityLevel Complexity { get; set; } = ComplexityLevel.Medium;

    /// <summary>Extracted keywords.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Technical context identifiers.</summary>
    public List<string> TechnicalContext { get; set; } = [];

    /// <summary>Explicit constraints found in input.</summary>
    public List<string> ExplicitConstraints { get; set; } = [];

    /// <summary>The original input.</summary>
    public string OriginalInput { get; set; } = string.Empty;

    /// <summary>Short goal summary.</summary>
    public string GoalSummary { get; set; } = string.Empty;

    /// <summary>Whether this is a memory instruction.</summary>
    public bool IsMemoryInstruction { get; set; }

    /// <summary>Whether this requires project context.</summary>
    public bool RequiresProjectContext { get; set; }

    /// <summary>Whether this is a general/simple query.</summary>
    public bool IsSimpleQuery { get; set; }
}

/// <summary>
/// Types of context that may be required.
/// </summary>
public enum RequiredContextType
{
    Memory,
    ProjectArchitecture,
    ProjectDecisions,
    CodingConventions,
    TechnicalStack,
    Rules,
    None
}

/// <summary>
/// Risk level classification.
/// </summary>
public enum RiskLevel
{
    Low,
    Normal,
    Elevated,
    High
}

/// <summary>
/// Complexity level classification.
/// </summary>
public enum ComplexityLevel
{
    Simple,
    Medium,
    Complex,
    Expert
}
