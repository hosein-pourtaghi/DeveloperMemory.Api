using DeveloperMemory.Application.DTOs;

namespace DeveloperMemory.Application.Contracts;

public interface IProjectService
{
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ProjectDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<ProjectDto>> SearchByNameAsync(string searchTerm, CancellationToken ct = default);
}
