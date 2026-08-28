using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

public class MemoryRepository : IMemoryRepository
{
    private readonly DeveloperMemoryDbContext _context;

    public MemoryRepository(DeveloperMemoryDbContext context)
    {
        _context = context;
    }

    public async Task<MemoryEntry?> GetByIdAsync(Guid id, string ownerId, CancellationToken ct = default)
    {
        return await _context.MemoryEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == id && e.OwnerId == ownerId, ct);
    }

    public async Task<List<MemoryEntry>> GetByScopeAsync(MemoryScope scope, string ownerId, Guid? projectId = null, CancellationToken ct = default)
    {
        var query = _context.MemoryEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .Where(e => e.Scope == scope && e.OwnerId == ownerId && e.State != MemoryState.Deleted);

        if (projectId.HasValue)
        {
            query = query.Where(e => e.ProjectId == projectId.Value);
        }

        return await query
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<MemoryEntry>> SearchAsync(string query, string ownerId, MemoryScope? scope = null, Guid? projectId = null, CancellationToken ct = default)
    {
        var queryable = _context.MemoryEntries
            .AsNoTracking()
            .Include(e => e.Project)
            .Where(e => e.OwnerId == ownerId && e.State != MemoryState.Deleted);

        if (scope.HasValue)
        {
            queryable = queryable.Where(e => e.Scope == scope.Value);
        }

        if (projectId.HasValue)
        {
            queryable = queryable.Where(e => e.ProjectId == projectId.Value);
        }

        var queryLower = query.ToLowerInvariant();
        queryable = queryable.Where(e =>
            e.Title.ToLower().Contains(queryLower) ||
            e.Content.ToLower().Contains(queryLower) ||
            (e.TagsJson != null && e.TagsJson.ToLower().Contains(queryLower)));

        return await queryable
            .OrderByDescending(e => e.Importance)
            .ThenByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<MemoryEntry>> GetExpiredAsync(CancellationToken ct = default)
    {
        return await _context.MemoryEntries
            .AsNoTracking()
            .Where(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= DateTime.UtcNow && e.State == MemoryState.Active)
            .ToListAsync(ct);
    }

    public async Task<MemoryEntry> CreateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _context.MemoryEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<MemoryEntry> UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _context.MemoryEntries.Update(entry);
        await _context.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.MemoryEntries.FindAsync([id], ct);
        if (entry == null) return false;

        _context.MemoryEntries.Remove(entry);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CountAsync(string ownerId, MemoryScope? scope = null, Guid? projectId = null, CancellationToken ct = default)
    {
        var query = _context.MemoryEntries
            .AsNoTracking()
            .Where(e => e.OwnerId == ownerId && e.State != MemoryState.Deleted);

        if (scope.HasValue)
        {
            query = query.Where(e => e.Scope == scope.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(e => e.ProjectId == projectId.Value);
        }

        return await query.CountAsync(ct);
    }
}
