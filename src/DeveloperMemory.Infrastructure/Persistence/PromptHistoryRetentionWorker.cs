using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// Background worker that periodically cleans up expired prompt history records.
/// Respects cancellation and avoids overlapping cleanup executions.
/// </summary>
public class PromptHistoryRetentionWorker : BackgroundService
{
    private readonly IPromptHistoryRetentionService _retentionService;
    private readonly IOptionsMonitor<PromptIntelligenceOptions> _options;
    private readonly ILogger<PromptHistoryRetentionWorker> _logger;

    public PromptHistoryRetentionWorker(
        IPromptHistoryRetentionService retentionService,
        IOptionsMonitor<PromptIntelligenceOptions> options,
        ILogger<PromptHistoryRetentionWorker> logger)
    {
        _retentionService = retentionService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Prompt history retention worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = _options.CurrentValue.HistoryRetention;

                if (!config.Enabled)
                {
                    _logger.LogDebug("History retention is disabled, sleeping for 5 minutes");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                var interval = TimeSpan.FromMinutes(Math.Max(config.IntervalMinutes, 1));
                var retentionPeriod = TimeSpan.FromDays(Math.Max(config.MaxHistoryRetentionDays, 1));

                _logger.LogDebug(
                    "Running history retention cleanup (retention: {RetentionDays} days, interval: {IntervalMinutes} min)",
                    config.MaxHistoryRetentionDays, config.IntervalMinutes);

                var expiredCount = await _retentionService.GetExpiredRecordCountAsync(
                    retentionPeriod, stoppingToken);

                if (expiredCount > 0)
                {
                    var deletedCount = await CleanupInBatchesAsync(
                        retentionPeriod, config.BatchSize, stoppingToken);

                    _logger.LogInformation(
                        "History retention completed: {DeletedCount} records cleaned up",
                        deletedCount);
                }
                else
                {
                    _logger.LogDebug("No expired records to clean up");
                }

                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during history retention cleanup");

                // Wait before retrying to avoid tight error loops
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Prompt history retention worker stopped");
    }

    private async Task<int> CleanupInBatchesAsync(
        TimeSpan retentionPeriod,
        int batchSize,
        CancellationToken ct)
    {
        int totalDeleted = 0;

        while (!ct.IsCancellationRequested)
        {
            var deleted = await _retentionService.CleanupExpiredRecordsAsync(retentionPeriod, ct);

            if (deleted == 0)
                break;

            totalDeleted += deleted;

            // If fewer than batch size were deleted, we're done
            if (deleted < batchSize)
                break;

            // Small delay between batches to avoid overwhelming the database
            try
            {
                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return totalDeleted;
    }
}
