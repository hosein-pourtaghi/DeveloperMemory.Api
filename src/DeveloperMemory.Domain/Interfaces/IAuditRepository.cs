using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Append-only repository for persistent security audit logs.
/// Never modifies or deletes entries — only appends and queries.
/// </summary>
public interface IAuditRepository
{
    /// <summary>Append a new audit entry.</summary>
    Task AppendAsync(SecurityAuditLogEntry entry, CancellationToken ct = default);

    /// <summary>Get recent audit entries, ordered by timestamp descending.</summary>
    Task<List<SecurityAuditLogEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default);

    /// <summary>Get audit entries by event type.</summary>
    Task<List<SecurityAuditLogEntry>> GetByEventTypeAsync(string eventType, int count = 50, CancellationToken ct = default);

    /// <summary>Get audit entries for a specific owner.</summary>
    Task<List<SecurityAuditLogEntry>> GetByOwnerIdAsync(string ownerId, int count = 50, CancellationToken ct = default);
}
