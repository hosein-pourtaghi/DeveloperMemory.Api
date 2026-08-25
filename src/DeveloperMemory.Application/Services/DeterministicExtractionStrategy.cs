using System.Text.RegularExpressions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic memory extraction strategy.
/// Uses pattern matching and heuristics to identify memory candidates
/// from structured input. Does NOT require an LLM.
/// 
/// This strategy is suitable for explicit/structured ingestion.
/// Future strategies may use LLM-based extraction for implicit memory.
/// </summary>
public partial class DeterministicExtractionStrategy : IMemoryExtractionStrategy
{
    public string StrategyName => "deterministic";

    public Task<IReadOnlyCollection<MemoryCandidate>> ExtractAsync(
        MemoryExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Task.FromResult<IReadOnlyCollection<MemoryCandidate>>([]);
        }

        var candidates = new List<MemoryCandidate>();

        // ── Pattern 1: Preference patterns ──
        var preferences = ExtractPreferences(request);
        candidates.AddRange(preferences);

        // ── Pattern 2: Instruction patterns ──
        var instructions = ExtractInstructions(request);
        candidates.AddRange(instructions);

        // ── Pattern 3: Fact patterns ──
        var facts = ExtractFacts(request);
        candidates.AddRange(facts);

        // ── Pattern 4: Constraint patterns ──
        var constraints = ExtractConstraints(request);
        candidates.AddRange(constraints);

        return Task.FromResult<IReadOnlyCollection<MemoryCandidate>>(candidates.DistinctBy(c => c.Content).ToList());
    }

    private static List<MemoryCandidate> ExtractPreferences(MemoryExtractionRequest request)
    {
        var candidates = new List<MemoryCandidate>();
        var patterns = new[]
        {
            @"(?:prefer|likes?|uses?|chose?|going with)\s+(.+?)(?:\.|$)",
            @"(?:favorite|preferred)\s+(.+?)(?:\.|$)"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(request.Content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                candidates.Add(new MemoryCandidate
                {
                    Title = "User Preference",
                    Content = match.Value.Trim(),
                    MemoryType = MemoryType.UserPreference,
                    Importance = 0.6,
                    Confidence = 0.8,
                    Source = request.Source ?? "extraction",
                    ExtractionReason = $"Matched preference pattern: {pattern}"
                });
            }
        }

        return candidates;
    }

    private static List<MemoryCandidate> ExtractInstructions(MemoryExtractionRequest request)
    {
        var candidates = new List<MemoryCandidate>();
        var patterns = new[]
        {
            @"(?:always|must|shall|make sure to|don't forget to|remember to)\s+(.+?)(?:\.|$)",
            @"(?:never|don't|do not|avoid)\s+(.+?)(?:\.|$)"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(request.Content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                candidates.Add(new MemoryCandidate
                {
                    Title = "Instruction",
                    Content = match.Value.Trim(),
                    MemoryType = MemoryType.Instruction,
                    Importance = 0.9,
                    Confidence = 0.85,
                    Source = request.Source ?? "extraction",
                    ExtractionReason = $"Matched instruction pattern: {pattern}"
                });
            }
        }

        return candidates;
    }

    private static List<MemoryCandidate> ExtractFacts(MemoryExtractionRequest request)
    {
        var candidates = new List<MemoryCandidate>();
        var patterns = new[]
        {
            @"(?:the project|this project|we use|we are using|the system)\s+(.+?)(?:\.|$)",
            @"(?:technology|framework|library|database)\s+(?:is|are|uses?)\s+(.+?)(?:\.|$)"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(request.Content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                candidates.Add(new MemoryCandidate
                {
                    Title = "Fact",
                    Content = match.Value.Trim(),
                    MemoryType = MemoryType.Fact,
                    Importance = 0.5,
                    Confidence = 0.7,
                    Source = request.Source ?? "extraction",
                    ExtractionReason = $"Matched fact pattern: {pattern}"
                });
            }
        }

        return candidates;
    }

    private static List<MemoryCandidate> ExtractConstraints(MemoryExtractionRequest request)
    {
        var candidates = new List<MemoryCandidate>();
        var patterns = new[]
        {
            @"(?:must not|cannot|should not|do not)\s+(.+?)(?:\.|$)",
            @"(?:no|without|banning|prohibiting)\s+(.+?)(?:\.|$)"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(request.Content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                candidates.Add(new MemoryCandidate
                {
                    Title = "Constraint",
                    Content = match.Value.Trim(),
                    MemoryType = MemoryType.UserConstraint,
                    Importance = 0.8,
                    Confidence = 0.75,
                    Source = request.Source ?? "extraction",
                    ExtractionReason = $"Matched constraint pattern: {pattern}"
                });
            }
        }

        return candidates;
    }
}
