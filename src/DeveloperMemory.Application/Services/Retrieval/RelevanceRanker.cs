using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Deterministic relevance ranking for memory candidates.
/// Scoring is provider-independent and easily replaceable.
/// 
/// Scoring model:
///   FinalScore = (TextRelevance × 0.35) +
///                (ScopeRelevance × 0.15) +
///                (ProjectRelevance × 0.10) +
///                (RecencyScore × 0.15) +
///                (ImportanceScore × 0.15) +
///                (CategoryScore × 0.10)
/// </summary>
public class RelevanceRanker : IRetrievalRanker
{
    // Scoring weights — easily configurable or injectable in the future
    private const double TextWeight = 0.35;
    private const double ScopeWeight = 0.15;
    private const double ProjectWeight = 0.10;
    private const double RecencyWeight = 0.15;
    private const double ImportanceWeight = 0.15;
    private const double CategoryWeight = 0.10;

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
                ScopeRelevance = CalculateScopeRelevance(candidate),
                ProjectRelevance = CalculateProjectRelevance(candidate, request),
                RecencyScore = CalculateRecencyScore(candidate),
                ImportanceScore = CalculateImportanceScore(candidate),
                CategoryScore = CalculateCategoryScore(candidate, request)
            };

            candidate.ScoreBreakdown = breakdown;
            candidate.RelevanceScore =
                (breakdown.TextRelevance * TextWeight) +
                (breakdown.ScopeRelevance * ScopeWeight) +
                (breakdown.ProjectRelevance * ProjectWeight) +
                (breakdown.RecencyScore * RecencyWeight) +
                (breakdown.ImportanceScore * ImportanceWeight) +
                (breakdown.CategoryScore * CategoryWeight);

            // Ensure score is clamped to [0, 1]
            candidate.RelevanceScore = Math.Clamp(candidate.RelevanceScore, 0.0, 1.0);
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
            return 0.3; // Neutral score when no query

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

    private static double CalculateScopeRelevance(RetrievedMemory memory)
    {
        // Global memories get a moderate score (always available but less specific)
        // Project-scoped memories in a project context get the highest score
        return memory.Scope switch
        {
            MemoryScope.Project => 0.9,
            MemoryScope.Workspace => 0.8,
            MemoryScope.Global => 0.6,
            MemoryScope.Private => 0.7,
            _ => 0.5
        };
    }

    private static double CalculateProjectRelevance(RetrievedMemory memory, RetrievalRequest request)
    {
        if (!request.ProjectId.HasValue)
            return 0.5; // Neutral when no project context

        if (memory.ProjectId == request.ProjectId.Value)
            return 1.0; // Exact project match

        if (memory.ProjectId.HasValue)
            return 0.1; // Different project — very low

        // Global memory — relevant but not project-specific
        return 0.5;
    }

    private static double CalculateRecencyScore(RetrievedMemory memory)
    {
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
            return 0.5; // Neutral when no category filter

        // Check if any tags match required categories
        var matchCount = memory.Tags
            .Count(t => request.RequiredCategories.Contains(t, StringComparer.OrdinalIgnoreCase));

        if (matchCount > 0)
            return 1.0;

        return 0.2; // No category match but still eligible
    }
}
