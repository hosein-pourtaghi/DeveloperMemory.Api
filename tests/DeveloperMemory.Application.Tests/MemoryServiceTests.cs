using Xunit;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// In-memory implementation of IMemoryRepository for testing.
/// </summary>
public class InMemoryMemoryRepository : IMemoryRepository
{
    private readonly List<MemoryEntry> _entries = [];

    public Task<MemoryEntry?> GetByIdAsync(Guid id, string ownerId, CancellationToken ct = default)
    {
        return Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));
    }

    public Task<List<MemoryEntry>> GetByScopeAsync(MemoryScope scope, string ownerId, Guid? projectId = null, CancellationToken ct = default)
    {
        var query = _entries.Where(e => e.Scope == scope && e.State != MemoryState.Deleted);
        if (projectId.HasValue)
            query = query.Where(e => e.ProjectId == projectId.Value);
        return Task.FromResult(query.OrderByDescending(e => e.UpdatedAt).ToList());
    }

    public Task<List<MemoryEntry>> SearchAsync(string query, string ownerId, MemoryScope? scope = null, Guid? projectId = null, CancellationToken ct = default)
    {
        var queryable = _entries.Where(e => e.State != MemoryState.Deleted);
        if (scope.HasValue)
            queryable = queryable.Where(e => e.Scope == scope.Value);
        if (projectId.HasValue)
            queryable = queryable.Where(e => e.ProjectId == projectId.Value);

        var queryLower = query.ToLowerInvariant();
        queryable = queryable.Where(e =>
            e.Title.ToLower().Contains(queryLower) ||
            e.Content.ToLower().Contains(queryLower));

        return Task.FromResult(queryable.OrderByDescending(e => e.Importance).ToList());
    }

    public Task<List<MemoryEntry>> GetExpiredAsync(CancellationToken ct = default)
    {
        return Task.FromResult(
            _entries.Where(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= DateTime.UtcNow && e.State == MemoryState.Active).ToList());
    }

    public Task<MemoryEntry> CreateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _entries.Add(entry);
        return Task.FromResult(entry);
    }

    public Task<MemoryEntry> UpdateAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        return Task.FromResult(entry);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_entries.RemoveAll(e => e.Id == id) > 0);
    }

    public Task<int> CountAsync(string ownerId, MemoryScope? scope = null, Guid? projectId = null, CancellationToken ct = default)
    {
        var query = _entries.Where(e => e.State != MemoryState.Deleted);
        if (scope.HasValue)
            query = query.Where(e => e.Scope == scope.Value);
        if (projectId.HasValue)
            query = query.Where(e => e.ProjectId == projectId.Value);
        return Task.FromResult(query.Count());
    }
}

/// <summary>
/// In-memory implementation of IProjectRepository for testing.
/// </summary>
public class InMemoryProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects = [];

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));
    }

    public Task<List<Project>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_projects.OrderBy(p => p.Name).ToList());
    }

    public Task<Project> CreateAsync(Project project, CancellationToken ct = default)
    {
        _projects.Add(project);
        return Task.FromResult(project);
    }

    public Task<Project> UpdateAsync(Project project, CancellationToken ct = default)
    {
        return Task.FromResult(project);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_projects.RemoveAll(p => p.Id == id) > 0);
    }
}

public class MemoryServiceTests
{
    private readonly InMemoryMemoryRepository _memoryRepo = new();
    private readonly InMemoryProjectRepository _projectRepo = new();
    private readonly MemoryService _sut;

    public MemoryServiceTests()
    {
        _sut = new MemoryService(_memoryRepo, _projectRepo);
    }

    [Fact]
    public async Task CreateAsync_GlobalMemory_CreatesSuccessfully()
    {
        var request = new CreateMemoryRequest
        {
            Title = "Test Memory",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Tags = ["test", "dotnet"]
        };

        var result = await _sut.CreateAsync(request, "test-user");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Test Memory", result.Title);
        Assert.Equal("Test content", result.Content);
        Assert.Equal(MemoryScope.Global, result.Scope);
        Assert.Equal(MemoryState.Active, result.State);
        Assert.Contains("test", result.Tags);
        Assert.Contains("dotnet", result.Tags);
        Assert.Null(result.ProjectId);
    }

    [Fact]
    public async Task CreateAsync_ProjectScope_WithoutProjectId_ThrowsDomainException()
    {
        var request = new CreateMemoryRequest
        {
            Title = "Test",
            Content = "Content",
            Scope = MemoryScope.Project,
            ProjectId = null
        };

        await Assert.ThrowsAsync<DomainException>(() => _sut.CreateAsync(request, "test-user"));
    }

    [Fact]
    public async Task CreateAsync_ProjectScope_WithNonexistentProject_ThrowsProjectNotFoundException()
    {
        var request = new CreateMemoryRequest
        {
            Title = "Test",
            Content = "Content",
            Scope = MemoryScope.Project,
            ProjectId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => _sut.CreateAsync(request, "test-user"));
    }

    [Fact]
    public async Task CreateAsync_ProjectScope_WithValidProject_CreatesSuccessfully()
    {
        var project = await _projectRepo.CreateAsync(new Project { Name = "TestProject" });

        var request = new CreateMemoryRequest
        {
            Title = "Project Memory",
            Content = "Project content",
            Scope = MemoryScope.Project,
            ProjectId = project.Id
        };

        var result = await _sut.CreateAsync(request, "test-user");

        Assert.Equal(project.Id, result.ProjectId);
        Assert.Equal("TestProject", result.ProjectName);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), "test-user");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMemory_WhenExists()
    {
        var created = await _sut.CreateAsync(new CreateMemoryRequest
        {
            Title = "FindMe",
            Content = "Content"
        }, "test-user");

        var result = await _sut.GetByIdAsync(created.Id, "test-user");

        Assert.NotNull(result);
        Assert.Equal("FindMe", result!.Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByTitle()
    {
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "PostgreSQL Setup", Content = "How to configure PG" }, "test-user");
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "Docker Config", Content = "How to configure containers" }, "test-user");

        var results = await _sut.SearchAsync("PostgreSQL", "test-user");

        Assert.Single(results);
        Assert.Equal("PostgreSQL Setup", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByContent()
    {
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "Doc A", Content = "Redis caching strategy" }, "test-user");
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "Doc B", Content = "PostgreSQL replication" }, "test-user");

        var results = await _sut.SearchAsync("Redis", "test-user");

        Assert.Single(results);
        Assert.Equal("Doc A", results[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        await Assert.ThrowsAsync<MemoryNotFoundException>(
            () => _sut.UpdateAsync(Guid.NewGuid(), new UpdateMemoryRequest { Title = "Updated" }, "test-user"));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTitle()
    {
        var created = await _sut.CreateAsync(new CreateMemoryRequest
        {
            Title = "Original",
            Content = "Content"
        }, "test-user");

        var updated = await _sut.UpdateAsync(created.Id, new UpdateMemoryRequest { Title = "Updated" }, "test-user");

        Assert.Equal("Updated", updated.Title);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var created = await _sut.CreateAsync(new CreateMemoryRequest
        {
            Title = "ToDelete",
            Content = "Content"
        }, "test-user");

        var deleted = await _sut.DeleteAsync(created.Id, "test-user");

        Assert.True(deleted);
        var result = await _sut.GetByIdAsync(created.Id, "test-user");
        Assert.NotNull(result);
        Assert.Equal(MemoryState.Deleted, result!.State);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var deleted = await _sut.DeleteAsync(Guid.NewGuid(), "test-user");
        Assert.False(deleted);
    }

    [Fact]
    public async Task SupersedeAsync_MarksOldAsSuperseded()
    {
        var original = await _sut.CreateAsync(new CreateMemoryRequest
        {
            Title = "Old Preference",
            Content = "Use Technology A",
            Tags = ["preference"]
        }, "test-user");

        var replacement = await _sut.SupersedeAsync(original.Id, new CreateMemoryRequest
        {
            Title = "New Preference",
            Content = "Use Technology B",
            Tags = ["preference"]
        }, "test-user");

        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Equal("New Preference", replacement.Title);

        var oldMemory = await _sut.GetByIdAsync(original.Id, "test-user");
        Assert.NotNull(oldMemory);
        Assert.Equal(MemoryState.Superseded, oldMemory!.State);
        Assert.Equal(replacement.Id, oldMemory.SupersededById);
    }

    [Fact]
    public async Task ExpireAsync_ExpiresExpiredEntries()
    {
        await _sut.CreateAsync(new CreateMemoryRequest
        {
            Title = "Temporary",
            Content = "Short-lived",
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // already expired
        }, "test-user");

        await _sut.CreateAsync(new CreateMemoryRequest
        {
            Title = "Permanent",
            Content = "Long-lived",
            ExpiresAt = DateTime.UtcNow.AddHours(1) // not yet expired
        }, "test-user");

        var expiredCount = await _sut.ExpireAsync();

        Assert.Equal(1, expiredCount);
    }

    [Fact]
    public async Task GetByScopeAsync_EnforcesScopeIsolation()
    {
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "Global A", Content = "Global", Scope = MemoryScope.Global }, "test-user");
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "Global B", Content = "Global", Scope = MemoryScope.Global }, "test-user");

        var globalEntries = await _sut.GetByScopeAsync(MemoryScope.Global, "test-user");
        var privateEntries = await _sut.GetByScopeAsync(MemoryScope.Private, "test-user");

        Assert.Equal(2, globalEntries.Count);
        Assert.Empty(privateEntries);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "A", Content = "Content", Scope = MemoryScope.Global }, "test-user");
        await _sut.CreateAsync(new CreateMemoryRequest { Title = "B", Content = "Content", Scope = MemoryScope.Global }, "test-user");

        var stats = await _sut.GetStatsAsync("test-user");

        Assert.Equal(2, stats.TotalCount);
        Assert.Equal(2, stats.ActiveCount);
        Assert.Equal(2, stats.GlobalCount);
        Assert.Equal(0, stats.ProjectCount);
    }
}
