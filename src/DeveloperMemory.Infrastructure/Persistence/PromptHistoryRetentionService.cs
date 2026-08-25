using DeveloperMemory.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of prompt history retention service.
/// </summary>
public class PromptHistoryRetentionService : IPromptHistoryRetentionService
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<PromptHistoryRetentionService> _logger;

    public PromptHistoryRetentionService(
        DeveloperMemoryDbContext context,
        ILogger<PromptHistoryRetentionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CleanupExpiredRecordsAsync(
        TimeSpan retentionPeriod,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - retentionPeriod;

        var expiredRecords = await _context.PromptProcessingRecords
            .Where(r => r.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (expiredRecords.Count == 0)
        {
            return 0;
        }

        _context.PromptProcessingRecords.RemoveRange(expiredRecords);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cleaned up {Count} expired prompt processing records (older than {Cutoff})",
            expiredRecords.Count, cutoff);

        return expiredRecords.Count;
    }

    public async Task<int> GetExpiredRecordCountAsync(
        TimeSpan retentionPeriod,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - retentionPeriod;

        return await _context.PromptProcessingRecords
            .CountAsync(r => r.CreatedAt < cutoff, ct);
    }

    public async Task<int> GetTotalRecordCountAsync(
        CancellationToken ct = default)
    {
        return await _context.PromptProcessingRecords.CountAsync(ct);
    }
}
