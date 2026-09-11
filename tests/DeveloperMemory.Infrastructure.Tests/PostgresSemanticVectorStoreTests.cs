using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Configuration;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// PostgreSQL + pgvector semantic retrieval tests.
/// Verifies that VectorEntries.Vector is a real pgvector `vector` column and
/// that PostgresVectorStore upsert/search operate correctly against it —
/// the previously known blocker (real[] column vs ::vector query) regression guard.
/// </summary>
public class PostgresSemanticVectorStoreTests : PostgresTestBase
{
    private const string OwnerA = "pg-vec-owner-a";
    private const string MemoryOneTitle = "Vector memory one";
    private const string MemoryTwoTitle = "Vector memory two";

    private static PostgresVectorStore CreateStore(
        DeveloperMemoryDbContext ctx,
        string? provider = null,
        string? model = null) =>
        new(
            ctx,
            Options.Create(new EmbeddingOptions
            {
                Enabled = true,
                // Search filters rows by provider/model, so these must match the
                // profile of whatever embedding provider the store is paired with.
                Provider = provider ?? "test-provider",
                Model = model ?? "test-model"
            }),
            NullLogger<PostgresVectorStore>.Instance);

    public PostgresSemanticVectorStoreTests(PostgresDbFixture fixture) : base(fixture) { }

    [Fact]
    public void VectorColumn_IsPgvectorType_NotRealArray()
    {
        using var ctx = Fixture.CreateContext();
        var connection = ctx.Database.GetDbConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
                SELECT udt_name
                FROM information_schema.columns
                WHERE table_name = 'VectorEntries' AND column_name = 'Vector'";

        var udtName = cmd.ExecuteScalar() as string;

        Assert.Equal("vector", udtName);
    }

    [Fact]
    public async Task SearchAsync_UpsertedVectors_FindsMostSimilarFirst()
    {
        // 8-dimensional orthonormal vectors keep the expected ranking deterministic.
        var v1 = new float[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        var v2 = new float[] { 0f, 1f, 0f, 0f, 0f, 0f, 0f, 0f };

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(new MemoryEntry
            {
                Title = MemoryOneTitle, Content = "vector memory one content",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await repo.CreateAsync(new MemoryEntry
            {
                Title = MemoryTwoTitle, Content = "vector memory two content",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

            var stored1 = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == MemoryOneTitle);
            var stored2 = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == MemoryTwoTitle);

            var store = CreateStore(ctx);
            Assert.True(await store.UpsertAsync(stored1.Id,
                new Embedding { Values = v1, Provider = "test-provider", Model = "test-model" }));
            Assert.True(await store.UpsertAsync(stored2.Id,
                new Embedding { Values = v2, Provider = "test-provider", Model = "test-model" }));
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var stored1 = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == MemoryOneTitle);
            var stored2 = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == MemoryTwoTitle);

            var store = CreateStore(ctx);
            var results = await store.SearchAsync(
                v1, maxResults: 2, minimumScore: 0.0);

            Assert.Equal(2, results.Count);
            // Query == v1: memory one must rank first with near-perfect similarity.
            Assert.Equal(stored1.Id, results[0].MemoryId);
            Assert.True(results[0].SimilarityScore > 0.99);
            Assert.Equal(stored2.Id, results[1].MemoryId);
            Assert.True(results[1].SimilarityScore < results[0].SimilarityScore);
        }
    }

    [Fact]
    public async Task UpsertAsync_ExistingMemory_ReplacesVectorNotDuplicate()
    {
        var v1 = new float[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        var v1b = new float[] { 0.9f, 0.1f, 0f, 0f, 0f, 0f, 0f, 0f };

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "Vector replace memory", Content = "replace content",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

            var stored = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == "Vector replace memory");

            var store = CreateStore(ctx);
            Assert.True(await store.UpsertAsync(stored.Id,
                new Embedding { Values = v1, Provider = "test-provider", Model = "test-model" }));
            Assert.True(await store.UpsertAsync(stored.Id,
                new Embedding { Values = v1b, Provider = "test-provider", Model = "test-model" }));
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var count = await ctx.VectorEntries.CountAsync();
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task SemanticRetrievalProvider_RealPgvector_ReturnsOwnerScopedResults()
    {
        // Full production semantic pipeline against real PostgreSQL + pgvector:
        // query text → deterministic embedding → vector search → owner isolation.
        const string textA = "pgvector semantic retrieval pipeline owner A unique text";
        const string textB = "completely different content for owner B memory text";

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            var memoryA = TestDataHelper.CreateMemory(
                title: "Semantic A", content: textA, ownerId: OwnerA);
            var memoryB = TestDataHelper.CreateMemory(
                title: "Semantic B", content: textB, ownerId: "pg-vec-owner-b");
            await repo.CreateAsync(memoryA);
            await repo.CreateAsync(memoryB);

            // Simulate the embedding capture path: store vectors for both memories.
            var embeddingProvider = new InMemoryEmbeddingProvider();
            var store = CreateStore(
                ctx,
                embeddingProvider.Profile.Provider,
                embeddingProvider.Profile.Model);
            foreach (var memory in new[] { memoryA, memoryB })
            {
                var result = await embeddingProvider.GenerateAsync(memory.Content);
                Assert.True(result.Success);
                Assert.True(await store.UpsertAsync(memory.Id, result.Embedding!));
            }
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var embeddingProvider = new InMemoryEmbeddingProvider();
            var store = CreateStore(
                ctx,
                embeddingProvider.Profile.Provider,
                embeddingProvider.Profile.Model);
            var provider = new SemanticRetrievalProvider(
                embeddingProvider,
                store,
                ctx,
                NullLogger<SemanticRetrievalProvider>.Instance);

            // Owner A queries with the exact text of memory A — the deterministic
            // embedding matches the stored vector, so memory A must rank first.
            var requestA = TestDataHelper.CreateRetrievalRequest(
                query: textA, ownerId: OwnerA);
            var resultsA = await provider.GetCandidatesAsync(requestA);

            Assert.NotEmpty(resultsA);
            Assert.Equal(OwnerA, resultsA[0].OwnerId);
            Assert.All(resultsA, m => Assert.Equal(OwnerA, m.OwnerId));

            // Owner B must never receive owner A's memory through the semantic path.
            var requestB = TestDataHelper.CreateRetrievalRequest(
                query: textA, ownerId: "pg-vec-owner-b");
            var resultsB = await provider.GetCandidatesAsync(requestB);

            Assert.DoesNotContain(resultsB, m => m.OwnerId == OwnerA);
        }
    }

    [Fact]
    public async Task GetAsync_DeleteAsync_RoundTrip()
    {
        var v = new float[] { 0f, 0f, 1f, 0f, 0f, 0f, 0f, 0f };

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new MemoryRepository(ctx);
            await repo.CreateAsync(new MemoryEntry
            {
                Title = "Vector roundtrip memory", Content = "roundtrip content",
                Scope = MemoryScope.Global, State = MemoryState.Active, OwnerId = OwnerA,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

            var stored = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == "Vector roundtrip memory");

            var store = CreateStore(ctx);
            Assert.True(await store.UpsertAsync(stored.Id,
                new Embedding { Values = v, Provider = "test-provider", Model = "test-model" }));

            var fetched = await store.GetAsync(stored.Id);
            Assert.NotNull(fetched);
            Assert.Equal(v, fetched!.Values);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var stored = await ctx.MemoryEntries
                .SingleAsync(m => m.Title == "Vector roundtrip memory");

            var store = CreateStore(ctx);
            Assert.True(await store.DeleteAsync(stored.Id));
            Assert.False(await store.DeleteAsync(stored.Id)); // second delete: nothing to remove
            Assert.Null(await store.GetAsync(stored.Id));
        }
    }
}
