using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// PostgreSQL retrieval isolation tests.
/// Verifies that keyword retrieval is owner-isolated, project-scoped,
/// lifecycle-filtered, and fails closed when OwnerId is missing.
/// </summary>
public class PostgresRetrievalIsolationTests : PostgresTestBase
{
    private const string OwnerA = "pg-ret-owner-a";
    private const string OwnerB = "pg-ret-owner-b";

    public PostgresRetrievalIsolationTests(PostgresDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task KeywordRetrieval_IsOwnerIsolated()
    {
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "PostgreSQL connection pooling", Content = "pool_size=20",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "PostgreSQL connection pooling", Content = "pool_size=50",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerB,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var provider = new KeywordRetrievalProvider(ctx);

            var reqA = TestDataHelper.CreateRetrievalRequest(
                query: "PostgreSQL connection pooling", ownerId: OwnerA);
            var resultsA = await provider.GetCandidatesAsync(reqA);
            Assert.Single(resultsA);
            Assert.Equal(OwnerA, resultsA[0].OwnerId);
            Assert.Equal("pool_size=20", resultsA[0].Content);

            var reqB = TestDataHelper.CreateRetrievalRequest(
                query: "PostgreSQL connection pooling", ownerId: OwnerB);
            var resultsB = await provider.GetCandidatesAsync(reqB);
            Assert.Single(resultsB);
            Assert.Equal(OwnerB, resultsB[0].OwnerId);
            Assert.Equal("pool_size=50", resultsB[0].Content);
        }
    }

    [Fact]
    public async Task KeywordRetrieval_FailsClosed_WhenOwnerIdMissing()
    {
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "Shared secret", Content = "should not leak",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var provider = new KeywordRetrievalProvider(ctx);
            var req = TestDataHelper.CreateRetrievalRequest(query: "Shared secret", ownerId: string.Empty);
            var results = await provider.GetCandidatesAsync(req);
            Assert.Empty(results); // fail closed — missing owner returns nothing
        }
    }

    [Fact]
    public async Task KeywordRetrieval_FiltersDeletedAndLifecycleStates()
    {
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var deleted = new MemoryEntry
            {
                Title = "Deleted memory", Content = "gone",
                Scope = MemoryScope.Global, State = MemoryState.Deleted, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            await repo.CreateAsync(deleted);

            var superseded = new MemoryEntry
            {
                Title = "Superseded memory", Content = "old",
                Scope = MemoryScope.Global, State = MemoryState.Superseded, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            await repo.CreateAsync(superseded);

            await repo.CreateAsync(new MemoryEntry
            {
                Title = "Active memory", Content = "current",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var provider = new KeywordRetrievalProvider(ctx);
            var req = TestDataHelper.CreateRetrievalRequest(query: "memory", ownerId: OwnerA);
            var results = await provider.GetCandidatesAsync(req);

            // Deleted excluded; superseded included (lifecycle filtering excludes only Deleted)
            Assert.DoesNotContain(results, r => r.Title == "Deleted memory");
            Assert.Contains(results, r => r.Title == "Superseded memory");
            Assert.Contains(results, r => r.Title == "Active memory");
        }
    }

    [Fact]
    public async Task KeywordRetrieval_RespectsProjectScoping()
    {
        Guid projectId;
        await using (var ctx = Fixture.CreateContext())
        {
            var projectRepo = new ProjectRepository(ctx);
            var project = new Project
            {
                Name = $"PG Scope Project {Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            await projectRepo.CreateAsync(project);
            projectId = project.Id;

            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "Project scoped memory", Content = "inside project",
                Scope = MemoryScope.Project, ProjectId = projectId, State = MemoryState.Active,
                OwnerId = OwnerA, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "Global memory", Content = "everywhere",
                Scope = MemoryScope.Global, State = MemoryState.Active,
                OwnerId = OwnerA, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var provider = new KeywordRetrievalProvider(ctx);

            // Without project context: project-scoped memories are excluded
            var noProjectReq = TestDataHelper.CreateRetrievalRequest(query: "memory", ownerId: OwnerA);
            var noProjectResults = await provider.GetCandidatesAsync(noProjectReq);
            Assert.DoesNotContain(noProjectResults, r => r.Title == "Project scoped memory");

            // With project context: project-scoped memories are included
            var projectReq = TestDataHelper.CreateRetrievalRequest(
                query: "memory", ownerId: OwnerA, projectId: projectId);
            var projectResults = await provider.GetCandidatesAsync(projectReq);
            Assert.Contains(projectResults, r => r.Title == "Project scoped memory");
        }
    }
}
