using System.Diagnostics;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Centralized memory retrieval service implementing the privacy-aware retrieval pipeline.
/// 
/// Pipeline: Request → Scope Resolution → Privacy/Isolation → Lifecycle → Candidate Retrieval
///          → Relevance Ranking → Context Budgeting → PromptContext
/// </summary>
public class MemoryRetrievalService : IMemoryRetrievalService
{
    private readonly IRetrievalProviderResolver _providerResolver;
    private readonly IRetrievalRanker _ranker;
    private readonly IContextBudgeter _budgeter;
    private readonly ILogger<MemoryRetrievalService> _logger;

    public MemoryRetrievalService(
        IMemoryRetrievalProvider retrievalProvider,
        IRetrievalProviderResolver providerResolver,
        IRetrievalRanker ranker,
        IContextBudgeter budgeter,
        ILogger<MemoryRetrievalService> logger)
    {
        _providerResolver = providerResolver;
        _ranker = ranker;
        _budgeter = budgeter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RetrievedMemoriesResult> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var metadata = new RetrievalMetadata
        {
            RetrievalProvider = "keyword"
        };

        try
        {
            // ── Stage 1: Scope Resolution ──
            if (string.IsNullOrWhiteSpace(request.OwnerId))
            {
                return new RetrievedMemoriesResult
                {
                    Memories = [],
                    Metadata = new RetrievalMetadata
                    {
                        RetrievalProvider = "keyword",
                        EligibleScopes = []
                    }
                };
            }

            request.MaximumResults = Math.Clamp(request.MaximumResults, 1, 100);
            request.ContextTokenBudget = Math.Max(0, request.ContextTokenBudget);

            var eligibleScopes = ScopeResolver.ResolveEligibleScopes(request);
            metadata.EligibleScopes = eligibleScopes;

            _logger.LogDebug(
                "Retrieval scope resolution: query={Query}, projectId={ProjectId}, scopes={Scopes}",
                TruncateLog(request.Query), request.ProjectId, string.Join(",", eligibleScopes));

            // ── Stage 2: Candidate Retrieval (with provider-level filtering) ──
            var provider = _providerResolver.Resolve(request.Mode);
            metadata.RetrievalProvider = provider.ProviderName;
            var scoredCandidates = await provider.GetScoredCandidatesAsync(request, ct);
            metadata.CandidateCount = scoredCandidates.Count;

            _logger.LogDebug("Retrieved {Count} candidates from provider {Provider}", scoredCandidates.Count, provider.ProviderName);

            // ── Stage 3: Privacy / Isolation Filtering ──
            var privacyFiltered = PrivacyFilter.FilterByPrivacy(
                scoredCandidates.Select(candidate => candidate.Memory).ToList(), request, eligibleScopes);

            // ── Stage 4: Lifecycle Filtering ──
            var lifecycleFiltered = LifecycleFilter.FilterByLifecycle(privacyFiltered);
            metadata.EligibleCount = lifecycleFiltered.Count;

            _logger.LogDebug(
                "After privacy+lifecycle filtering: {Eligible} eligible from {Candidate} candidates",
                lifecycleFiltered.Count, scoredCandidates.Count);

            // ── Stage 5: Convert to RetrievedMemory ──
            var semanticScores = scoredCandidates
                .Where(candidate => candidate.SemanticScore.HasValue)
                .ToDictionary(candidate => candidate.Memory.Id, candidate => candidate.SemanticScore!.Value);

            var retrievedMemories = lifecycleFiltered.Select(m =>
            {
                var retrieved = MapToRetrievedMemory(m.Memory, m.EligibilityReason);
                if (semanticScores.TryGetValue(m.Memory.Id, out var semanticScore))
                {
                    retrieved.SemanticRelevanceScore = semanticScore;
                }
                return retrieved;
            }).ToList();

            // ── Stage 6: Relevance Ranking ──
            var rankingStart = Stopwatch.StartNew();
            var ranked = await _ranker.RankAsync(retrievedMemories, request, ct);
            rankingStart.Stop();
            metadata.RankingDurationMs = rankingStart.Elapsed.TotalMilliseconds;

            // ── Stage 7: Apply MaximumResults ──
            if (ranked.Count > request.MaximumResults)
            {
                ranked = ranked.Take(request.MaximumResults).ToList();
            }

            // ── Stage 8: Context Budgeting ──
            var budgeted = await _budgeter.SelectWithinBudgetAsync(
                ranked, request.ContextTokenBudget, ct);
            metadata.SelectedCount = budgeted.Count;
            metadata.EstimatedTokensUsed = budgeted.Sum(m => m.EstimatedTokens);

            stopwatch.Stop();
            metadata.RetrievalDurationMs = stopwatch.Elapsed.TotalMilliseconds;

            _logger.LogInformation(
                "Retrieval completed: {Selected}/{Eligible}/{Candidate} memories, " +
                "{Tokens} tokens, {Duration}ms, provider={Provider}",
                metadata.SelectedCount, metadata.EligibleCount, metadata.CandidateCount,
                metadata.EstimatedTokensUsed, metadata.RetrievalDurationMs, metadata.RetrievalProvider);

            return new RetrievedMemoriesResult
            {
                Memories = budgeted,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metadata.RetrievalDurationMs = stopwatch.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Retrieval failed after {Duration}ms", metadata.RetrievalDurationMs);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PromptContext> BuildPromptContextAsync(
        RetrievalRequest request,
        CancellationToken ct = default)
    {
        var contextBuildingStart = Stopwatch.StartNew();

        var result = await RetrieveAsync(request, ct);

        var promptContext = new PromptContext
        {
            OriginalQuery = request.Query,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            UserId = request.UserId,
            RetrievedMemories = result.Memories,
            ContextTokenBudget = request.ContextTokenBudget,
            Metadata = result.Metadata
        };

        contextBuildingStart.Stop();
        promptContext.Metadata.ContextBuildingDurationMs = contextBuildingStart.Elapsed.TotalMilliseconds;

        return promptContext;
    }

    private static RetrievedMemory MapToRetrievedMemory(MemoryEntry entry, string eligibilityReason)
    {
        var tags = entry.Tags;
        var estimatedTokens = EstimateTokens(entry);

        return new RetrievedMemory
        {
            MemoryId = entry.Id,
            Title = entry.Title,
            Content = entry.Content,
            Scope = entry.Scope,
            Category = tags.FirstOrDefault(),
            State = entry.State,
            ProjectId = entry.ProjectId,
            WorkspaceId = entry.WorkspaceId,
            UserId = entry.UserId,
            Classification = entry.Classification,
            Importance = entry.Importance,
            Source = entry.Source,
            Tags = tags,
            UpdatedAt = entry.UpdatedAt,
            EligibilityReason = eligibilityReason,
            EstimatedTokens = estimatedTokens
        };
    }

    private static int EstimateTokens(MemoryEntry entry)
    {
        // ~4 chars per token heuristic, consistent with existing TokenEstimator
        var textLength = entry.Title.Length + entry.Content.Length;
        return (int)Math.Ceiling(textLength / 4.0);
    }

    private static string TruncateLog(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length > 50 ? text[..50] + "..." : text;
    }
}
