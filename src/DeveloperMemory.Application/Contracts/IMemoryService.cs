using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

public interface IMemoryService
{
    Task<MemoryDto?> GetByIdAsync(Guid id, string ownerId, CancellationToken ct = default);
    Task<List<MemoryDto>> GetByScopeAsync(MemoryScope scope, string ownerId, Guid? projectId = null, CancellationToken ct = default);
    Task<List<MemoryDto>> SearchAsync(string query, string ownerId, MemoryScope? scope = null, Guid? projectId = null, List<string>? tags = null, CancellationToken ct = default);
    Task<MemoryDto> CreateAsync(CreateMemoryRequest request, string ownerId, CancellationToken ct = default);
    Task<MemoryDto> UpdateAsync(Guid id, UpdateMemoryRequest request, string ownerId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, string ownerId, CancellationToken ct = default);
    Task<MemoryDto> SupersedeAsync(Guid id, CreateMemoryRequest replacementRequest, string ownerId, CancellationToken ct = default);
    Task<int> ExpireAsync(CancellationToken ct = default);
    Task<MemoryStatsDto> GetStatsAsync(string ownerId, CancellationToken ct = default);
}
