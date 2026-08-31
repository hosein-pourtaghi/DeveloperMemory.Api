using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// PostgreSQL persistence tests for memory entries.
/// Every test writes through one DbContext instance and reads back through
/// a NEW context instance, proving data survives context recreation
/// (the EF InMemory provider cannot demonstrate this).
/// </summary>
public class PostgresMemoryPersistenceTests : PostgresTestBase
{
    private const string OwnerA = "pg-owner-a";
    private const string OwnerB = "pg-owner-b";

    public PostgresMemoryPersistenceTests(PostgresDbFixture fixture) : base(fixture) { }

    private static MemoryEntry CreateEntry(
        string title,
        string content = "content",
        MemoryScope scope = MemoryScope.Global,
        string ownerId = OwnerA,
        Guid? projectId = null,
        MemoryState state = MemoryState.Active)
    {
        return new MemoryEntry
        {
            Title = title,
            Content = content,
            Scope = scope,
            State = state,
            OwnerId = ownerId,
            ProjectId = projectId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task CreateAndRead_SurvivesContextRecreation()
    {
        Guid createdId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var created = await repo.CreateAsync(CreateEntry("Persistent Title", "Persistent content"));
            createdId = created.Id;
        }

        // Fresh context — proves the row survived
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var found = await repo.GetByIdAsync(createdId, OwnerA);
            Assert.NotNull(found);
            Assert.Equal("Persistent Title", found!.Title);
            Assert.Equal("Persistent content", found.Content);
            Assert.Equal(OwnerA, found.OwnerId);
        }
    }

    [Fact]
    public async Task Update_SurvivesContextRecreation()
    {
        Guid createdId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var created = await repo.CreateAsync(CreateEntry("Original Title"));
            createdId = created.Id;

            created.Title = "Updated Title";
            created.State = MemoryState.Updated;
            await repo.UpdateAsync(created);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var found = await repo.GetByIdAsync(createdId, OwnerA);
            Assert.NotNull(found);
            Assert.Equal("Updated Title", found!.Title);
            Assert.Equal(MemoryState.Updated, found.State);
        }
    }

    [Fact]
    public async Task SoftDelete_IsHiddenFromReads_AfterContextRecreation()
    {
        Guid createdId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var created = await repo.CreateAsync(CreateEntry("To Soft Delete"));
            createdId = created.Id;

            created.SoftDelete();
            await repo.UpdateAsync(created);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);

            // Established contract: soft-deleted entries are excluded from search/scope/count
            // but remain retrievable by direct ID (owner-scoped, for audit/undo purposes).
            var byId = await repo.GetByIdAsync(createdId, OwnerA);
            Assert.NotNull(byId);
            Assert.Equal(MemoryState.Deleted, byId!.State);

            var search = await repo.SearchAsync("Soft Delete", OwnerA);
            Assert.Empty(search);

            var count = await repo.CountAsync(OwnerA);
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task LifecycleTransitions_SurviveContextRecreation()
    {
        Guid createdId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var created = await repo.CreateAsync(CreateEntry("Lifecycle"));
            createdId = created.Id;

            // SupersededById is a FK to MemoryEntries — must reference a real row
            var replacement = await repo.CreateAsync(CreateEntry("Lifecycle Replacement"));
            created.Supersede(replacement.Id);
            await repo.UpdateAsync(created);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var found = await repo.GetByIdAsync(createdId, OwnerA);
            Assert.NotNull(found);
            Assert.Equal(MemoryState.Superseded, found!.State);
            Assert.NotNull(found.SupersededById);
        }
    }

    [Fact]
    public async Task ProjectAssociation_SurvivesContextRecreation()
    {
        Guid projectId;
        Guid memoryId;
        await using (var ctx = Fixture.CreateContext())
        {
            var projectRepo = new ProjectRepository(ctx);
            var project = new Project
            {
                Name = $"PG Project {Guid.NewGuid():N}",
                Description = "Postgres test project",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await projectRepo.CreateAsync(project);
            projectId = project.Id;

            var repo = new MemoryRepository(ctx);
            var created = await repo.CreateAsync(CreateEntry("Project Memory", scope: MemoryScope.Project, projectId: projectId));
            memoryId = created.Id;
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var found = await repo.GetByIdAsync(memoryId, OwnerA);
            Assert.NotNull(found);
            Assert.Equal(projectId, found!.ProjectId);
            Assert.NotNull(found.Project);

            var scoped = await repo.GetByScopeAsync(MemoryScope.Project, OwnerA, projectId);
            Assert.Single(scoped);
        }
    }

    [Fact]
    public async Task OwnershipIsolation_SurvivesContextRecreation()
    {
        Guid ownerAMemoryId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(CreateEntry("A's Secret", ownerId: OwnerA));
            var bEntry = await repo.CreateAsync(CreateEntry("B's Secret", ownerId: OwnerB));
            ownerAMemoryId = bEntry.Id; // B's memory id
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);

            // A cannot read B's memory by direct ID
            var crossOwner = await repo.GetByIdAsync(ownerAMemoryId, OwnerA);
            Assert.Null(crossOwner);

            // A cannot find B's memory by search
            var search = await repo.SearchAsync("Secret", OwnerA);
            Assert.Single(search);
            Assert.Equal("A's Secret", search[0].Title);

            // B can still read their own
            var own = await repo.GetByIdAsync(ownerAMemoryId, OwnerB);
            Assert.NotNull(own);
            Assert.Equal("B's Secret", own!.Title);
        }
    }

    [Fact]
    public async Task Count_IsOwnerScoped_AfterContextRecreation()
    {
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(CreateEntry("A1", ownerId: OwnerA));
            await repo.CreateAsync(CreateEntry("A2", ownerId: OwnerA));
            await repo.CreateAsync(CreateEntry("B1", ownerId: OwnerB));
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            Assert.Equal(2, await repo.CountAsync(OwnerA));
            Assert.Equal(1, await repo.CountAsync(OwnerB));
        }
    }
}
