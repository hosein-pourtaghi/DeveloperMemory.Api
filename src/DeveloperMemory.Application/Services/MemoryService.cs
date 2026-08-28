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

    public async Task<MemoryDto?> GetByIdAsync(Guid id, string ownerId, CancellationToken ct = default)
    {
        var entry = await _memoryRepository.GetByIdAsync(id, ownerId, ct);
        if (entry == null) return null;

        entry.MarkAccessed();
        await _memoryRepository.UpdateAsync(entry, ct);

        return await MapToDtoAsync(entry, ct);
    }

    public async Task<List<MemoryDto>> GetByScopeAsync(MemoryScope scope, string ownerId, Guid? projectId = null, CancellationToken ct = default)
    {
        var entries = await _memoryRepository.GetByScopeAsync(scope, ownerId, projectId, ct);
        var dtos = new List<MemoryDto>();
        foreach (var entry in entries)
        {
            dtos.Add(await MapToDtoAsync(entry, ct));
        }
        return dtos;
    }

    public async Task<List<MemoryDto>> SearchAsync(
        string query,
        string ownerId,
        MemoryScope? scope = null,
        Guid? projectId = null,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        var entries = await _memoryRepository.SearchAsync(query, ownerId, scope, projectId, ct);

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

    public async Task<MemoryDto> CreateAsync(CreateMemoryRequest request, string ownerId, CancellationToken ct = default)
    {
        if (request.Scope == MemoryScope.Project)
        {
            if (request.ProjectId == null)
                throw new DomainException("Project scope requires a ProjectId.", "project_required");

            var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value, ct);
            if (project == null)
                throw new ProjectNotFoundException(request.ProjectId.Value);
        }

        if (request.Importance < 0.0 || request.Importance > 1.0)
            throw new DomainException("Importance must be between 0.0 and 1.0.", "invalid_importance");
        if (request.Confidence < 0.0 || request.Confidence > 1.0)
            throw new DomainException("Confidence must be between 0.0 and 1.0.", "invalid_confidence");

        var entry = new MemoryEntry
        {
            Title = request.Title,
            Content = request.Content,
            Scope = request.Scope,
            State = MemoryState.Active,
            MemoryType = request.MemoryType,
            Classification = request.Classification,
            ProjectId = request.Scope == MemoryScope.Project ? request.ProjectId : null,
            WorkspaceId = request.Scope == MemoryScope.Workspace ? request.WorkspaceId : null,
            UserId = request.Scope == MemoryScope.Private ? request.UserId : null,
            OwnerId = ownerId,
            Source = request.Source,
            ExpiresAt = request.ExpiresAt,
            Importance = request.Importance,
            Confidence = request.Confidence,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            NormalizedContent = ComputeNormalizedContent(request.Title, request.Content)
        };

        if (request.Tags != null)
        {
            entry.SetTags(request.Tags);
        }

        var created = await _memoryRepository.CreateAsync(entry, ct);
        return await MapToDtoAsync(created, ct);
    }

    public async Task<MemoryDto> UpdateAsync(Guid id, UpdateMemoryRequest request, string ownerId, CancellationToken ct = default)
    {
        var entry = await _memoryRepository.GetByIdAsync(id, ownerId, ct)
            ?? throw new MemoryNotFoundException(id);

        if (request.Title != null) entry.Title = request.Title;
        if (request.Content != null)
        {
            entry.Content = request.Content;
            entry.NormalizedContent = ComputeNormalizedContent(entry.Title, request.Content);
        }
        if (request.State.HasValue)
        {
            if (!MemoryEntry.IsValidTransition(entry.State, request.State.Value))
            {
                throw new DomainException(
                    $"Invalid state transition from {entry.State} to {request.State.Value}.",
                    "invalid_state_transition");
            }
            entry.State = request.State.Value;
        }
        if (request.Classification.HasValue) entry.Classification = request.Classification.Value;
        if (request.Tags != null) entry.SetTags(request.Tags);
        if (request.ExpiresAt.HasValue) entry.ExpiresAt = request.ExpiresAt.Value;
        if (request.Importance.HasValue) entry.Importance = request.Importance.Value;

        entry.UpdatedAt = DateTime.UtcNow;
        entry.Version++;

        var updated = await _memoryRepository.UpdateAsync(entry, ct);
        return await MapToDtoAsync(updated, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string ownerId, CancellationToken ct = default)
    {
        var entry = await _memoryRepository.GetByIdAsync(id, ownerId, ct);
        if (entry == null) return false;

        entry.SoftDelete();
        await _memoryRepository.UpdateAsync(entry, ct);
        return true;
    }

    public async Task<MemoryDto> SupersedeAsync(Guid id, CreateMemoryRequest replacementRequest, string ownerId, CancellationToken ct = default)
    {
        var existing = await _memoryRepository.GetByIdAsync(id, ownerId, ct)
            ?? throw new MemoryNotFoundException(id);

        if (existing.State == MemoryState.Deleted)
            throw new DomainException("Cannot supersede a deleted memory.", "supersede_deleted");

        if (existing.State == MemoryState.Superseded)
            throw new DomainException("Memory is already superseded.", "already_superseded");

        var replacementEntry = new MemoryEntry
        {
            Title = replacementRequest.Title,
            Content = replacementRequest.Content,
            Scope = replacementRequest.Scope,
            State = MemoryState.Active,
            MemoryType = replacementRequest.MemoryType,
            Classification = replacementRequest.Classification,
            ProjectId = replacementRequest.Scope == MemoryScope.Project ? replacementRequest.ProjectId : null,
            WorkspaceId = replacementRequest.Scope == MemoryScope.Workspace ? replacementRequest.WorkspaceId : null,
            UserId = replacementRequest.Scope == MemoryScope.Private ? replacementRequest.UserId : null,
            OwnerId = ownerId,
            Source = replacementRequest.Source,
            ExpiresAt = replacementRequest.ExpiresAt,
            Importance = replacementRequest.Importance,
            Confidence = replacementRequest.Confidence,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            NormalizedContent = ComputeNormalizedContent(replacementRequest.Title, replacementRequest.Content),
            SupersedesId = existing.Id
        };

        if (replacementRequest.Tags != null)
        {
            replacementEntry.SetTags(replacementRequest.Tags);
        }

        var createdReplacement = await _memoryRepository.CreateAsync(replacementEntry, ct);

        existing.Supersede(createdReplacement.Id);
        await _memoryRepository.UpdateAsync(existing, ct);

        return await MapToDtoAsync(createdReplacement, ct);
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

    public async Task<MemoryStatsDto> GetStatsAsync(string ownerId, CancellationToken ct = default)
    {
        var allEntries = new List<MemoryEntry>();

        foreach (MemoryScope scope in Enum.GetValues<MemoryScope>())
        {
            var entries = await _memoryRepository.GetByScopeAsync(scope, ownerId, ct: ct);
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
            MemoryType = entry.MemoryType,
            Classification = entry.Classification,
            ProjectId = entry.ProjectId,
            ProjectName = projectName,
            Source = entry.Source,
            Tags = entry.Tags,
            SupersededById = entry.SupersededById,
            SupersedesId = entry.SupersedesId,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            LastAccessedAt = entry.LastAccessedAt,
            AccessCount = entry.AccessCount,
            ExpiresAt = entry.ExpiresAt,
            Importance = entry.Importance,
            Confidence = entry.Confidence,
            Version = entry.Version
        };
    }

    private static string ComputeNormalizedContent(string title, string content)
    {
        var text = $"{title} {content}".ToLowerInvariant();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
