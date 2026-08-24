using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// Creates a fresh in-memory DeveloperMemoryDbContext per test method.
/// </summary>
public class InMemoryDbFixture : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private DeveloperMemoryDbContext? _context;

    public DeveloperMemoryDbContext Context => _context ??= CreateContext();

    private DeveloperMemoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        var ctx = new DeveloperMemoryDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    /// <summary>
    /// Clears all data from the in-memory database for test isolation.
    /// </summary>
    public void ClearDatabase()
    {
        if (_context == null) return;

        _context.MemoryEntries.RemoveRange(_context.MemoryEntries);
        _context.Projects.RemoveRange(_context.Projects);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

public class MemoryRepositoryTests : IClassFixture<InMemoryDbFixture>, IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly MemoryRepository _sut;

    public MemoryRepositoryTests(InMemoryDbFixture fixture)
    {
        _fixture = fixture;
        _sut = new MemoryRepository(fixture.Context);
        fixture.ClearDatabase(); // fresh state for each test
    }

    public void Dispose() { }

    [Fact]
    public async Task CreateAsync_PersistsEntry()
    {
        var entry = new MemoryEntry
        {
            Title = "Test Memory",
            Content = "Test content for persistence",
            Scope = MemoryScope.Global,
            State = MemoryState.Active
        };

        var created = await _sut.CreateAsync(entry);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Test Memory", created.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntry_AfterCreate()
    {
        var entry = new MemoryEntry
        {
            Title = "Findable",
            Content = "Content",
            Scope = MemoryScope.Global,
            State = MemoryState.Active
        };

        var created = await _sut.CreateAsync(entry);
        var found = await _sut.GetByIdAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByScopeAsync_FiltersByScope()
    {
        await _sut.CreateAsync(new MemoryEntry { Title = "Global 1", Content = "G", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "Global 2", Content = "G", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "Private 1", Content = "P", Scope = MemoryScope.Private, State = MemoryState.Active });

        var globalEntries = await _sut.GetByScopeAsync(MemoryScope.Global);
        var privateEntries = await _sut.GetByScopeAsync(MemoryScope.Private);

        Assert.Equal(2, globalEntries.Count);
        Assert.Single(privateEntries);
    }

    [Fact]
    public async Task GetByScopeAsync_ExcludesDeletedEntries()
    {
        var active = new MemoryEntry { Title = "Active", Content = "A", Scope = MemoryScope.Global, State = MemoryState.Active };
        var deleted = new MemoryEntry { Title = "Deleted", Content = "D", Scope = MemoryScope.Global, State = MemoryState.Deleted };

        await _sut.CreateAsync(active);
        await _sut.CreateAsync(deleted);

        var results = await _sut.GetByScopeAsync(MemoryScope.Global);

        Assert.Single(results);
        Assert.Equal("Active", results[0].Title);
    }

    [Fact]
    public async Task GetByScopeAsync_FiltersByProjectId()
    {
        var projectId = Guid.NewGuid();
        await _sut.CreateAsync(new MemoryEntry { Title = "Project Memory", Content = "P", Scope = MemoryScope.Project, ProjectId = projectId, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "Other Project", Content = "O", Scope = MemoryScope.Project, ProjectId = Guid.NewGuid(), State = MemoryState.Active });

        var results = await _sut.GetByScopeAsync(MemoryScope.Project, projectId);

        Assert.Single(results);
        Assert.Equal("Project Memory", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByTitle()
    {
        await _sut.CreateAsync(new MemoryEntry { Title = "PostgreSQL Setup", Content = "How to configure", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "Docker Config", Content = "Containers", Scope = MemoryScope.Global, State = MemoryState.Active });

        var results = await _sut.SearchAsync("PostgreSQL");

        Assert.Single(results);
        Assert.Equal("PostgreSQL Setup", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByContent()
    {
        await _sut.CreateAsync(new MemoryEntry { Title = "Doc A", Content = "Redis caching strategy", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "Doc B", Content = "PostgreSQL replication", Scope = MemoryScope.Global, State = MemoryState.Active });

        var results = await _sut.SearchAsync("Redis");

        Assert.Single(results);
        Assert.Equal("Doc A", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByTagsJson()
    {
        var entry = new MemoryEntry { Title = "Tagged", Content = "Content", Scope = MemoryScope.Global, State = MemoryState.Active };
        entry.SetTags(["dotnet", "postgresql"]);

        await _sut.CreateAsync(entry);

        var results = await _sut.SearchAsync("postgresql");

        Assert.Single(results);
        Assert.Equal("Tagged", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ExcludesDeletedEntries()
    {
        await _sut.CreateAsync(new MemoryEntry { Title = "Visible", Content = "Content", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "Visible but deleted", Content = "Deleted content", Scope = MemoryScope.Global, State = MemoryState.Deleted });

        var results = await _sut.SearchAsync("Visible");

        Assert.Single(results);
        Assert.Equal("Visible", results[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var entry = new MemoryEntry { Title = "Original", Content = "Content", Scope = MemoryScope.Global, State = MemoryState.Active };
        var created = await _sut.CreateAsync(entry);

        created.Title = "Updated";
        created.State = MemoryState.Superseded;
        await _sut.UpdateAsync(created);

        var found = await _sut.GetByIdAsync(created.Id);
        Assert.NotNull(found);
        Assert.Equal("Updated", found!.Title);
        Assert.Equal(MemoryState.Superseded, found.State);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var entry = new MemoryEntry { Title = "ToDelete", Content = "Content", Scope = MemoryScope.Global, State = MemoryState.Active };
        var created = await _sut.CreateAsync(entry);

        var deleted = await _sut.DeleteAsync(created.Id);

        Assert.True(deleted);
        var found = await _sut.GetByIdAsync(created.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var deleted = await _sut.DeleteAsync(Guid.NewGuid());
        Assert.False(deleted);
    }

    [Fact]
    public async Task GetExpiredAsync_ReturnsExpiredActiveEntries()
    {
        var expired = new MemoryEntry
        {
            Title = "Expired",
            Content = "Content",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        var notExpired = new MemoryEntry
        {
            Title = "NotExpired",
            Content = "Content",
            Scope = MemoryScope.Global,
            State = MemoryState.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var alreadyExpired = new MemoryEntry
        {
            Title = "AlreadyExpired",
            Content = "Content",
            Scope = MemoryScope.Global,
            State = MemoryState.Expired,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        await _sut.CreateAsync(expired);
        await _sut.CreateAsync(notExpired);
        await _sut.CreateAsync(alreadyExpired);

        var results = await _sut.GetExpiredAsync();

        Assert.Single(results);
        Assert.Equal("Expired", results[0].Title);
    }

    [Fact]
    public async Task CountAsync_CountsByScope()
    {
        await _sut.CreateAsync(new MemoryEntry { Title = "G1", Content = "G", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "G2", Content = "G", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "P1", Content = "P", Scope = MemoryScope.Private, State = MemoryState.Active });

        var globalCount = await _sut.CountAsync(scope: MemoryScope.Global);
        var privateCount = await _sut.CountAsync(scope: MemoryScope.Private);
        var totalCount = await _sut.CountAsync();

        Assert.Equal(2, globalCount);
        Assert.Equal(1, privateCount);
        Assert.Equal(3, totalCount);
    }

    [Fact]
    public async Task CountAsync_ExcludesDeleted()
    {
        await _sut.CreateAsync(new MemoryEntry { Title = "A", Content = "A", Scope = MemoryScope.Global, State = MemoryState.Active });
        await _sut.CreateAsync(new MemoryEntry { Title = "D", Content = "D", Scope = MemoryScope.Global, State = MemoryState.Deleted });

        var count = await _sut.CountAsync(scope: MemoryScope.Global);

        Assert.Equal(1, count);
    }
}
