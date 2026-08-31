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

    public void ClearDatabase()
    {
        if (_context == null) return;
        _context.MemoryEntries.RemoveRange(_context.MemoryEntries);
        _context.Projects.RemoveRange(_context.Projects);
        _context.SaveChanges();
    }

    public void Dispose() { _context?.Dispose(); }
}

public class MemoryRepositoryTests : IClassFixture<InMemoryDbFixture>, IDisposable
{
    private const string OwnerA = "user-a";
    private const string OwnerB = "user-b";

    private readonly InMemoryDbFixture _fixture;
    private readonly MemoryRepository _sut;

    public MemoryRepositoryTests(InMemoryDbFixture fixture)
    {
        _fixture = fixture;
        _sut = new MemoryRepository(fixture.Context);
        fixture.ClearDatabase();
    }

    public void Dispose() { }

    private static MemoryEntry CreateEntry(string title, string content = "c", MemoryScope scope = MemoryScope.Global, string? ownerId = null)
    {
        return new MemoryEntry { Title = title, Content = content, Scope = scope, State = MemoryState.Active, OwnerId = ownerId ?? OwnerA };
    }

    [Fact]
    public async Task CreateAsync_PersistsEntry()
    {
        var entry = CreateEntry("Test Memory", "Test content for persistence");
        var created = await _sut.CreateAsync(entry);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Test Memory", created.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntry_AfterCreate()
    {
        var created = await _sut.CreateAsync(CreateEntry("Findable"));
        var found = await _sut.GetByIdAsync(created.Id, OwnerA);
        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), OwnerA);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WrongOwner()
    {
        var created = await _sut.CreateAsync(CreateEntry("Secret", ownerId: OwnerA));
        var result = await _sut.GetByIdAsync(created.Id, OwnerB);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByScopeAsync_FiltersByScope()
    {
        await _sut.CreateAsync(CreateEntry("Global 1", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("Global 2", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("Private 1", "P", MemoryScope.Private, OwnerA));

        var globalEntries = await _sut.GetByScopeAsync(MemoryScope.Global, OwnerA);
        var privateEntries = await _sut.GetByScopeAsync(MemoryScope.Private, OwnerA);

        Assert.Equal(2, globalEntries.Count);
        Assert.Single(privateEntries);
    }

    [Fact]
    public async Task GetByScopeAsync_ExcludesDeletedEntries()
    {
        await _sut.CreateAsync(CreateEntry("Active", ownerId: OwnerA));
        var deleted = CreateEntry("Deleted", ownerId: OwnerA);
        deleted.State = MemoryState.Deleted;
        await _sut.CreateAsync(deleted);

        var results = await _sut.GetByScopeAsync(MemoryScope.Global, OwnerA);
        Assert.Single(results);
        Assert.Equal("Active", results[0].Title);
    }

    [Fact]
    public async Task GetByScopeAsync_FiltersByProjectId()
    {
        var projectId = Guid.NewGuid();
        await _sut.CreateAsync(new MemoryEntry { Title = "Project Memory", Content = "P", Scope = MemoryScope.Project, ProjectId = projectId, State = MemoryState.Active, OwnerId = OwnerA });
        await _sut.CreateAsync(new MemoryEntry { Title = "Other Project", Content = "O", Scope = MemoryScope.Project, ProjectId = Guid.NewGuid(), State = MemoryState.Active, OwnerId = OwnerA });

        var results = await _sut.GetByScopeAsync(MemoryScope.Project, OwnerA, projectId);
        Assert.Single(results);
        Assert.Equal("Project Memory", results[0].Title);
    }

    [Fact]
    public async Task GetByScopeAsync_ExcludesOtherOwners()
    {
        await _sut.CreateAsync(CreateEntry("OwnerA Global", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("OwnerB Global", ownerId: OwnerB));

        var results = await _sut.GetByScopeAsync(MemoryScope.Global, OwnerA);
        Assert.Single(results);
        Assert.Equal("OwnerA Global", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByTitle()
    {
        await _sut.CreateAsync(CreateEntry("PostgreSQL Setup"));
        await _sut.CreateAsync(CreateEntry("Docker Config"));
        var results = await _sut.SearchAsync("PostgreSQL", OwnerA);
        Assert.Single(results);
        Assert.Equal("PostgreSQL Setup", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FindsByContent()
    {
        await _sut.CreateAsync(CreateEntry("Doc A", "Redis caching strategy"));
        await _sut.CreateAsync(CreateEntry("Doc B", "PostgreSQL replication"));
        var results = await _sut.SearchAsync("Redis", OwnerA);
        Assert.Single(results);
        Assert.Equal("Doc A", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ExcludesDeletedEntries()
    {
        await _sut.CreateAsync(CreateEntry("Visible"));
        var deleted = CreateEntry("Visible but deleted", "Deleted content");
        deleted.State = MemoryState.Deleted;
        await _sut.CreateAsync(deleted);
        var results = await _sut.SearchAsync("Visible", OwnerA);
        Assert.Single(results);
        Assert.Equal("Visible", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_ExcludesOtherOwners()
    {
        await _sut.CreateAsync(CreateEntry("OwnerA Secret", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("OwnerB Secret", ownerId: OwnerB));
        var results = await _sut.SearchAsync("Secret", OwnerA);
        Assert.Single(results);
        Assert.Equal("OwnerA Secret", results[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var created = await _sut.CreateAsync(CreateEntry("Original"));
        created.Title = "Updated";
        created.State = MemoryState.Superseded;
        await _sut.UpdateAsync(created);
        var found = await _sut.GetByIdAsync(created.Id, OwnerA);
        Assert.NotNull(found);
        Assert.Equal("Updated", found!.Title);
        Assert.Equal(MemoryState.Superseded, found.State);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var created = await _sut.CreateAsync(CreateEntry("ToDelete"));
        var deleted = await _sut.DeleteAsync(created.Id);
        Assert.True(deleted);
        var found = await _sut.GetByIdAsync(created.Id, OwnerA);
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
        await _sut.CreateAsync(new MemoryEntry { Title = "Expired", Content = "C", Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA, ExpiresAt = DateTime.UtcNow.AddHours(-1) });
        await _sut.CreateAsync(new MemoryEntry { Title = "NotExpired", Content = "C", Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA, ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await _sut.CreateAsync(new MemoryEntry { Title = "AlreadyExpired", Content = "C", Scope = MemoryScope.Global, State = MemoryState.Expired, OwnerId = OwnerA, ExpiresAt = DateTime.UtcNow.AddHours(-1) });

        var results = await _sut.GetExpiredAsync();
        Assert.Single(results);
        Assert.Equal("Expired", results[0].Title);
    }

    [Fact]
    public async Task CountAsync_CountsByScope()
    {
        await _sut.CreateAsync(CreateEntry("G1", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("G2", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("P1", "P", MemoryScope.Private, OwnerA));

        var globalCount = await _sut.CountAsync(OwnerA, MemoryScope.Global);
        var privateCount = await _sut.CountAsync(OwnerA, MemoryScope.Private);
        var totalCount = await _sut.CountAsync(OwnerA);

        Assert.Equal(2, globalCount);
        Assert.Equal(1, privateCount);
        Assert.Equal(3, totalCount);
    }

    [Fact]
    public async Task CountAsync_ExcludesDeleted()
    {
        await _sut.CreateAsync(CreateEntry("A", ownerId: OwnerA));
        var deleted = CreateEntry("D", ownerId: OwnerA);
        deleted.State = MemoryState.Deleted;
        await _sut.CreateAsync(deleted);

        var count = await _sut.CountAsync(OwnerA, MemoryScope.Global);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountAsync_ExcludesOtherOwners()
    {
        await _sut.CreateAsync(CreateEntry("OwnerA", ownerId: OwnerA));
        await _sut.CreateAsync(CreateEntry("OwnerB", ownerId: OwnerB));

        var countA = await _sut.CountAsync(OwnerA);
        var countB = await _sut.CountAsync(OwnerB);
        Assert.Equal(1, countA);
        Assert.Equal(1, countB);
    }
}
