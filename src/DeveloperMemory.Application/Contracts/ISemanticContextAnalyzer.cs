namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// LLM-assisted semantic analysis of conversational messages.
/// Used when deterministic/pattern-based detection produces ambiguous results.
/// 
/// The analyzer determines:
/// - Is this information potentially durable?
/// - What type of information is it?
/// - Is it user-level or project-level?
/// - Is it temporary or permanent?
/// - Is it a decision, preference, or constraint?
/// - Does it contradict existing information?
/// - How confident is the interpretation?
/// 
/// This service is called ONLY for ambiguous messages where the deterministic
/// detector produces a confidence score between 0.3 and 0.7.
/// It must NOT be called for every message.
/// </summary>
public interface ISemanticContextAnalyzer
{
    /// <summary>
    /// Performs semantic analysis on a message to determine its memory potential.
    /// Returns null if the LLM is unavailable or the analysis fails.
    /// </summary>
    Task<SemanticAnalysisResult?> AnalyzeAsync(
        string message,
        List<string>? conversationHistory = null,
        string? currentProjectName = null,
        CancellationToken ct = default);
}

/// <summary>
/// Result of semantic analysis by the LLM-assisted analyzer.
/// </summary>
public class SemanticAnalysisResult
{
    /// <summary>Whether the message contains durable information worth persisting.</summary>
    public bool ContainsDurableInformation { get; set; }

    /// <summary>Confidence in the analysis (0.0-1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Suggested memory type if durable.</summary>
    public string? SuggestedMemoryType { get; set; }

    /// <summary>Whether this is user-level or project-level information.</summary>
    public string? Scope { get; set; }

    /// <summary>Whether this is temporary (should not be persisted permanently).</summary>
    public bool IsTemporary { get; set; }

    /// <summary>Whether this is an explicit "remember" request from the user.</summary>
    public bool IsExplicitMemoryRequest { get; set; }

    /// <summary>The extracted durable content.</summary>
    public string? ExtractedContent { get; set; }

    /// <summary>Brief explanation of the analysis.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Project name if the analysis determines project-level scope.</summary>
    public string? ProjectName { get; set; }

    /// <summary>Whether this contradicts existing known information.</summary>
    public bool PotentialContradiction { get; set; }
}
