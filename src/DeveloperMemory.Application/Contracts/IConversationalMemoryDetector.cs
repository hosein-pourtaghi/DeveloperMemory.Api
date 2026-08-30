namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Detects whether a user message contains durable information worth persisting
/// as a memory. Uses pattern matching and heuristics — does NOT require an LLM.
/// 
/// The detector is conservative: it prefers false negatives over false positives.
/// It must NOT depend solely on words like "remember" or "save".
/// </summary>
public interface IConversationalMemoryDetector
{
    /// <summary>
    /// Analyzes a user message and determines whether it contains durable information
    /// worth storing as a memory.
    /// </summary>
    /// <param name="message">The user message to analyze.</param>
    /// <param name="conversationContext">Optional recent conversation context for inference.</param>
    /// <returns>A detection result indicating whether the message is memory-worthy.</returns>
    ConversationalMemoryDetectionResult Detect(string message, List<string>? conversationContext = null);
}

/// <summary>
/// Result of conversational memory detection.
/// </summary>
public class ConversationalMemoryDetectionResult
{
    /// <summary>Whether the message contains durable information worth storing.</summary>
    public bool ContainsDurableInformation { get; set; }

    /// <summary>Confidence in the detection (0.0-1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Reason for the detection decision.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Suggested memory type if the message is memory-worthy.</summary>
    public string? SuggestedMemoryType { get; set; }

    /// <summary>Extracted core content that should be persisted (the durable fact).</summary>
    public string? ExtractedContent { get; set; }
}
