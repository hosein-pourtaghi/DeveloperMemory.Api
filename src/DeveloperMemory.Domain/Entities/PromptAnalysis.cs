using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Structured analysis of an incoming user request.
/// Produced by the PromptAnalyzer and consumed by the Prompt Intelligence Engine.
/// </summary>
public class PromptAnalysis
{
    /// <summary>
    /// The original user request text.
    /// </summary>
    public string OriginalRequest { get; set; } = string.Empty;

    /// <summary>
    /// Detected high-level intent.
    /// </summary>
    public IntentType Intent { get; set; } = IntentType.General;

    /// <summary>
    /// Technical task classification.
    /// </summary>
    public TaskType TaskType { get; set; } = TaskType.General;

    /// <summary>
    /// Summary of what the user wants to achieve.
    /// </summary>
    public string UserGoal { get; set; } = string.Empty;

    /// <summary>
    /// Expected output format or type, if determinable.
    /// </summary>
    public string? RequestedOutput { get; set; }

    /// <summary>
    /// Keywords extracted from the request for retrieval.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// References to projects, files, or systems mentioned in the request.
    /// </summary>
    public List<string> ProjectReferences { get; set; } = [];

    /// <summary>
    /// Technical terms or concepts relevant to the request.
    /// </summary>
    public List<string> TechnicalContext { get; set; } = [];

    /// <summary>
    /// Constraints explicitly stated in the current request.
    /// </summary>
    public List<string> ExplicitConstraints { get; set; } = [];
}

/// <summary>
/// Technical task classification, finer-grained than IntentType.
/// </summary>
public enum TaskType
{
    /// <summary>Writing or modifying source code.</summary>
    Coding,

    /// <summary>Fixing bugs or resolving errors.</summary>
    Debugging,

    /// <summary>Designing system architecture or structures.</summary>
    Architecture,

    /// <summary>Writing or editing documentation.</summary>
    Documentation,

    /// <summary>Researching or investigating a topic.</summary>
    Research,

    /// <summary>Explaining concepts or behaviors.</summary>
    Explanation,

    /// <summary>Restructuring code without changing behavior.</summary>
    Refactoring,

    /// <summary>Planning or organizing tasks.</summary>
    Planning,

    /// <summary>General task not fitting specific categories.</summary>
    General,

    /// <summary>Performance analysis or optimization.</summary>
    Performance,

    /// <summary>Security review or hardening.</summary>
    Security,

    /// <summary>Testing or quality assurance.</summary>
    Testing
}
