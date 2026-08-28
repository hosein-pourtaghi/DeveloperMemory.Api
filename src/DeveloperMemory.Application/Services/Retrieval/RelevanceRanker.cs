using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Deterministic relevance ranking for memory candidates.
/// Scoring is provider-independent and easily replaceable.
/// Scoring model: the weighted components below are divided by 1.20,
/// preserving their relative importance while keeping the result in [0, 1].
/// </summary>
public class RelevanceRanker : IRetrievalRanker
{
    // Scoring weights — easily configurable or injectable in the future
    private const double TextWeight = 0.35;
    private const double SemanticWeight = 0.25;
    private const double ScopeWeight = 0.10;
    private const double ProjectWeight = 0.10;
    private const double RecencyWeight = 0.15;
    private const double ImportanceWeight = 0.15;
    private const double CategoryWeight = 0.10;
    private const double TotalWeight = TextWeight + SemanticWeight + ScopeWeight +
                                       ProjectWeight + RecencyWeight + ImportanceWeight + CategoryWeight;

    /// <summary>
    /// Scores and ranks candidate memories by relevance.
    /// </summary>
    public Task<List<RetrievedMemory>> RankAsync(
        List<RetrievedMemory> candidates,
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        foreach (var candidate in candidates)
        {
            var breakdown = new RetrievalScoreBreakdown
            {
                TextRelevance = CalculateTextRelevance(candidate, request),
                SemanticRelevance = candidate.SemanticRelevanceScore ?? 0.0,
                ScopeRelevance = CalculateScopeRelevance(candidate, request),
                ProjectRelevance = CalculateProjectRelevance(candidate, request),
                RecencyScore = CalculateRecencyScore(candidate),
                ImportanceScore = CalculateImportanceScore(candidate),
                CategoryScore = CalculateCategoryScore(candidate, request)
            };

            candidate.ScoreBreakdown = breakdown;
            candidate.RelevanceScore = (
                (breakdown.TextRelevance * TextWeight) +
                (breakdown.SemanticRelevance * SemanticWeight) +
                (breakdown.ScopeRelevance * ScopeWeight) +
                (breakdown.ProjectRelevance * ProjectWeight) +
                (breakdown.RecencyScore * RecencyWeight) +
                (breakdown.ImportanceScore * ImportanceWeight) +
                (breakdown.CategoryScore * CategoryWeight)) / TotalWeight;

            // Ensure score is clamped to [0, 1]
            candidate.RelevanceScore = Math.Clamp(candidate.RelevanceScore, 0.0, 1.0);
            if (candidate.ScoreBreakdown.TextRelevance == 0.0 &&
                candidate.ScoreBreakdown.SemanticRelevance == 0.0 &&
                candidate.ScoreBreakdown.ScopeRelevance == 0.0 &&
                candidate.ScoreBreakdown.ProjectRelevance == 0.0 &&
                candidate.ScoreBreakdown.RecencyScore == 0.0 &&
                candidate.ScoreBreakdown.ImportanceScore == 0.0 &&
                candidate.ScoreBreakdown.CategoryScore == 0.0)
            {
                candidate.RelevanceScore = 0.0;
            }
        }

        // Sort by relevance descending, then stable tie-breaking:
        //   1. RelevanceScore (primary)
        //   2. Importance (secondary — higher is more relevant)
        //   3. UpdatedAt (tertiary — more recent is preferred)
        //   4. MemoryId (quaternary — deterministic for identical inputs)
        var ranked = candidates
            .OrderByDescending(c => c.RelevanceScore)
            .ThenByDescending(c => c.Importance)
            .ThenByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.MemoryId)
            .ToList();

        return Task.FromResult(ranked);
    }

    private static double CalculateTextRelevance(RetrievedMemory memory, RetrievalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return 0.0; // No textual signal when no query

        var queryLower = request.Query.ToLowerInvariant();
        var score = 0.0;

        // Exact title match — highest signal
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

        // Token-level matching (simplistic word overlap)
        var queryTokens = queryLower.Split([' ', ',', '.', ';', ':', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries);
        var contentLower = (memory.Title + " " + memory.Content).ToLowerInvariant();
        var matchCount = queryTokens.Count(t => contentLower.Contains(t));
        if (queryTokens.Length > 0)
        {
            var tokenScore = (double)matchCount / queryTokens.Length;
            score += tokenScore * 0.2;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double CalculateScopeRelevance(RetrievedMemory memory, RetrievalRequest request)
    {
        if (memory.Scope == MemoryScope.Global)
            return 0.0;
        if (memory.Scope == MemoryScope.Project && request.ProjectId.HasValue && memory.ProjectId == request.ProjectId)
            return 1.0;
        if (memory.Scope == MemoryScope.Workspace && !string.IsNullOrEmpty(request.WorkspaceId) && memory.WorkspaceId == request.WorkspaceId)
            return 1.0;
        if (memory.Scope == MemoryScope.Private && !string.IsNullOrEmpty(request.UserId) && memory.UserId == request.UserId)
            return 1.0;
        return 0.0;
    }

    private static double CalculateProjectRelevance(RetrievedMemory memory, RetrievalRequest request)
    {
        if (!request.ProjectId.HasValue)
            return 0.0; // No project signal when no project context

        if (memory.ProjectId == request.ProjectId.Value)
            return 1.0; // Exact project match

        if (memory.ProjectId.HasValue)
            return 0.1; // Different project — very low

        // Global memory is eligible, but has no project-specific signal.
        return 0.0;
    }

    private static double CalculateRecencyScore(RetrievedMemory memory)
    {
        if (memory.UpdatedAt == default)
            return 0.0;

        var age = DateTime.UtcNow - memory.UpdatedAt;

        return age.TotalDays switch
        {
            < 1 => 1.0,      // Updated today
            < 7 => 0.9,      // Updated this week
            < 30 => 0.75,    // Updated this month
            < 90 => 0.5,     // Updated this quarter
            < 365 => 0.3,    // Updated this year
            _ => 0.1         // Older than a year
        };
    }

    private static double CalculateImportanceScore(RetrievedMemory memory)
    {
        // Importance is already 0.0–1.0
        return memory.Importance;
    }

    private static double CalculateCategoryScore(RetrievedMemory memory, RetrievalRequest request)
    {
        if (request.RequiredCategories == null || request.RequiredCategories.Count == 0)
            return 0.0; // No category signal when no category filter

        // Check if any tags match required categories
        var matchCount = memory.Tags
            .Count(t => request.RequiredCategories.Contains(t, StringComparer.OrdinalIgnoreCase));

        if (matchCount > 0)
            return 1.0;

        return 0.2; // No category match but still eligible
    }
}
