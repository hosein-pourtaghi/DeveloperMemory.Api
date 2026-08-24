using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Tests;

public class ProjectRepositoryTests : IClassFixture<InMemoryDbFixture>, IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly ProjectRepository _sut;

    public ProjectRepositoryTests(InMemoryDbFixture fixture)
    {
        _fixture = fixture;
        _sut = new ProjectRepository(fixture.Context);
        fixture.ClearDatabase(); // fresh state for each test
    }

    public void Dispose() { }

    [Fact]
    public async Task CreateAsync_PersistsProject()
    {
        var project = new Project { Name = "TestProject", Description = "A test project" };

        var created = await _sut.CreateAsync(project);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("TestProject", created.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProject_AfterCreate()
    {
        var project = new Project { Name = "Findable", Description = "Desc" };
        var created = await _sut.CreateAsync(project);

        var found = await _sut.GetByIdAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProjects()
    {
        await _sut.CreateAsync(new Project { Name = "Alpha" });
        await _sut.CreateAsync(new Project { Name = "Beta" });

        var all = await _sut.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("Alpha", all[0].Name);
        Assert.Equal("Beta", all[1].Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var project = new Project { Name = "Original", Description = "Desc" };
        var created = await _sut.CreateAsync(project);

        created.Name = "Updated";
        await _sut.UpdateAsync(created);

        var found = await _sut.GetByIdAsync(created.Id);
        Assert.NotNull(found);
        Assert.Equal("Updated", found!.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProject()
    {
        var project = new Project { Name = "ToDelete" };
        var created = await _sut.CreateAsync(project);

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
}
