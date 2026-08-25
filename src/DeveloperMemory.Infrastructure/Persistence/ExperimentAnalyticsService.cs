using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of experiment analytics service.
/// Computes aggregate metrics from persisted experiment results.
/// </summary>
public class ExperimentAnalyticsService : IExperimentAnalyticsService
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<ExperimentAnalyticsService> _logger;

    public ExperimentAnalyticsService(
        DeveloperMemoryDbContext context,
        ILogger<ExperimentAnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ExperimentAnalyticsResult> GetExperimentAnalyticsAsync(
        Guid experimentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var query = _context.PromptExperimentResults
            .Where(r => r.ExperimentId == experimentId);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        var results = await query.ToListAsync(ct);

        if (results.Count == 0)
        {
            return new ExperimentAnalyticsResult
            {
                ExperimentId = experimentId,
                From = from ?? DateTime.MinValue,
                To = to ?? DateTime.UtcNow
            };
        }

        var total = results.Count;
        var qualityResults = results.Where(r => r.QualityScore.HasValue).ToList();

        return new ExperimentAnalyticsResult
        {
            ExperimentId = experimentId,
            TotalResults = total,
            SuccessCount = results.Count(r => r.QualityGatePassed),
            FailureCount = results.Count(r => !r.QualityGatePassed),
            FallbackRate = (double)results.Count(r => r.WasFallbackUsed) / total,
            LlmUsageRate = (double)results.Count(r => r.WasLlmUsed) / total,
            QualityGatePassRate = (double)results.Count(r => r.QualityGatePassed) / total,
            AverageQualityScore = qualityResults.Count > 0
                ? qualityResults.Average(r => r.QualityScore!.Value) : 0,
            AverageInputTokens = results.Average(r => r.EstimatedInputTokens),
            AverageOutputTokens = results.Average(r => r.EstimatedOutputTokens),
            AverageProcessingLatencyMs = results.Average(r => r.ProcessingDurationMs),
            From = results.Min(r => r.CreatedAt),
            To = results.Max(r => r.CreatedAt)
        };
    }

    public async Task<IReadOnlyList<VariantAnalyticsResult>> GetVariantAnalyticsAsync(
        Guid experimentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        // Load variants for name resolution
        var variants = await _context.PromptExperimentVariants
            .Where(v => v.ExperimentId == experimentId)
            .ToListAsync(ct);

        var variantMap = variants.ToDictionary(v => v.Id, v => v.Name);

        var query = _context.PromptExperimentResults
            .Where(r => r.ExperimentId == experimentId);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        var results = await query.ToListAsync(ct);

        var grouped = results.GroupBy(r => r.VariantId);

        return grouped.Select(g =>
        {
            var list = g.ToList();
            var total = list.Count;
            var qualityResults = list.Where(r => r.QualityScore.HasValue).ToList();

            return new VariantAnalyticsResult
            {
                VariantId = g.Key,
                VariantName = variantMap.TryGetValue(g.Key, out var name) ? name : g.Key.ToString(),
                ResultCount = total,
                AverageQualityScore = qualityResults.Count > 0
                    ? qualityResults.Average(r => r.QualityScore!.Value) : 0,
                FallbackRate = total > 0 ? (double)list.Count(r => r.WasFallbackUsed) / total : 0,
                LlmUsageRate = total > 0 ? (double)list.Count(r => r.WasLlmUsed) / total : 0,
                QualityGatePassRate = total > 0 ? (double)list.Count(r => r.QualityGatePassed) / total : 0,
                AverageInputTokens = list.Average(r => r.EstimatedInputTokens),
                AverageOutputTokens = list.Average(r => r.EstimatedOutputTokens),
                AverageProcessingLatencyMs = list.Average(r => r.ProcessingDurationMs)
            };
        }).ToList();
    }
}
