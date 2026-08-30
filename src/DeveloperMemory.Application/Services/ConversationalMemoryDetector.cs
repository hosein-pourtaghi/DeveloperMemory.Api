using System.Text.RegularExpressions;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Pattern-based conversational memory detector.
/// Identifies durable user information in chat messages using heuristics.
/// 
/// Detection categories:
///   1. Explicit memory signals ("remember that...", "I prefer...")
///   2. Preference statements ("I like...", "I use...")
///   3. Constraint statements ("Don't recommend...", "never use...")
///   4. Identity/fact statements ("My default agent is...", "I am a...")
///   5. Project context statements ("In X project we use...")
/// 
/// Non-memory patterns (filtered out):
///   - Questions ("What is...", "How does...")
///   - Imperatives about immediate tasks ("Fix this...", "Write code for...")
///   - Transient references ("today", "right now")
/// 
/// The detector is conservative: it prefers false negatives over false positives.
/// </summary>
public class ConversationalMemoryDetector : IConversationalMemoryDetector
{
    private readonly ILogger<ConversationalMemoryDetector> _logger;

    // ── Negative patterns: messages that should NOT become memories ──
    private static readonly (string Pattern, double Weight)[] NegativePatterns =
    [
        // Questions — asking for information, not stating durable facts
        (@"^(?:what|how|why|when|where|who|which|can you|could you|please|explain|describe|tell me)\s", 1.0),
        // Imperatives — immediate task instructions, not durable preferences
        (@"^(?:fix|debug|write|create|implement|add|remove|update|delete|change|refactor|optimize|test|run|build|deploy)\s", 0.9),
        // Code generation requests
        (@"(?:write (?:a |the |me )?(?:function|class|method|code|script|program|module|component))", 0.8),
        // Error debugging
        (@"(?:exception|error|bug|stack trace|crash|failing|broken|doesn't work|not working|issue with)", 0.7),
        // File/line specific requests
        (@"(?:in (?:the |this )?(?:file|line|method|class|function))", 0.6),
        // Temporary context
        (@"^(?:today|right now|currently|for this session|temporarily|just for now|at the moment)\b", 0.9),
        // Explanatory requests
        (@"^(?:explain|describe|show|list|compare|summarize|review)\s", 0.7),
    ];

    // ── Positive patterns: durable information signals ──
    private static readonly (string Pattern, MemoryType SuggestedType, double Weight)[] PositivePatterns =
    [
        // Explicit memory signals
        (@"(?:remember|save|store|note)\s+(?:that\s+)?(.+)", MemoryType.Instruction, 0.95),

        // Preference patterns
        (@"(?:i\s+)?(?:prefer|like|love|enjoy|favor)\s+(.+)", MemoryType.UserPreference, 0.9),
        (@"(?:i\s+)?(?:always|typically|usually|generally)\s+(?:use|choose|go with|pick|select)\s+(.+)", MemoryType.UserPreference, 0.85),

        // Usage/habit patterns
        (@"(?:i\s+)?(?:use|uses|am using|'m using|have been using)\s+(.+)", MemoryType.UserPreference, 0.8),

        // Constraint patterns
        (@"(?:don'?t|do not|never|avoid|stop|no)\s+(?:recommend|suggest|use|include|show)\s+(.+)", MemoryType.UserConstraint, 0.9),
        (@"(?:must not|cannot|should not|won'?t)\s+(.+)", MemoryType.UserConstraint, 0.85),

        // Identity/fact patterns
        (@"(?:i\s+)?(?:am|'m|is|are)\s+(?:a |an |the )?(.+)", MemoryType.Fact, 0.7),
        (@"(?:my\s+(?:default|primary|main|preferred|favorite)\s+\w+\s+(?:is|are))\s+(.+)", MemoryType.Fact, 0.85),
        (@"(?:i\s+)?(?:want|want to|would like)\s+(.+)", MemoryType.UserGoal, 0.75),

        // Project context patterns
        (@"(?:in|for|on)\s+(?:the\s+)?(.+?)(?:\s+(?:project|app|service|api|system|codebase))\s+(?:,?\s*(?:we|i)\s+)?(?:use|uses|are using|have|follow|follows)\s+(.+)", MemoryType.ProjectContext, 0.8),
        (@"(?:this project|the project)\s+(?:uses?|has|follows?|is built with)\s+(.+)", MemoryType.ProjectContext, 0.8),

        // Architecture/technical decisions
        (@"(?:i\s+)?(?:want|'d like|would prefer)\s+(?:cloud-first|on-premise|serverless|monolith|microservices)\s+architecture", MemoryType.ArchitectureDecision, 0.85),
        (@"(?:using|adopting|chose|selected|decided on)\s+(.+?)(?:\s+for\s+.+)?$", MemoryType.TechnicalDecision, 0.75),
    ];

    // ── Content quality patterns ──
    private static readonly string[] LowValueContent =
    [
        "thanks", "thank you", "ok", "okay", "sure", "got it",
        "sounds good", "great", "perfect", "awesome", "nice",
        "hello", "hi", "hey", "bye", "goodbye",
        "yes", "no", "maybe", "i see", "understood"
    ];

    public ConversationalMemoryDetector(ILogger<ConversationalMemoryDetector> logger)
    {
        _logger = logger;
    }

    public ConversationalMemoryDetectionResult Detect(string message, List<string>? conversationContext = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0,
                Reason = "Empty message"
            };
        }

        var trimmed = message.Trim();

        // Filter out very short messages (likely not durable)
        if (trimmed.Length < 10)
        {
            return new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0,
                Reason = "Message too short to contain durable information"
            };
        }

        // Filter out low-value content
        if (IsLowValueContent(trimmed))
        {
            return new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0,
                Reason = "Message is low-value conversational content"
            };
        }

        // Check negative patterns first
        var negativeScore = EvaluateNegativePatterns(trimmed);
        if (negativeScore >= 0.8)
        {
            _logger.LogDebug(
                "Memory candidate rejected: negative pattern match (score={Score}) for message: {Preview}",
                negativeScore, Truncate(trimmed, 50));
            return new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 1.0 - negativeScore,
                Reason = $"Message matches negative pattern (score: {negativeScore:F2})"
            };
        }

        // Check positive patterns
        var (bestMatch, suggestedType, positiveScore) = EvaluatePositivePatterns(trimmed);

        // Combined score: positive match weighted against negative patterns
        var combinedScore = positiveScore * (1.0 - negativeScore * 0.5);

        // Minimum threshold for detection
        if (combinedScore < 0.5 || bestMatch == null)
        {
            _logger.LogDebug(
                "Memory candidate rejected: insufficient positive signal (combined={Combined:F2}, positive={Positive:F2}, negative={Negative:F2})",
                combinedScore, positiveScore, negativeScore);
            return new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = combinedScore,
                Reason = combinedScore < 0.3
                    ? "No durable information patterns detected"
                    : $"Weak signal (confidence: {combinedScore:F2}) — conservatively rejected"
            };
        }

        // Extract the durable content from the match
        var extractedContent = ExtractDurableContent(trimmed, bestMatch);

        _logger.LogInformation(
            "Memory candidate detected: type={Type}, confidence={Confidence:F2}, source={Source}",
            suggestedType, combinedScore, Truncate(extractedContent ?? trimmed, 50));

        return new ConversationalMemoryDetectionResult
        {
            ContainsDurableInformation = true,
            Confidence = Math.Clamp(combinedScore, 0.0, 1.0),
            Reason = $"Matched positive pattern: {bestMatch}",
            SuggestedMemoryType = suggestedType.ToString(),
            ExtractedContent = extractedContent
        };
    }

    private static bool IsLowValueContent(string message)
    {
        var lower = message.ToLowerInvariant().TrimEnd('.', '!', '?', ',');
        return LowValueContent.Any(lv => lower == lv);
    }

    private static double EvaluateNegativePatterns(string message)
    {
        var maxScore = 0.0;
        foreach (var (pattern, weight) in NegativePatterns)
        {
            if (Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase))
            {
                maxScore = Math.Max(maxScore, weight);
            }
        }
        return maxScore;
    }

    private static (string? Pattern, MemoryType Type, double Score) EvaluatePositivePatterns(string message)
    {
        string? bestPattern = null;
        var bestType = MemoryType.Other;
        var bestScore = 0.0;

        foreach (var (pattern, suggestedType, weight) in PositivePatterns)
        {
            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && weight > bestScore)
            {
                bestPattern = pattern;
                bestType = suggestedType;
                bestScore = weight;
            }
        }

        return (bestPattern, bestType, bestScore);
    }

    private static string? ExtractDurableContent(string message, string pattern)
    {
        try
        {
            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                // Return the captured group content (the actual durable fact)
                var captured = match.Groups[1].Value.Trim();
                if (captured.Length > 5) // Minimum viable content
                {
                    return captured;
                }
            }
        }
        catch
        {
            // Regex extraction failure — fall through to return the full message
        }

        return null; // Caller should use the full message
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}
