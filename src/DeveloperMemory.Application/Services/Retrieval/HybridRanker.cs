using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Hybrid relevance ranker combining lexical and semantic signals.
///
/// Scoring model:
///   LexicalScore × LexicalWeight
///   + SemanticScore × SemanticWeight
///   + ConfidenceScore × ConfidenceWeight
///   + ScopeRelevance × ScopeWeight
///   + ProjectRelevance × ProjectWeight
///   + RecencyScore × RecencyWeight
///   + ImportanceScore × ImportanceWeight
///   + CategoryScore × CategoryWeight
///
/// All scores are normalized to [0,1] before combination.
/// The final ranking is determined by weighted signals, NOT by retrieval source.
/// A memory that is semantically much more relevant CAN outrank a lexical match.
/// Tie-breaking is deterministic via Importance → UpdatedAt → MemoryId.
/// </summary>
public class HybridRanker : IRetrievalRanker
{
    // Weights — configurable in future via options
    // All weights should sum to 1.0
    private const double LexicalWeight = 0.25;
    private const double SemanticWeight = 0.25;
    private const double ConfidenceWeight = 0.05;
    private const double ScopeWeight = 0.10;
    private const double ProjectWeight = 0.10;
    private const double RecencyWeight = 0.10;
    private const double ImportanceWeight = 0.10;
    private const double CategoryWeight = 0.05;

    /// <summary>
    /// Semantic scores for memories, keyed by memory ID.
    /// If not provided, semantic contribution is zero.
    /// </summary>
    private readonly Dictionary<Guid, double>? _semanticScores;

    public HybridRanker(Dictionary<Guid, double>? semanticScores = null)
    {
        _semanticScores = semanticScores;
    }

    public Task<List<RetrievedMemory>> RankAsync(
        List<RetrievedMemory> candidates,
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        bool hasSemanticData = _semanticScores != null && _semanticScores.Count > 0;

        foreach (var candidate in candidates)
        {
            // All scores normalized to [0,1]
            var lexicalScore = CalculateLexicalScore(candidate, request);

            var semanticScore = 0.0;
            if (hasSemanticData &&
                _semanticScores!.TryGetValue(candidate.MemoryId, out var score))
            {
                semanticScore = Math.Clamp(score, 0.0, 1.0);
            }

            // Normalize other signals to [0,1]
            var confidenceScore = Math.Clamp(candidate.Confidence, 0.0, 1.0);
            var scopeScore = NormalizeScopeRelevance(candidate);
            var projectScore = CalculateProjectRelevance(candidate, request);
            var recencyScore = CalculateRecencyScore(candidate);
            var importanceScore = Math.Clamp(candidate.Importance, 0.0, 1.0);
            var categoryScore = CalculateCategoryScore(candidate, request);

            // Weighted combination — no source-based priority
            var finalScore =
                lexicalScore * LexicalWeight +
                semanticScore * SemanticWeight +
                confidenceScore * ConfidenceWeight +
                scopeScore * ScopeWeight +
                projectScore * ProjectWeight +
                recencyScore * RecencyWeight +
                importanceScore * ImportanceWeight +
                categoryScore * CategoryWeight;

            candidate.RelevanceScore = Math.Clamp(finalScore, 0.0, 1.0);

            // Store breakdown for explainability
            candidate.ScoreBreakdown = new RetrievalScoreBreakdown
            {
                TextRelevance = lexicalScore,
                ScopeRelevance = scopeScore,
                ProjectRelevance = projectScore,
                RecencyScore = recencyScore,
                ImportanceScore = importanceScore,
                CategoryScore = categoryScore
            };
        }

        // Deterministic ordering
        var ranked = candidates
            .OrderByDescending(c => c.RelevanceScore)
            .ThenByDescending(c => c.Importance)
            .ThenByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.MemoryId)
            .ToList();

        return Task.FromResult(ranked);
    }

    private static double CalculateLexicalScore(RetrievedMemory memory, RetrievalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return 0.3;

        var queryLower = request.Query.ToLowerInvariant();
        var score = 0.0;

        if (memory.Title.ToLowerInvariant().Contains(queryLower))
            score += 0.5;

        if (memory.Content.ToLowerInvariant().Contains(queryLower))
            score += 0.3;

        foreach (var tag in memory.Tags)
        {
            if (tag.ToLowerInvariant().Contains(queryLower))
                score += 0.1;
        }

        var queryTokens = queryLower.Split([' ', ',', '.', ';', ':', '!', '?'],
            StringSplitOptions.RemoveEmptyEntries);
        var contentLower = (memory.Title + " " + memory.Content).ToLowerInvariant();
        var matchCount = queryTokens.Count(t => contentLower.Contains(t));
        if (queryTokens.Length > 0)
        {
            score += ((double)matchCount / queryTokens.Length) * 0.2;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double CalculateScopeRelevance(RetrievedMemory memory)
    {
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
        if (!request.ProjectId.HasValue) return 0.5;
        if (memory.ProjectId == request.ProjectId.Value) return 1.0;
        if (memory.ProjectId.HasValue) return 0.1;
        return 0.5;
    }

    private static double CalculateRecencyScore(RetrievedMemory memory)
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

    private static double CalculateCategoryScore(RetrievedMemory memory, RetrievalRequest request)
    {
        if (request.RequiredCategories == null || request.RequiredCategories.Count == 0)
            return 0.5;

        var matchCount = memory.Tags
            .Count(t => request.RequiredCategories.Contains(t, StringComparer.OrdinalIgnoreCase));

        return matchCount > 0 ? 1.0 : 0.2;
    }
}
