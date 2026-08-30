using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Repository for persisting diagnostic log entries to PostgreSQL.
/// Implementations must handle persistence failures gracefully —
/// logging failures must never break the application request pipeline.
/// </summary>
public interface IDiagnosticLogRepository
{
    /// <summary>
    /// Persists a diagnostic log entry. Must not throw on failure.
    /// </summary>
    Task TryLogAsync(DiagnosticLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Persists multiple diagnostic log entries in a single batch.
    /// Must not throw on failure.
    /// </summary>
    Task TryLogBatchAsync(IReadOnlyList<DiagnosticLogEntry> entries, CancellationToken ct = default);
}
