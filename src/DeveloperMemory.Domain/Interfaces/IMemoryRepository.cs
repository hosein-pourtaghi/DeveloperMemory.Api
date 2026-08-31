using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Interfaces;

public interface IMemoryRepository
{
    Task<MemoryEntry?> GetByIdAsync(Guid id, string ownerId, CancellationToken ct = default);
    Task<List<MemoryEntry>> GetByScopeAsync(MemoryScope scope, string ownerId, Guid? projectId = null, CancellationToken ct = default);
    Task<List<MemoryEntry>> SearchAsync(string query, string ownerId, MemoryScope? scope = null, Guid? projectId = null, CancellationToken ct = default);
    Task<List<MemoryEntry>> GetExpiredAsync(CancellationToken ct = default);
    Task<MemoryEntry> CreateAsync(MemoryEntry entry, CancellationToken ct = default);
    Task<MemoryEntry> UpdateAsync(MemoryEntry entry, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> CountAsync(string ownerId, MemoryScope? scope = null, Guid? projectId = null, CancellationToken ct = default);
}
