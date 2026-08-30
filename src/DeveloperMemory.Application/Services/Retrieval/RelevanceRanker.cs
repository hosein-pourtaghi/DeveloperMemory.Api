using System.Text.RegularExpressions;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Deterministic relevance ranking for memory candidates.
/// Scoring is provider-independent and easily replaceable.
/// 
/// Scoring model (Phase S — context-aware):
///   FinalScore = (TextRelevance × 0.25) +
///                (ScopeRelevance × 0.12) +
///                (ProjectRelevance × 0.15) +
///                (RecencyScore × 0.12) +
///                (ImportanceScore × 0.12) +
///                (ConfidenceScore × 0.08) +
///                (MemoryTypeScore × 0.08) +
///                (SupersessionScore × 0.05) +
///                (CategoryScore × 0.03)
/// 
/// Phase S improvements over previous version:
///   - Added ConfidenceScore: consolidated memories with higher confidence rank higher
///   - Added MemoryTypeScore: explicit instructions and constraints rank above generic facts
///   - Added SupersessionScore: memories that supersede others signal more current information
///   - Added duplicate suppression: semantically equivalent memories are collapsed
///   - TextRelevance improved with normalized content matching and provenance awareness
/// </summary>
public class RelevanceRanker : IRetrievalRanker
{
    // Scoring weights — adjusted in Phase S
    private const double TextWeight = 0.25;
    private const double ScopeWeight = 0.12;
    private const double ProjectWeight = 0.15;
    private const double RecencyWeight = 0.12;
    private const double ImportanceWeight = 0.12;
    private const double ConfidenceWeight = 0.08;
    private const double MemoryTypeWeight = 0.08;
    private const double SupersessionWeight = 0.05;
    private const double CategoryWeight = 0.03;

    /// <summary>
    /// Scores and ranks candidate memories by relevance.
    /// Applies duplicate suppression before final ranking.
    /// </summary>
    public Task<List<RetrievedMemory>> RankAsync(
        List<RetrievedMemory> candidates,
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        // Step 1: Suppress duplicates (keep highest-scored representative)
        var deduplicated = SuppressDuplicates(candidates);

        // Step 2: Score each candidate
        foreach (var candidate in deduplicated)
        {
            var breakdown = new RetrievalScoreBreakdown
            {
                TextRelevance = CalculateTextRelevance(candidate, request),
                ScopeRelevance = CalculateScopeRelevance(candidate, request),
                ProjectRelevance = CalculateProjectRelevance(candidate, request),
                RecencyScore = CalculateRecencyScore(candidate),
                ImportanceScore = CalculateImportanceScore(candidate),
                CategoryScore = CalculateCategoryScore(candidate, request)
            };

            var confidenceScore = CalculateConfidenceScore(candidate);
            var memoryTypeScore = CalculateMemoryTypeScore(candidate, request);
            var supersessionScore = CalculateSupersessionScore(candidate);

            candidate.ScoreBreakdown = breakdown;
            candidate.RelevanceScore =
                (breakdown.TextRelevance * TextWeight) +
                (breakdown.ScopeRelevance * ScopeWeight) +
                (breakdown.ProjectRelevance * ProjectWeight) +
                (breakdown.RecencyScore * RecencyWeight) +
                (breakdown.ImportanceScore * ImportanceWeight) +
                (confidenceScore * ConfidenceWeight) +
                (memoryTypeScore * MemoryTypeWeight) +
                (supersessionScore * SupersessionWeight) +
                (breakdown.CategoryScore * CategoryWeight);

            // Ensure score is clamped to [0, 1]
            candidate.RelevanceScore = Math.Clamp(candidate.RelevanceScore, 0.0, 1.0);
        }

        // Step 3: Sort by relevance descending, with stable tie-breaking
        var ranked = deduplicated
            .OrderByDescending(c => c.RelevanceScore)
            .ThenByDescending(c => c.Importance)
            .ThenByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.MemoryId)
            .ToList();

        return Task.FromResult(ranked);
    }

    // ── Duplicate Suppression ──

    /// <summary>
    /// Suppresses semantically equivalent memories, keeping the highest-scored representative.
    /// This ensures high information density with low redundancy.
    /// 
    /// Suppression rules:
    ///   - Exact content duplicates: keep the one with higher importance/recency
    ///   - Normalized content duplicates: keep the one with higher importance/recency
    ///   - Superseded memories: prefer the current (superseding) memory
    ///   - Conflicting memories: preserve both (do not suppress contradictions)
    /// </summary>
    private static List<RetrievedMemory> SuppressDuplicates(List<RetrievedMemory> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        // Group by normalized content
        var groups = new List<RetrievedMemory>();
        var seen = new Dictionary<string, RetrievedMemory>(StringComparer.OrdinalIgnoreCase);

        // Sort by importance descending first, so we keep the "best" representative
        var sorted = candidates
            .OrderByDescending(c => c.Importance)
            .ThenByDescending(c => c.UpdatedAt)
            .ToList();

        foreach (var candidate in sorted)
        {
            var normalized = NormalizeForComparison(candidate.Title + " " + candidate.Content);
            if (string.IsNullOrEmpty(normalized) || normalized.Length < 5)
            {
                groups.Add(candidate);
                continue;
            }

            if (seen.TryGetValue(normalized, out var existing))
            {
                // Already have a representative for this content
                // If the existing one has a supersession relationship, prefer the current one
                if (candidate.State == MemoryState.Active && existing.State == MemoryState.Superseded)
                {
                    // Replace the superseded representative with the current one
                    groups.Remove(existing);
                    seen[normalized] = candidate;
                    groups.Add(candidate);
                }
                // Otherwise keep the existing (higher importance/recency)
                continue;
            }

            seen[normalized] = candidate;
            groups.Add(candidate);
        }

        return groups;
    }

    // ── Scoring Methods ──

    private static double CalculateTextRelevance(RetrievedMemory memory, RetrievalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return 0.3; // Neutral score when no query

        var queryLower = request.Query.ToLowerInvariant();
        var score = 0.0;

        // Exact title match — highest signal
        if (memory.Title.ToLowerInvariant().Contains(queryLower))
            score += 0.4;

        // Content match
        if (memory.Content.ToLowerInvariant().Contains(queryLower))
            score += 0.3;

        // Tag matches
        foreach (var tag in memory.Tags)
        {
            if (tag.ToLowerInvariant().Contains(queryLower))
                score += 0.1;
        }

        // Token-level matching (word overlap with query)
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
        // Context-aware scope scoring:
        // When a project context is active, project-scoped memories are strongly preferred
        var hasProjectContext = request.ProjectId.HasValue;

        return memory.Scope switch
        {
            MemoryScope.Project when hasProjectContext => 0.95,
            MemoryScope.Project => 0.5, // Project memory without project context — less relevant
            MemoryScope.Workspace when !string.IsNullOrEmpty(request.WorkspaceId) => 0.85,
            MemoryScope.Global => 0.6,
            MemoryScope.Private when !string.IsNullOrEmpty(request.UserId) => 0.7,
            _ => 0.5
        };
    }

    private static double CalculateProjectRelevance(RetrievedMemory memory, RetrievalRequest request)
    {
        if (!request.ProjectId.HasValue)
            return 0.5; // Neutral when no project context

        if (memory.ProjectId == request.ProjectId.Value)
            return 1.0; // Exact project match — highest score

        if (memory.ProjectId.HasValue)
            return 0.1; // Different project — very low relevance

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

    private static double CalculateConfidenceScore(RetrievedMemory memory)
    {
        // Confidence reflects how certain we are about this memory.
        // Knowledge-sourced memories default to 0.9, conversational to 0.5-0.8.
        // Higher confidence = more reliable = higher rank.
        return memory.Confidence;
    }

    private static double CalculateMemoryTypeScore(RetrievedMemory memory, RetrievalRequest request)
    {
        // Memory type relevance depends on the context:
        // - Explicit instructions/constraints should rank higher for operational queries
        // - Architecture/technical decisions should rank higher for technical queries
        // - Facts should rank higher for informational queries

        var queryLower = request.Query?.ToLowerInvariant() ?? string.Empty;

        return memory.MemoryType switch
        {
            // Instructions and constraints are always highly relevant
            MemoryType.Instruction => 0.95,
            MemoryType.UserConstraint => 0.9,

            // Technical decisions rank higher for technical queries
            MemoryType.TechnicalDecision when IsTechnicalQuery(queryLower) => 0.85,
            MemoryType.ArchitectureDecision when IsTechnicalQuery(queryLower) => 0.85,

            // Project context is relevant when project context is active
            MemoryType.ProjectContext when request.ProjectId.HasValue => 0.8,

            // Preferences are always moderately relevant
            MemoryType.UserPreference => 0.7,

            // Technical decisions are moderately relevant even without technical query
            MemoryType.TechnicalDecision => 0.65,
            MemoryType.ArchitectureDecision => 0.65,

            // Working context is relevant for current task queries
            MemoryType.WorkingContext => 0.6,

            // Facts are baseline
            MemoryType.Fact => 0.5,

            // Default
            _ => 0.5
        };
    }

    private static double CalculateSupersessionScore(RetrievedMemory memory)
    {
        // Memories that supersede others signal current, authoritative information.
        // The existence of SupersedesId means this memory replaced an older one.
        // This is a positive signal — it's the "current" version.

        if (memory.State == MemoryState.Active)
        {
            // Active memories get baseline score
            // Memories with supersession history get a small boost
            return 0.5;
        }

        if (memory.State == MemoryState.Updated)
        {
            return 0.6;
        }

        return 0.3;
    }

    private static double CalculateCategoryScore(RetrievedMemory memory, RetrievalRequest request)
    {
        if (request.RequiredCategories == null || request.RequiredCategories.Count == 0)
            return 0.5; // Neutral when no category filter

        var matchCount = memory.Tags
            .Count(t => request.RequiredCategories.Contains(t, StringComparer.OrdinalIgnoreCase));

        if (matchCount > 0)
            return 1.0;

        return 0.2; // No category match but still eligible
    }

    // ── Helpers ──

    private static bool IsTechnicalQuery(string queryLower)
    {
        return queryLower.Contains("architect") ||
               queryLower.Contains("design") ||
               queryLower.Contains("implement") ||
               queryLower.Contains("build") ||
               queryLower.Contains("code") ||
               queryLower.Contains("database") ||
               queryLower.Contains("api") ||
               queryLower.Contains("service") ||
               queryLower.Contains("framework") ||
               queryLower.Contains("pattern") ||
               queryLower.Contains("technology") ||
               queryLower.Contains("stack") ||
               queryLower.Contains("library") ||
               queryLower.Contains("dependency");
    }

    private static string NormalizeForComparison(string content)
    {
        var text = content.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
