using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

public class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _memoryRepository;
    private readonly IProjectRepository _projectRepository;

    public MemoryService(IMemoryRepository memoryRepository, IProjectRepository projectRepository)
    {
        _memoryRepository = memoryRepository;
        _projectRepository = projectRepository;
    }

    public async Task<MemoryDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _memoryRepository.GetByIdAsync(id, ct);
        if (entry == null) return null;
        return await MapToDtoAsync(entry, ct);
    }

    public async Task<List<MemoryDto>> GetByScopeAsync(MemoryScope scope, Guid? projectId = null, CancellationToken ct = default)
    {
        var entries = await _memoryRepository.GetByScopeAsync(scope, projectId, ct);
        var dtos = new List<MemoryDto>();
        foreach (var entry in entries)
        {
            dtos.Add(await MapToDtoAsync(entry, ct));
        }
        return dtos;
    }

    public async Task<List<MemoryDto>> SearchAsync(
        string query,
        MemoryScope? scope = null,
        Guid? projectId = null,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        var entries = await _memoryRepository.SearchAsync(query, scope, projectId, ct);

        // Filter by tags if specified
        if (tags != null && tags.Count > 0)
        {
            entries = entries.Where(e =>
                tags.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }

        var dtos = new List<MemoryDto>();
        foreach (var entry in entries)
        {
            dtos.Add(await MapToDtoAsync(entry, ct));
        }
        return dtos;
    }

    public async Task<MemoryDto> CreateAsync(CreateMemoryRequest request, CancellationToken ct = default)
    {
        // Validate project exists if scope is Project
        if (request.Scope == MemoryScope.Project)
        {
            if (request.ProjectId == null)
                throw new DomainException("Project scope requires a ProjectId.", "project_required");

            var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value, ct);
            if (project == null)
                throw new ProjectNotFoundException(request.ProjectId.Value);
        }

        var entry = new MemoryEntry
        {
            Title = request.Title,
            Content = request.Content,
            Scope = request.Scope,
            State = MemoryState.Active,
            Classification = request.Classification,
            ProjectId = request.Scope == MemoryScope.Project ? request.ProjectId : null,
            Source = request.Source,
            ExpiresAt = request.ExpiresAt,
            Importance = request.Importance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.Tags != null)
        {
            entry.SetTags(request.Tags);
        }

        var created = await _memoryRepository.CreateAsync(entry, ct);
        return await MapToDtoAsync(created, ct);
    }

    public async Task<MemoryDto> UpdateAsync(Guid id, UpdateMemoryRequest request, CancellationToken ct = default)
    {
        var entry = await _memoryRepository.GetByIdAsync(id, ct)
            ?? throw new MemoryNotFoundException(id);

        if (request.Title != null) entry.Title = request.Title;
        if (request.Content != null) entry.Content = request.Content;
        if (request.State.HasValue) entry.State = request.State.Value;
        if (request.Classification.HasValue) entry.Classification = request.Classification.Value;
        if (request.Tags != null) entry.SetTags(request.Tags);
        if (request.ExpiresAt.HasValue) entry.ExpiresAt = request.ExpiresAt.Value;
        if (request.Importance.HasValue) entry.Importance = request.Importance.Value;

        entry.UpdatedAt = DateTime.UtcNow;

        var updated = await _memoryRepository.UpdateAsync(entry, ct);
        return await MapToDtoAsync(updated, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _memoryRepository.GetByIdAsync(id, ct);
        if (entry == null) return false;

        // Soft delete — set state to Deleted
        entry.SoftDelete();
        await _memoryRepository.UpdateAsync(entry, ct);
        return true;
    }

    public async Task<MemoryDto> SupersedeAsync(Guid id, CreateMemoryRequest replacementRequest, CancellationToken ct = default)
    {
        var existing = await _memoryRepository.GetByIdAsync(id, ct)
            ?? throw new MemoryNotFoundException(id);

        // Create the replacement memory
        var replacement = await CreateAsync(replacementRequest, ct);

        // Mark the old one as superseded
        existing.Supersede(replacement.Id);
        await _memoryRepository.UpdateAsync(existing, ct);

        return replacement;
    }

    public async Task<int> ExpireAsync(CancellationToken ct = default)
    {
        var expiredEntries = await _memoryRepository.GetExpiredAsync(ct);
        var count = 0;

        foreach (var entry in expiredEntries)
        {
            if (entry.IsActive)
            {
                entry.Expire();
                await _memoryRepository.UpdateAsync(entry, ct);
                count++;
            }
        }

        return count;
    }

    public async Task<MemoryStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var allEntries = new List<MemoryEntry>();

        // Get all scopes
        foreach (MemoryScope scope in Enum.GetValues<MemoryScope>())
        {
            var entries = await _memoryRepository.GetByScopeAsync(scope, ct: ct);
            allEntries.AddRange(entries);
        }

        var stats = new MemoryStatsDto
        {
            TotalCount = allEntries.Count,
            ActiveCount = allEntries.Count(e => e.State == MemoryState.Active),
            ExpiredCount = allEntries.Count(e => e.State == MemoryState.Expired),
            SupersededCount = allEntries.Count(e => e.State == MemoryState.Superseded),
            ArchivedCount = allEntries.Count(e => e.State == MemoryState.Archived),
            GlobalCount = allEntries.Count(e => e.Scope == MemoryScope.Global),
            ProjectCount = allEntries.Count(e => e.Scope == MemoryScope.Project),
            WorkspaceCount = allEntries.Count(e => e.Scope == MemoryScope.Workspace),
            PrivateCount = allEntries.Count(e => e.Scope == MemoryScope.Private),
            ByScope = allEntries.GroupBy(e => e.Scope).ToDictionary(g => g.Key, g => g.Count()),
            ByState = allEntries.GroupBy(e => e.State).ToDictionary(g => g.Key, g => g.Count())
        };

        return stats;
    }

    private async Task<MemoryDto> MapToDtoAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        string? projectName = null;
        if (entry.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(entry.ProjectId.Value, ct);
            projectName = project?.Name;
        }

        return new MemoryDto
        {
            Id = entry.Id,
            Title = entry.Title,
            Content = entry.Content,
            Scope = entry.Scope,
            State = entry.State,
            Classification = entry.Classification,
            ProjectId = entry.ProjectId,
            ProjectName = projectName,
            Source = entry.Source,
            Tags = entry.Tags,
            SupersededById = entry.SupersededById,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            ExpiresAt = entry.ExpiresAt,
            Importance = entry.Importance
        };
    }
}
