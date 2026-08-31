using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IMemoryRepository _memoryRepository;
    private readonly ICurrentUser _currentUser;

    public ProjectService(IProjectRepository projectRepository, IMemoryRepository memoryRepository, ICurrentUser currentUser)
    {
        _projectRepository = projectRepository;
        _memoryRepository = memoryRepository;
        _currentUser = currentUser;
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(id, ct);
        if (project == null) return null;
        return await MapToDtoAsync(project, ct);
    }

    public async Task<List<ProjectDto>> GetAllAsync(CancellationToken ct = default)
    {
        var projects = await _projectRepository.GetAllAsync(ct);
        var dtos = new List<ProjectDto>();
        foreach (var project in projects)
        {
            dtos.Add(await MapToDtoAsync(project, ct));
        }
        return dtos;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _projectRepository.CreateAsync(project, ct);
        return await MapToDtoAsync(created, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(id, ct)
            ?? throw new ProjectNotFoundException(id);

        if (request.Name != null) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;
        project.UpdatedAt = DateTime.UtcNow;

        var updated = await _projectRepository.UpdateAsync(project, ct);
        return await MapToDtoAsync(updated, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(id, ct);
        if (project == null) return false;

        return await _projectRepository.DeleteAsync(id, ct);
    }

    public async Task<ProjectDto?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByNameAsync(name, ct);
        if (project == null) return null;
        return await MapToDtoAsync(project, ct);
    }

    public async Task<List<ProjectDto>> SearchByNameAsync(string searchTerm, CancellationToken ct = default)
    {
        var projects = await _projectRepository.SearchByNameAsync(searchTerm, ct);
        var dtos = new List<ProjectDto>();
        foreach (var project in projects)
        {
            dtos.Add(await MapToDtoAsync(project, ct));
        }
        return dtos;
    }

    private async Task<ProjectDto> MapToDtoAsync(Project project, CancellationToken ct = default)
    {
        var memoryCount = await _memoryRepository.CountAsync(
            _currentUser.UserId, projectId: project.Id, ct: ct);

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            MemoryCount = memoryCount
        };
    }
}
