using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Policy layer between extraction and persistence.
/// The LLM suggests candidates; the application decides.
/// This distinction is mandatory.
///
/// Policy rules:
/// - Validate scope, type, confidence, importance
/// - Check for explicit temporary markers
/// - Apply security filters (no secrets, no injection)
/// - Determine persistence action
/// </summary>
public class MemoryPolicyEngine : IMemoryPolicy
{
    private readonly ILogger<MemoryPolicyEngine> _logger;

    // Content length limits
    private const int MaxContentLength = 10000;
    private const int MinContentLength = 3;

    // Confidence thresholds
    private const double MinConfidenceForPersist = 0.3;
    private const double ReviewThreshold = 0.5;
    private const double HighConfidenceThreshold = 0.8;

    // Temporary context patterns
    private static readonly string[] TemporaryPatterns =
    [
        "today i'm working on",
        "right now i'm",
        "for this session",
        "temporarily",
        "just for now",
        "current task",
        "working on right now"
    ];

    // Security patterns (should not become persistent memory)
    private static readonly string[] SecurityPatterns =
    [
        "api key",
        "secret",
        "password",
        "token",
        "credential",
        "private key",
        "ssh key"
    ];

    public MemoryPolicyEngine(ILogger<MemoryPolicyEngine> logger)
    {
        _logger = logger;
    }

    public MemoryPolicyDecision Evaluate(
        MemoryCandidate candidate,
        IReadOnlyList<MemoryEntry>? relatedMemories = null)
    {
        // ── Step 1: Basic validation ──
        var validation = ValidateCandidate(candidate);
        if (validation != null)
        {
            return validation;
        }

        // ── Step 2: Security check ──
        var securityCheck = CheckSecurity(candidate);
        if (securityCheck != null)
        {
            return securityCheck;
        }

        // ── Step 3: Temporary context check ──
        if (IsTemporaryContext(candidate))
        {
            _logger.LogDebug("Candidate marked as temporary: {Content}", Truncate(candidate.Content, 50));
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Ignore,
                Reason = "Temporary context — not worth persistent memory",
                FinalImportance = candidate.Importance,
                FinalConfidence = candidate.Confidence
            };
        }

        // ── Step 4: Conflict/supersession check ──
        if (relatedMemories?.Count > 0)
        {
            var conflictDecision = CheckForSupersession(candidate, relatedMemories);
            if (conflictDecision != null)
            {
                return conflictDecision;
            }
        }

        // ── Step 5: Confidence-based decision ──
        var importance = Math.Clamp(candidate.Importance, 0.0, 1.0);
        var confidence = Math.Clamp(candidate.Confidence, 0.0, 1.0);

        // Explicit user instructions get higher importance
        if (candidate.MemoryType == MemoryType.Instruction ||
            candidate.MemoryType == MemoryType.UserConstraint)
        {
            importance = Math.Max(importance, 0.8);
        }

        // High confidence → persist
        if (confidence >= HighConfidenceThreshold)
        {
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Persist,
                Reason = $"High confidence ({confidence:F2}) — persisting",
                FinalImportance = importance,
                FinalConfidence = confidence
            };
        }

        // Medium confidence → persist with note
        if (confidence >= MinConfidenceForPersist)
        {
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Persist,
                Reason = $"Moderate confidence ({confidence:F2}) — persisting",
                FinalImportance = importance,
                FinalConfidence = confidence
            };
        }

        // Low confidence → require review
        if (confidence >= ReviewThreshold)
        {
            _logger.LogInformation(
                "Candidate requires review: confidence={Confidence}, type={Type}",
                confidence, candidate.MemoryType);
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.RequiresReview,
                Reason = $"Low confidence ({confidence:F2}) — requires review",
                FinalImportance = importance,
                FinalConfidence = confidence
            };
        }

        // Very low confidence → ignore
        return new MemoryPolicyDecision
        {
            Action = MemoryPolicyAction.Ignore,
            Reason = $"Very low confidence ({confidence:F2}) — ignoring",
            FinalImportance = importance,
            FinalConfidence = confidence
        };
    }

    private MemoryPolicyDecision? ValidateCandidate(MemoryCandidate candidate)
    {
        // Empty content
        if (string.IsNullOrWhiteSpace(candidate.Content))
        {
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Ignore,
                Reason = "Empty content",
                FinalImportance = 0,
                FinalConfidence = 0
            };
        }

        // Too short
        if (candidate.Content.Length < MinContentLength)
        {
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Ignore,
                Reason = $"Content too short ({candidate.Content.Length} chars)",
                FinalImportance = 0,
                FinalConfidence = 0
            };
        }

        // Too long
        if (candidate.Content.Length > MaxContentLength)
        {
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Ignore,
                Reason = $"Content too long ({candidate.Content.Length} chars)",
                FinalImportance = 0,
                FinalConfidence = 0
            };
        }

        // Invalid memory type
        if (!Enum.IsDefined(candidate.MemoryType))
        {
            return new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Ignore,
                Reason = $"Invalid memory type: {candidate.MemoryType}",
                FinalImportance = 0,
                FinalConfidence = 0
            };
        }

        return null;
    }

    private MemoryPolicyDecision? CheckSecurity(MemoryCandidate candidate)
    {
        var contentLower = candidate.Content.ToLowerInvariant();

        foreach (var pattern in SecurityPatterns)
        {
            if (contentLower.Contains(pattern))
            {
                // Check if this is a legitimate constraint about security
                if (candidate.MemoryType == MemoryType.UserConstraint ||
                    candidate.MemoryType == MemoryType.Instruction)
                {
                    // Legitimate security instruction
                    return null;
                }

                // Potential sensitive data
                _logger.LogWarning(
                    "Security filter: candidate contains sensitive pattern '{Pattern}': {Content}",
                    pattern, Truncate(candidate.Content, 50));

                return new MemoryPolicyDecision
                {
                    Action = MemoryPolicyAction.RequiresReview,
                    Reason = $"Contains potentially sensitive content ('{pattern}') — requires review",
                    FinalImportance = candidate.Importance,
                    FinalConfidence = candidate.Confidence
                };
            }
        }

        return null;
    }

    private static bool IsTemporaryContext(MemoryCandidate candidate)
    {
        if (candidate.ExpiresAt.HasValue)
        {
            return true;
        }

        var contentLower = candidate.Content.ToLowerInvariant();
        return TemporaryPatterns.Any(p => contentLower.Contains(p));
    }

    private MemoryPolicyDecision? CheckForSupersession(
        MemoryCandidate candidate,
        IReadOnlyList<MemoryEntry> relatedMemories)
    {
        foreach (var existing in relatedMemories)
        {
            // Check for semantic similarity (simplified)
            var similarity = CalculateContentSimilarity(candidate.Content, existing.Content);

            if (similarity > 0.9)
            {
                // Very similar content — likely duplicate
                return new MemoryPolicyDecision
                {
                    Action = MemoryPolicyAction.Ignore,
                    Reason = $"Similar to existing memory {existing.Id} (similarity={similarity:F2})",
                    FinalImportance = candidate.Importance,
                    FinalConfidence = candidate.Confidence
                };
            }

            if (similarity > 0.6 && candidate.Importance > existing.Importance)
            {
                // Related but different, and candidate is more important — supersede
                return new MemoryPolicyDecision
                {
                    Action = MemoryPolicyAction.Supersede,
                    Reason = $"Supersedes existing memory {existing.Id} (similarity={similarity:F2}, importance increased)",
                    FinalImportance = candidate.Importance,
                    FinalConfidence = candidate.Confidence
                };
            }
        }

        return null;
    }

    private static double CalculateContentSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0.0;
        }

        // Simple Jaccard similarity on words
        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var setA = new HashSet<string>(wordsA);
        var setB = new HashSet<string>(wordsB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}
