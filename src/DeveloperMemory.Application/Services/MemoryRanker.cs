using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic memory ranker using multiple signals.
/// 
/// Ranking signals:
///   - Text relevance (keyword matching)
///   - Memory type relevance (instruction > preference > fact > other)
///   - Importance (0.0-1.0)
///   - Confidence (0.0-1.0)
///   - Recency (more recent = higher)
///   - Access frequency (more accessed = higher)
///   - Scope specificity (project > workspace > private > global)
/// 
/// The ranking is deterministic, explainable, and replaceable.
/// Future: EmbeddingRanker, HybridRanker, LLMReranker.
/// </summary>
public class MemoryRanker : IMemoryRanker
{
    // Weights — easily configurable or injectable in the future
    private const double TextWeight = 0.30;
    private const double TypeWeight = 0.15;
    private const double ImportanceWeight = 0.20;
    private const double ConfidenceWeight = 0.15;
    private const double RecencyWeight = 0.10;
    private const double AccessWeight = 0.05;
    private const double ScopeWeight = 0.05;

    public IReadOnlyList<RankedMemory> Rank(
        IReadOnlyList<MemoryEntry> candidates,
        string query,
        RankingContext? context = null)
    {
        var ranked = new List<RankedMemory>();

        foreach (var candidate in candidates)
        {
            var signals = new RankingSignals
            {
                TextRelevance = CalculateTextRelevance(candidate, query),
                TypeRelevance = CalculateTypeRelevance(candidate),
                ImportanceScore = candidate.Importance,
                ConfidenceScore = candidate.Confidence,
                RecencyScore = CalculateRecencyScore(candidate),
                AccessFrequencyScore = CalculateAccessFrequency(candidate),
                ScopeSpecificityScore = CalculateScopeScore(candidate, context)
            };

            var score =
                signals.TextRelevance * TextWeight +
                signals.TypeRelevance * TypeWeight +
                signals.ImportanceScore * ImportanceWeight +
                signals.ConfidenceScore * ConfidenceWeight +
                signals.RecencyScore * RecencyWeight +
                signals.AccessFrequencyScore * AccessWeight +
                signals.ScopeSpecificityScore * ScopeWeight;

            score = Math.Clamp(score, 0.0, 1.0);

            ranked.Add(new RankedMemory
            {
                Memory = candidate,
                RelevanceScore = score,
                Signals = signals,
                SelectionReason = BuildReason(candidate, signals, score)
            });
        }

        // Deterministic ordering: score, then importance, then recency, then ID
        return ranked
            .OrderByDescending(r => r.RelevanceScore)
            .ThenByDescending(r => r.Memory.Importance)
            .ThenByDescending(r => r.Memory.UpdatedAt)
            .ThenBy(r => r.Memory.Id)
            .ToList();
    }

    private static double CalculateTextRelevance(MemoryEntry memory, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return 0.3; // Neutral

        var queryLower = query.ToLowerInvariant();
        var score = 0.0;

        // Title match — highest signal
        if (memory.Title.ToLowerInvariant().Contains(queryLower))
            score += 0.5;

        // Content match
        if (memory.Content.ToLowerInvariant().Contains(queryLower))
            score += 0.3;

        // Tag matches
        foreach (var tag in memory.Tags)
        {
            if (tag.ToLowerInvariant().Contains(queryLower))
                score += 0.1;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double CalculateTypeRelevance(MemoryEntry memory)
    {
        // Instructions and constraints are highest value for prompt construction
        return memory.MemoryType switch
        {
            MemoryType.Instruction => 1.0,
            MemoryType.UserConstraint => 0.95,
            MemoryType.ArchitectureDecision => 0.9,
            MemoryType.TechnicalDecision => 0.85,
            MemoryType.ProjectContext => 0.8,
            MemoryType.UserPreference => 0.75,
            MemoryType.UserGoal => 0.7,
            MemoryType.Fact => 0.6,
            MemoryType.WorkingContext => 0.5,
            MemoryType.ConversationContext => 0.3,
            _ => 0.4
        };
    }

    private static double CalculateRecencyScore(MemoryEntry memory)
    {
        var age = DateTime.UtcNow - memory.UpdatedAt;
        return age.TotalDays switch
        {
            < 1 => 1.0,
            < 7 => 0.9,
            < 30 => 0.75,
            < 90 => 0.5,
            < 365 => 0.3,
            _ => 0.1
        };
    }

    private static double CalculateAccessFrequency(MemoryEntry memory)
    {
        // Logarithmic scale: 0 accesses = 0.0, 1 = 0.3, 10 = 0.7, 100 = 1.0
        if (memory.AccessCount == 0) return 0.0;
        return Math.Clamp(Math.Log10(memory.AccessCount) / 2.0, 0.0, 1.0);
    }

    private static double CalculateScopeScore(MemoryEntry memory, RankingContext? context)
    {
        if (context == null) return 0.5;

        // More specific scope = higher relevance when context matches
        if (memory.Scope == MemoryScope.Project && context.ProjectId.HasValue &&
            memory.ProjectId == context.ProjectId)
            return 1.0;

        if (memory.Scope == MemoryScope.Workspace && !string.IsNullOrEmpty(context.WorkspaceId) &&
            string.Equals(memory.WorkspaceId, context.WorkspaceId, StringComparison.Ordinal))
            return 0.9;

        if (memory.Scope == MemoryScope.Private && !string.IsNullOrEmpty(context.UserId) &&
            string.Equals(memory.UserId, context.UserId, StringComparison.Ordinal))
            return 0.8;

        // Global memories get moderate score
        if (memory.Scope == MemoryScope.Global) return 0.5;

        return 0.3;
    }

    private static string BuildReason(MemoryEntry memory, RankingSignals signals, double score)
    {
        var reasons = new List<string>();

        if (signals.TextRelevance > 0.5) reasons.Add("high text match");
        if (signals.ImportanceScore >= 0.8) reasons.Add("high importance");
        if (signals.ConfidenceScore >= 0.9) reasons.Add("high confidence");
        if (signals.RecencyScore >= 0.9) reasons.Add("recent");
        if (signals.TypeRelevance >= 0.8) reasons.Add($"type={memory.MemoryType}");

        return reasons.Count > 0
            ? $"Score {score:F2}: {string.Join(", ", reasons)}"
            : $"Score {score:F2}";
    }
}
