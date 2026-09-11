using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Configuration;
using DeveloperMemory.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// Tests for configuration-driven retrieval-mode selection and its interaction
/// with the existing MemoryRetrievalService pipeline.
///
/// Contract under test:
///   Retrieval:Mode=Keyword (default)  → KeywordRetrievalProvider
///   Retrieval:Mode=Semantic           → SemanticRetrievalProvider
///   Retrieval:Mode=Hybrid             → HybridRetrievalProvider
/// with backward-compatible pipeline behavior in every mode
/// (privacy/lifecycle filtering, ranking, budgeting, isolation).
/// </summary>
public class RetrievalModeSelectionTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;

    public RetrievalModeSelectionTests()
    {
        _fixture = new InMemoryDbFixture();
    }

    private MemoryRetrievalService CreateService(
        string mode,
        IVectorStore? vectorStore = null,
        IEmbeddingProvider? embeddingProvider = null)
    {
        var embedding = embeddingProvider ?? new InMemoryEmbeddingProvider();
        var store = vectorStore ?? new InMemoryVectorStore();
        var keywordProvider = new KeywordRetrievalProvider(_fixture.Context);
        var semanticProvider = new SemanticRetrievalProvider(
            embedding, store, _fixture.Context, Mock.Of<ILogger<SemanticRetrievalProvider>>());
        var hybridProvider = new HybridRetrievalProvider(
            keywordProvider, semanticProvider, Mock.Of<ILogger<HybridRetrievalProvider>>());

        var options = Options.Create(new RetrievalOptions { Mode = mode });
        IMemoryRetrievalProvider selected = options.Value.ResolvedMode switch
        {
            RetrievalMode.Semantic => semanticProvider,
            RetrievalMode.Hybrid or RetrievalMode.Auto => hybridProvider,
            _ => keywordProvider
        };

        return new MemoryRetrievalService(
            selected,
            new RelevanceRanker(),
            new CharacterContextBudgeter(),
            Mock.Of<ILogger<MemoryRetrievalService>>());
    }

    private static async Task SeedVectorsAsync(
        IVectorStore store,
        IEmbeddingProvider embeddingProvider,
        IEnumerable<MemoryEntry> memories)
    {
        foreach (var memory in memories)
        {
            var result = await embeddingProvider.GenerateAsync(memory.Content);
            result.Embedding.Should().NotBeNull();
            (await store.UpsertAsync(memory.Id, result.Embedding!)).Should().BeTrue();
        }
    }

    // ── Selection: default and explicit modes reach the right provider ──

    [Theory]
    [InlineData("Keyword", "keyword")]
    [InlineData("Lexical", "keyword")] // alias for keyword mode
    [InlineData("", "keyword")]        // empty falls back to keyword (fail-safe)
    [InlineData("SomethingElse", "keyword")] // unknown falls back to keyword (fail-safe)
    [InlineData("Semantic", "semantic")]
    [InlineData("Hybrid", "hybrid")]
    [InlineData("Auto", "hybrid")] // Auto → hybrid (documented fallback behavior)
    public async Task RetrieveAsync_ModeSelection_UsesCorrectProvider(string mode, string expectedProvider)
    {
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory(
            title: "PostgreSQL Configuration Guide",
            content: "How to configure PostgreSQL connection strings",
            scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        // Seed the memory's vector from the QUERY text so the deterministic
        // in-memory embedding makes the semantic leg an exact vector match.
        // This exercises every provider's actual candidate path in each mode.
        var embeddingProvider = new InMemoryEmbeddingProvider();
        var vectorStore = new InMemoryVectorStore();
        var memories = await _fixture.Context.MemoryEntries.ToListAsync();
        foreach (var memory in memories)
        {
            var e = await embeddingProvider.GenerateAsync("postgresql");
            (await vectorStore.UpsertAsync(memory.Id, e.Embedding!)).Should().BeTrue();
        }

        var service = CreateService(mode, vectorStore, embeddingProvider);
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "postgresql"));

        result.Should().NotBeNull();
        result.Metadata.RetrievalProvider.Should().Be(expectedProvider,
            "configuration mode '{0}' must select the '{1}' provider", mode, expectedProvider);
        result.Memories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_DefaultConfiguration_IsKeyword()
    {
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory(
            title: "Default Mode Memory", scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        // Simulates an unconfigured deployment: no Retrieval section at all.
        var options = Options.Create(new RetrievalOptions());
        var keywordProvider = new KeywordRetrievalProvider(_fixture.Context);
        var service = new MemoryRetrievalService(
            keywordProvider,
            new RelevanceRanker(),
            new CharacterContextBudgeter(),
            Mock.Of<ILogger<MemoryRetrievalService>>());

        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "Default Mode"));

        options.Value.ResolvedMode.Should().Be(RetrievalMode.Lexical,
            "default configuration must select keyword retrieval");
        result.Metadata.RetrievalProvider.Should().Be("keyword");
    }

    // ── Semantic mode actually reaches the semantic provider ──

    [Fact]
    public async Task RetrieveAsync_SemanticMode_FindsMemoryWithoutKeywordMatch()
    {
        // Content deliberately shares NO keywords with the query — only the
        // seeded embedding matches. Proves the semantic leg executes.
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory(
            title: "Storage layer",
            content: "We standardize on the relational engine for persistence",
            scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var embeddingProvider = new InMemoryEmbeddingProvider();
        var vectorStore = new InMemoryVectorStore();
        // Seed the vector from the QUERY text ("database"), not the content —
        // proving retrieval works purely through the vector store.
        var memories = await _fixture.Context.MemoryEntries.ToListAsync();
        foreach (var memory in memories)
        {
            var e = await embeddingProvider.GenerateAsync("database");
            (await vectorStore.UpsertAsync(memory.Id, e.Embedding!)).Should().BeTrue();
        }

        var service = CreateService("Semantic", vectorStore, embeddingProvider);
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "database"));

        result.Metadata.RetrievalProvider.Should().Be("semantic");
        result.Memories.Should().Contain(m => m.Title == "Storage layer",
            "semantic retrieval must surface the memory through the vector store");
    }

    [Fact]
    public async Task RetrieveAsync_SemanticMode_ReturnsEmpty_WhenVectorStoreEmpty()
    {
        var service = CreateService("Semantic", new InMemoryVectorStore());
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "anything"));

        result.Metadata.RetrievalProvider.Should().Be("semantic");
        result.Memories.Should().BeEmpty();
    }

    // ── Hybrid mode actually reaches the hybrid provider ──

    [Fact]
    public async Task RetrieveAsync_HybridMode_MergesLexicalAndSemanticCandidates()
    {
        // "PostgreSQL Configuration Guide" matches lexically ("postgresql");
        // "Storage layer" matches only semantically (seeded embedding).
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(
                title: "PostgreSQL Configuration Guide",
                content: "How to configure PostgreSQL",
                scope: MemoryScope.Global),
            TestDataHelper.CreateMemory(
                title: "Storage layer",
                content: "We standardize on the relational engine",
                scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var embeddingProvider = new InMemoryEmbeddingProvider();
        var vectorStore = new InMemoryVectorStore();
        var storageMemory = await _fixture.Context.MemoryEntries
            .SingleAsync(m => m.Title == "Storage layer");
        // Seed the vector from the query text ("postgresql") — simulating a real
        // embedding model associating the storage note with the query. The lexical
        // leg cannot find "Storage layer" (no shared keyword), proving the merge.
        var semanticVector = await embeddingProvider.GenerateAsync("postgresql");
        (await vectorStore.UpsertAsync(storageMemory.Id, semanticVector.Embedding!)).Should().BeTrue();

        var service = CreateService("Hybrid", vectorStore, embeddingProvider);
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "postgresql"));

        result.Metadata.RetrievalProvider.Should().Be("hybrid");
        result.Memories.Should().Contain(m => m.Title == "PostgreSQL Configuration Guide",
            "the lexical leg must still contribute");
        result.Memories.Should().Contain(m => m.Title == "Storage layer",
            "the semantic leg must contribute candidates the lexical leg missed");
    }

    [Fact]
    public async Task RetrieveAsync_HybridMode_LexicalFallback_WhenSemanticUnavailable()
    {
        // No vectors seeded — the semantic leg returns nothing; hybrid must still
        // return lexical results (its documented fallback behavior).
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory(
            title: "Keyword Only Match", content: "contains the word zebra", scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService("Hybrid", new InMemoryVectorStore());
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "zebra"));

        result.Metadata.RetrievalProvider.Should().Be("hybrid");
        result.Memories.Should().Contain(m => m.Title == "Keyword Only Match",
            "hybrid must fall back to lexical results when the semantic leg is empty");
    }

    // ── Pipeline integrity in every mode ──

    [Theory]
    [InlineData("Keyword")]
    [InlineData("Semantic")]
    [InlineData("Hybrid")]
    public async Task RetrieveAsync_IsolationRulesHold_InEveryMode(string mode)
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        // Shared probe content: matches the query lexically (Contains) AND
        // semantically (deterministic embedding of the same text), so the
        // provider's candidate leg actually executes in every mode.
        const string probe = "isolation probe content";

        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("Global", content: probe, scope: MemoryScope.Global),
            TestDataHelper.CreateMemory("Project A", content: probe, scope: MemoryScope.Project, projectId: projectA),
            TestDataHelper.CreateMemory("Project B", content: probe, scope: MemoryScope.Project, projectId: projectB),
            TestDataHelper.CreateMemory("Workspace 1", content: probe, scope: MemoryScope.Workspace, workspaceId: "ws-1"),
            TestDataHelper.CreateMemory("Workspace 2", content: probe, scope: MemoryScope.Workspace, workspaceId: "ws-2"),
            TestDataHelper.CreateMemory("User 1", content: probe, scope: MemoryScope.Private, userId: "user-1"),
            TestDataHelper.CreateMemory("User 2", content: probe, scope: MemoryScope.Private, userId: "user-2"));
        await _fixture.Context.SaveChangesAsync();

        var embeddingProvider = new InMemoryEmbeddingProvider();
        var vectorStore = new InMemoryVectorStore();
        await SeedVectorsAsync(vectorStore, embeddingProvider, await _fixture.Context.MemoryEntries.ToListAsync());

        var service = CreateService(mode, vectorStore, embeddingProvider);
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: probe, projectId: projectA, workspaceId: "ws-1", userId: "user-1"));

        var titles = result.Memories.Select(m => m.Title).ToList();
        titles.Should().Contain("Global");
        titles.Should().Contain("Project A");
        titles.Should().Contain("Workspace 1");
        titles.Should().Contain("User 1");
        titles.Should().NotContain("Project B", "project isolation must hold in {0} mode", mode);
        titles.Should().NotContain("Workspace 2", "workspace isolation must hold in {0} mode", mode);
        titles.Should().NotContain("User 2", "user isolation must hold in {0} mode", mode);
    }

    [Theory]
    [InlineData("Keyword")]
    [InlineData("Semantic")]
    [InlineData("Hybrid")]
    public async Task RetrieveAsync_LifecycleFiltering_Holds_InEveryMode(string mode)
    {
        // Shared probe content so the query reaches candidates in every mode.
        const string probe = "lifecycle probe content";

        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("Active", content: probe, scope: MemoryScope.Global),
            TestDataHelper.CreateMemory("Deleted", content: probe, scope: MemoryScope.Global, state: MemoryState.Deleted),
            TestDataHelper.CreateMemory("Superseded", content: probe, scope: MemoryScope.Global, state: MemoryState.Superseded));
        await _fixture.Context.SaveChangesAsync();

        var embeddingProvider = new InMemoryEmbeddingProvider();
        var vectorStore = new InMemoryVectorStore();
        await SeedVectorsAsync(vectorStore, embeddingProvider, await _fixture.Context.MemoryEntries.ToListAsync());

        var service = CreateService(mode, vectorStore, embeddingProvider);
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: probe));

        result.Memories.Should().Contain(m => m.Title == "Active");
        result.Memories.Should().NotContain(m => m.Title == "Deleted",
            "deleted memories must be filtered in {0} mode", mode);
        result.Memories.Should().NotContain(m => m.Title == "Superseded",
            "superseded memories must be filtered in {0} mode", mode);
    }

    [Fact]
    public async Task RetrieveAsync_RankingAndBudget_NotBypassed_InHybridMode()
    {
        var memories = Enumerable.Range(1, 10)
            .Select(i => TestDataHelper.CreateMemory(
                title: $"Memory {i}", content: new string('x', 2000), scope: MemoryScope.Global))
            .ToList();
        _fixture.Context.MemoryEntries.AddRange(memories);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService("Hybrid");
        var result = await service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "", tokenBudget: 600, maxResults: 3));

        result.Metadata.EstimatedTokensUsed.Should().BeLessThanOrEqualTo(600,
            "context budget must be respected in hybrid mode");
        result.Memories.Count.Should().BeLessThanOrEqualTo(3,
            "MaximumResults must be respected in hybrid mode");
    }

    [Fact]
    public async Task RetrieveAsync_FailClosed_NoOwner_InEveryMode()
    {
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory(
            title: "Secret", scope: MemoryScope.Global, ownerId: "real-owner"));
        await _fixture.Context.SaveChangesAsync();

        var embeddingProvider = new InMemoryEmbeddingProvider();
        var vectorStore = new InMemoryVectorStore();
        await SeedVectorsAsync(vectorStore, embeddingProvider, await _fixture.Context.MemoryEntries.ToListAsync());

        foreach (var mode in new[] { "Keyword", "Semantic", "Hybrid" })
        {
            var service = CreateService(mode, vectorStore, embeddingProvider);
            var request = TestDataHelper.CreateRetrievalRequest(query: "Secret");
            request.OwnerId = string.Empty; // fail-closed contract

            var result = await service.RetrieveAsync(request);

            result.Memories.Should().BeEmpty(
                "missing OwnerId must return no results in {0} mode", mode);
        }
    }

    // ── Provider abstraction remains intact ──

    [Fact]
    public void Providers_AllImplementSharedAbstraction()
    {
        // All three selection outcomes are IMemoryRetrievalProvider —
        // the gateway/service depends only on the abstraction.
        typeof(KeywordRetrievalProvider).Should().BeAssignableTo<IMemoryRetrievalProvider>();
        typeof(SemanticRetrievalProvider).Should().BeAssignableTo<IMemoryRetrievalProvider>();
        typeof(HybridRetrievalProvider).Should().BeAssignableTo<IMemoryRetrievalProvider>();
    }

    [Theory]
    [InlineData("keyword", true)]
    [InlineData("Keyword", true)]
    [InlineData("lexical", true)]
    [InlineData("semantic", false)]
    [InlineData("hybrid", false)]
    [InlineData("auto", false)]
    // Fail-safe rows: unknown/empty/null MUST resolve to keyword (Lexical) so a
    // misconfigured deployment can never silently switch retrieval behavior.
    [InlineData("nonsense", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void RetrievalOptions_ResolvedMode_ParsesCorrectly(string? input, bool isKeywordMode)
    {
        var options = new RetrievalOptions { Mode = input! };

        if (isKeywordMode)
        {
            options.ResolvedMode.Should().Be(RetrievalMode.Lexical);
        }
        else
        {
            options.ResolvedMode.Should().NotBe(RetrievalMode.Lexical);
        }
    }

    [Theory]
    [InlineData("Semantic", RetrievalMode.Semantic)]
    [InlineData("Hybrid", RetrievalMode.Hybrid)]
    [InlineData("Auto", RetrievalMode.Auto)]
    public void RetrievalOptions_ResolvedMode_MapsExplicitModes(string input, RetrievalMode expected)
    {
        new RetrievalOptions { Mode = input }.ResolvedMode.Should().Be(expected);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
