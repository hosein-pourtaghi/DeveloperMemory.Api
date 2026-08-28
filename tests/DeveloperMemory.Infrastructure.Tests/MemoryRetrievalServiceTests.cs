using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;
using DeveloperMemory.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

public class MemoryRetrievalServiceTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly MemoryRetrievalService _service;
    private readonly KeywordRetrievalProvider _provider;

    public MemoryRetrievalServiceTests()
    {
        _fixture = new InMemoryDbFixture();
        _provider = new KeywordRetrievalProvider(_fixture.Context);
        var resolver = new TestRetrievalProviderResolver(_provider);
        _service = new MemoryRetrievalService(
            _provider,
            resolver,
            new RelevanceRanker(),
            new CharacterContextBudgeter(),
            new Mock<ILogger<MemoryRetrievalService>>().Object);
    }

    [Fact]
    public async Task RetrieveAsync_RespectsProjectAndOwnerIsolation()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("A", "database", MemoryScope.Project, projectId: projectA),
            TestDataHelper.CreateMemory("B", "database", MemoryScope.Project, projectId: projectB),
            TestDataHelper.CreateMemory("Global", "database"));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "database", projectId: projectA));

        result.Memories.Select(m => m.Title).Should().Contain(["A", "Global"]);
        result.Memories.Should().NotContain(m => m.Title == "B");
    }

    [Fact]
    public async Task RetrieveAsync_ExcludesInvalidLifecycleStates()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("Active", "lifecycle"),
            TestDataHelper.CreateMemory("Updated", "lifecycle", state: MemoryState.Updated),
            TestDataHelper.CreateMemory("Superseded", "lifecycle", state: MemoryState.Superseded),
            TestDataHelper.CreateMemory("Archived", "lifecycle", state: MemoryState.Archived),
            TestDataHelper.CreateMemory("Expired", "lifecycle", expiresAt: DateTime.UtcNow.AddMinutes(-1)));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(query: "lifecycle"));

        result.Memories.Select(m => m.Title).Should().BeEquivalentTo(["Active", "Updated"]);
    }

    [Fact]
    public async Task RetrieveAsync_RespectsMaximumResults()
    {
        _fixture.Context.MemoryEntries.AddRange(Enumerable.Range(1, 10)
            .Select(i => TestDataHelper.CreateMemory($"Memory {i}", "bounded")));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest("bounded", maxResults: 3));

        result.Memories.Should().HaveCount(3);
    }

    [Fact]
    public async Task RetrieveAsync_RespectsWorkspaceIsolation()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("Workspace A Secret", "workspace", MemoryScope.Workspace, workspaceId: "ws-A"),
            TestDataHelper.CreateMemory("Workspace B Secret", "workspace", MemoryScope.Workspace, workspaceId: "ws-B"),
            TestDataHelper.CreateMemory("Global Config", "workspace"));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "workspace", workspaceId: "ws-A", userId: "user-1"));

        result.Memories.Select(m => m.Title).Should().Contain("Workspace A Secret");
        result.Memories.Should().NotContain(m => m.Title == "Workspace B Secret");
    }

    [Fact]
    public async Task RetrieveAsync_RespectsUserIsolation()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("User A Secret", "private", MemoryScope.Private, userId: "user-A"),
            TestDataHelper.CreateMemory("User B Secret", "private", MemoryScope.Private, userId: "user-B"),
            TestDataHelper.CreateMemory("Global Config", "private"));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "private", userId: "user-A"));

        result.Memories.Select(m => m.Title).Should().Contain("User A Secret");
        result.Memories.Should().NotContain(m => m.Title == "User B Secret");
    }

    [Fact]
    public async Task RetrieveAsync_MetadataIsPopulated()
    {
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory("Test Memory", "metadata"));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest("metadata"));

        result.Metadata.RetrievalProvider.Should().Be("keyword");
        result.Metadata.CandidateCount.Should().BeGreaterThan(0);
        result.Metadata.RetrievalDurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task BuildPromptContextAsync_ReturnsCompleteContext()
    {
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory(
            "Important Decision", "decision", tags: ["decision"], importance: 0.9));
        await _fixture.Context.SaveChangesAsync();

        var context = await _service.BuildPromptContextAsync(
            TestDataHelper.CreateRetrievalRequest("Important Decision"));

        context.OriginalQuery.Should().Be("Important Decision");
        context.RetrievedMemories.Should().ContainSingle(m => m.Title == "Important Decision");
        context.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task RetrieveAsync_MaximumResults_IsRespected()
    {
        _fixture.Context.MemoryEntries.AddRange(Enumerable.Range(1, 10)
            .Select(i => TestDataHelper.CreateMemory($"Memory {i}", "bounded")));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "bounded", maxResults: 3));

        result.Memories.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task RetrieveAsync_RankingOrder_IsByRelevance()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("Exact Match About Database", "database configuration guide", importance: 0.9),
            TestDataHelper.CreateMemory("General Notes", "random notes", importance: 0.3));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "database configuration"));

        result.Memories[0].Title.Should().Contain("Database");
    }

    [Fact]
    public async Task RetrieveAsync_ContextBudget_IsRespected()
    {
        _fixture.Context.MemoryEntries.AddRange(Enumerable.Range(1, 20)
            .Select(i => TestDataHelper.CreateMemory($"Large Memory {i}", new string('x', 2000))));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(TestDataHelper.CreateRetrievalRequest(
            query: "", tokenBudget: 600));

        result.Metadata.EstimatedTokensUsed.Should().BeLessThanOrEqualTo(600);
    }

    [Fact]
    public async Task RetrieveAsync_MissingOwnerFailsClosed()
    {
        _fixture.Context.MemoryEntries.Add(TestDataHelper.CreateMemory("Secret", "secret", ownerId: "owner-a"));
        await _fixture.Context.SaveChangesAsync();

        var result = await _service.RetrieveAsync(new RetrievalRequest { Query = "secret" });

        result.Memories.Should().BeEmpty();
    }

    public void Dispose() => _fixture.Dispose();

    private sealed class TestRetrievalProviderResolver : IRetrievalProviderResolver
    {
        private readonly IMemoryRetrievalProvider _provider;
        public TestRetrievalProviderResolver(IMemoryRetrievalProvider provider) => _provider = provider;
        public IMemoryRetrievalProvider Resolve(RetrievalMode mode) => _provider;
    }
}
