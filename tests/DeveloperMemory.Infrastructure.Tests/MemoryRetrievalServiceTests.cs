using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// Integration tests for the full MemoryRetrievalService pipeline.
/// Tests the end-to-end flow: scope resolution → privacy → lifecycle → retrieval → ranking → budgeting.
/// </summary>
public class MemoryRetrievalServiceTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly MemoryRetrievalService _service;
    private readonly KeywordRetrievalProvider _provider;

    public MemoryRetrievalServiceTests()
    {
        _fixture = new InMemoryDbFixture();
        _provider = new KeywordRetrievalProvider(_fixture.Context);

        var ranker = new RelevanceRanker();
        var budgeter = new CharacterContextBudgeter();
        var logger = new Mock<ILogger<MemoryRetrievalService>>();

        _service = new MemoryRetrievalService(_provider, ranker, budgeter, logger.Object);
    }

    // ── Project Isolation ──

    [Fact]
    public async Task RetrieveAsync_RespectsProjectIsolation()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "Project A Config", scope: MemoryScope.Project, projectId: projectA),
            TestDataHelper.CreateMemory(title: "Project B Config", scope: MemoryScope.Project, projectId: projectB),
            TestDataHelper.CreateMemory(title: "Global Config", scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "Config", projectId: projectA);
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().NotContain(m => m.Title == "Project B Config",
            "Project B memories must not leak to Project A");
        result.Memories.Should().Contain(m => m.Title == "Project A Config");
        result.Memories.Should().Contain(m => m.Title == "Global Config");
    }

    // ── Workspace Isolation ──

    [Fact]
    public async Task RetrieveAsync_RespectsWorkspaceIsolation()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "Workspace A Secret", scope: MemoryScope.Workspace, workspaceId: "ws-A"),
            TestDataHelper.CreateMemory(title: "Workspace B Secret", scope: MemoryScope.Workspace, workspaceId: "ws-B"),
            TestDataHelper.CreateMemory(title: "Global Config", scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(
            query: "", workspaceId: "ws-A", userId: "user-1");
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().NotContain(m => m.Title == "Workspace B Secret",
            "Workspace B memories must not leak to Workspace A");
        result.Memories.Should().Contain(m => m.Title == "Workspace A Secret");
        result.Memories.Should().Contain(m => m.Title == "Global Config");
    }

    // ── User/Private Isolation ──

    [Fact]
    public async Task RetrieveAsync_RespectsUserIsolation()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "User A Secret", scope: MemoryScope.Private, userId: "user-A"),
            TestDataHelper.CreateMemory(title: "User B Secret", scope: MemoryScope.Private, userId: "user-B"),
            TestDataHelper.CreateMemory(title: "Global Config", scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(
            query: "", userId: "user-A");
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().NotContain(m => m.Title == "User B Secret",
            "User B private memories must not leak to User A");
        result.Memories.Should().Contain(m => m.Title == "User A Secret");
        result.Memories.Should().Contain(m => m.Title == "Global Config");
    }

    // ── Lifecycle ──

    [Fact]
    public async Task RetrieveAsync_ExcludesDeletedAndExpiredMemories()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "Active", scope: MemoryScope.Global, state: MemoryState.Active),
            TestDataHelper.CreateMemory(title: "Deleted", scope: MemoryScope.Global, state: MemoryState.Deleted),
            TestDataHelper.CreateMemory(title: "Expired", scope: MemoryScope.Global,
                state: MemoryState.Active, expiresAt: DateTime.UtcNow.AddDays(-1)));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "");
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().Contain(m => m.Title == "Active");
        result.Memories.Should().NotContain(m => m.Title == "Deleted");
        result.Memories.Should().NotContain(m => m.Title == "Expired");
    }

    [Fact]
    public async Task RetrieveAsync_SupersededMemories_Excluded()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "Original", scope: MemoryScope.Global, state: MemoryState.Superseded),
            TestDataHelper.CreateMemory(title: "Replacement", scope: MemoryScope.Global, state: MemoryState.Active));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "");
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().NotContain(m => m.Title == "Original");
        result.Memories.Should().Contain(m => m.Title == "Replacement");
    }

    // ── Metadata ──

    [Fact]
    public async Task RetrieveAsync_MetadataIsPopulated()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "Test Memory", scope: MemoryScope.Global));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "Test");
        var result = await _service.RetrieveAsync(request);

        result.Metadata.Should().NotBeNull();
        result.Metadata.RetrievalProvider.Should().Be("keyword");
        result.Metadata.CandidateCount.Should().BeGreaterThan(0);
        result.Metadata.RetrievalDurationMs.Should().BeGreaterThan(0);
    }

    // ── PromptContext ──

    [Fact]
    public async Task BuildPromptContextAsync_ReturnsCompleteContext()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(title: "Important Decision", scope: MemoryScope.Global,
                importance: 0.9, tags: ["decision"]));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "Important Decision");
        var context = await _service.BuildPromptContextAsync(request);

        context.Should().NotBeNull();
        context.OriginalQuery.Should().Be("Important Decision");
        context.RetrievedMemories.Should().HaveCount(1);
        context.RetrievedMemories[0].Title.Should().Be("Important Decision");
        context.Metadata.Should().NotBeNull();
    }

    // ── MaximumResults ──

    [Fact]
    public async Task RetrieveAsync_MaximumResults_IsRespected()
    {
        var memories = Enumerable.Range(1, 10)
            .Select(i => TestDataHelper.CreateMemory(
                title: $"Memory {i}", content: $"Content about topic {i}",
                scope: MemoryScope.Global))
            .ToList();

        _fixture.Context.MemoryEntries.AddRange(memories);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "", maxResults: 3);
        var result = await _service.RetrieveAsync(request);

        result.Memories.Count.Should().BeLessThanOrEqualTo(3,
            "MaximumResults should limit the number of returned memories");
    }

    // ── Ranking ──

    [Fact]
    public async Task RetrieveAsync_RankingOrder_IsByRelevance()
    {
        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory(
                title: "Exact Match About Database", content: "Detailed database configuration guide",
                scope: MemoryScope.Global, importance: 0.9),
            TestDataHelper.CreateMemory(
                title: "General Notes", content: "Some random notes about various topics",
                scope: MemoryScope.Global, importance: 0.3));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "database configuration");
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().HaveCountGreaterThan(0);
        result.Memories[0].Title.Should().Contain("Database",
            "Exact match should rank first");
    }

    // ── Context Budget ──

    [Fact]
    public async Task RetrieveAsync_ContextBudget_IsRespected()
    {
        var memories = Enumerable.Range(1, 20)
            .Select(i => TestDataHelper.CreateMemory(
                title: $"Large Memory {i}", content: new string('x', 2000),
                scope: MemoryScope.Global))
            .ToList();

        _fixture.Context.MemoryEntries.AddRange(memories);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "", tokenBudget: 600);
        var result = await _service.RetrieveAsync(request);

        result.Metadata.EstimatedTokensUsed.Should().BeLessThanOrEqualTo(600,
            "Context budget must be respected");
    }

    // ── Full Isolation Scenario ──

    [Fact]
    public async Task RetrieveAsync_FullIsolation_OnlyCorrectMemoriesReturned()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        _fixture.Context.MemoryEntries.AddRange(
            TestDataHelper.CreateMemory("Global", scope: MemoryScope.Global),
            TestDataHelper.CreateMemory("Project A", scope: MemoryScope.Project, projectId: projectA),
            TestDataHelper.CreateMemory("Project B", scope: MemoryScope.Project, projectId: projectB),
            TestDataHelper.CreateMemory("Workspace 1", scope: MemoryScope.Workspace, workspaceId: "ws-1"),
            TestDataHelper.CreateMemory("Workspace 2", scope: MemoryScope.Workspace, workspaceId: "ws-2"),
            TestDataHelper.CreateMemory("User 1", scope: MemoryScope.Private, userId: "user-1"),
            TestDataHelper.CreateMemory("User 2", scope: MemoryScope.Private, userId: "user-2"));
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(
            query: "", projectId: projectA, workspaceId: "ws-1", userId: "user-1");
        var result = await _service.RetrieveAsync(request);

        result.Memories.Should().HaveCount(4);
        var titles = result.Memories.Select(m => m.Title).ToList();
        titles.Should().Contain("Global");
        titles.Should().Contain("Project A");
        titles.Should().Contain("Workspace 1");
        titles.Should().Contain("User 1");
        titles.Should().NotContain("Project B");
        titles.Should().NotContain("Workspace 2");
        titles.Should().NotContain("User 2");
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
