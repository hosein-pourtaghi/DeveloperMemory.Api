using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

public interface IMemoryService
{
    Task<MemoryDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MemoryDto>> GetByScopeAsync(MemoryScope scope, Guid? projectId = null, CancellationToken ct = default);
    Task<List<MemoryDto>> SearchAsync(string query, MemoryScope? scope = null, Guid? projectId = null, List<string>? tags = null, CancellationToken ct = default);
    Task<MemoryDto> CreateAsync(CreateMemoryRequest request, CancellationToken ct = default);
    Task<MemoryDto> UpdateAsync(Guid id, UpdateMemoryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<MemoryDto> SupersedeAsync(Guid id, CreateMemoryRequest replacementRequest, CancellationToken ct = default);
    Task<int> ExpireAsync(CancellationToken ct = default);
    Task<MemoryStatsDto> GetStatsAsync(CancellationToken ct = default);
}
