using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL/InMemory implementation of persistent security audit logging.
/// Append-only — entries are never modified or deleted.
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly DeveloperMemoryDbContext _context;

    public AuditRepository(DeveloperMemoryDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(SecurityAuditLogEntry entry, CancellationToken ct = default)
    {
        _context.SecurityAuditLog.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<SecurityAuditLogEntry>> GetRecentAsync(int count = 100, CancellationToken ct = default)
    {
        return await _context.SecurityAuditLog
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<List<SecurityAuditLogEntry>> GetByEventTypeAsync(string eventType, int count = 50, CancellationToken ct = default)
    {
        return await _context.SecurityAuditLog
            .Where(e => e.EventType == eventType)
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<List<SecurityAuditLogEntry>> GetByOwnerIdAsync(string ownerId, int count = 50, CancellationToken ct = default)
    {
        return await _context.SecurityAuditLog
            .Where(e => e.OwnerId == ownerId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToListAsync(ct);
    }
}
