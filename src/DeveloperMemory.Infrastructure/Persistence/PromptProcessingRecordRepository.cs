using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// Repository for prompt processing records.
/// Provides query capabilities for history endpoints.
/// </summary>
public class PromptProcessingRecordRepository
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<PromptProcessingRecordRepository> _logger;

    public PromptProcessingRecordRepository(
        DeveloperMemoryDbContext context,
        ILogger<PromptProcessingRecordRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PromptProcessingRecord> CreateAsync(
        PromptProcessingRecord record,
        CancellationToken ct = default)
    {
        record.Id = record.Id == Guid.Empty ? Guid.NewGuid() : record.Id;
        record.CreatedAt = DateTime.UtcNow;

        _context.PromptProcessingRecords.Add(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Processing record created: {Id} (CorrelationId: {CorrelationId})",
            record.Id, record.CorrelationId);

        return record;
    }

    public async Task<PromptProcessingRecord?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        return await _context.PromptProcessingRecords
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<PromptProcessingRecord>> GetRecentAsync(
        int count = 50,
        CancellationToken ct = default)
    {
        return await _context.PromptProcessingRecords
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PromptProcessingRecord>> QueryAsync(
        Guid? profileId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? optimizationMode = null,
        string? validationStatus = null,
        bool? fallbackUsed = null,
        int maxResults = 100,
        CancellationToken ct = default)
    {
        IQueryable<PromptProcessingRecord> query = _context.PromptProcessingRecords;

        if (profileId.HasValue)
            query = query.Where(r => r.ProfileId == profileId.Value);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        if (!string.IsNullOrEmpty(optimizationMode))
            query = query.Where(r => r.OptimizationMode == optimizationMode);

        if (!string.IsNullOrEmpty(validationStatus))
            query = query.Where(r => r.ValidationStatus == validationStatus);

        if (fallbackUsed.HasValue)
            query = query.Where(r => r.WasFallbackUsed == fallbackUsed.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(maxResults)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        return await _context.PromptProcessingRecords.CountAsync(ct);
    }
}
