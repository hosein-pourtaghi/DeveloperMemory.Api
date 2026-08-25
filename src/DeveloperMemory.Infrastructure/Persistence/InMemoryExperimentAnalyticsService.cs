using DeveloperMemory.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of IExperimentAnalyticsService for testing.
/// Delegates to the in-memory experiment repository.
/// </summary>
public class InMemoryExperimentAnalyticsService : IExperimentAnalyticsService
{
    private readonly InMemoryPromptExperimentRepository _repository;

    public InMemoryExperimentAnalyticsService(InMemoryPromptExperimentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ExperimentAnalyticsResult> GetExperimentAnalyticsAsync(
        Guid experimentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var results = from.HasValue && to.HasValue
            ? await _repository.GetResultsByTimeRangeAsync(experimentId, from.Value, to.Value, ct)
            : await _repository.GetResultsAsync(experimentId, ct: ct);

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
        var variants = await _repository.GetVariantsAsync(experimentId, ct);
        var variantMap = variants.ToDictionary(v => v.Id, v => v.Name);

        var results = from.HasValue && to.HasValue
            ? await _repository.GetResultsByTimeRangeAsync(experimentId, from.Value, to.Value, ct)
            : await _repository.GetResultsAsync(experimentId, ct: ct);

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
