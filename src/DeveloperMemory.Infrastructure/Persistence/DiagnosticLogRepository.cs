using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL implementation of diagnostic log persistence.
/// All operations are failure-resilient — logging failures must never break the application.
/// </summary>
public class DiagnosticLogRepository : IDiagnosticLogRepository
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<DiagnosticLogRepository> _logger;

    public DiagnosticLogRepository(DeveloperMemoryDbContext context, ILogger<DiagnosticLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task TryLogAsync(DiagnosticLogEntry entry, CancellationToken ct = default)
    {
        try
        {
            _context.DiagnosticLogs.Add(entry);
            await _context.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw; // Always propagate cancellation
        }
        catch (Exception ex)
        {
            // Diagnostic logging failure must NEVER break the application
            _logger.LogDebug(ex, "Failed to persist diagnostic log entry (non-fatal)");
        }
    }

    public async Task TryLogBatchAsync(IReadOnlyList<DiagnosticLogEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;

        try
        {
            _context.DiagnosticLogs.AddRange(entries);
            await _context.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to persist {Count} diagnostic log entries (non-fatal)", entries.Count);
        }
    }
}
