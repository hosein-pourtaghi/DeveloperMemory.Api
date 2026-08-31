using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// Integration tests for the KeywordRetrievalProvider against an InMemory database.
/// Proves project isolation, workspace isolation, user isolation, scope filtering,
/// and keyword search at the database level.
/// </summary>
public class KeywordRetrievalProviderTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly KeywordRetrievalProvider _provider;

    public KeywordRetrievalProviderTests()
    {
        _fixture = new InMemoryDbFixture();
        _provider = new KeywordRetrievalProvider(_fixture.Context);
    }

    [Fact]
    public async Task RetrieveAsync_GlobalMemories_AlwaysReturned()
    {
        var globalMemory = TestDataHelper.CreateMemory(
            title: "Global Config", scope: MemoryScope.Global);
        _fixture.Context.MemoryEntries.Add(globalMemory);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "Global Config");
        var results = await _provider.GetCandidatesAsync(request);

        results.Should().Contain(m => m.Id == globalMemory.Id);
    }

    // ── Project Isolation ──

    [Fact]
    public async Task RetrieveAsync_ProjectMemory_OnlyReturnedForMatchingProject()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var projectAMemory = TestDataHelper.CreateMemory(
            title: "Project A Config", scope: MemoryScope.Project, projectId: projectA);
        var projectBMemory = TestDataHelper.CreateMemory(
            title: "Project B Config", scope: MemoryScope.Project, projectId: projectB);

        _fixture.Context.MemoryEntries.AddRange(projectAMemory, projectBMemory);
        await _fixture.Context.SaveChangesAsync();

        var requestA = TestDataHelper.CreateRetrievalRequest(query: "Config", projectId: projectA);
        var resultsA = await _provider.GetCandidatesAsync(requestA);

        resultsA.Should().Contain(m => m.Id == projectAMemory.Id);
        resultsA.Should().NotContain(m => m.Id == projectBMemory.Id,
            "Project B memory must not be returned for Project A request");
    }

    [Fact]
    public async Task RetrieveAsync_NoProjectContext_ExcludesProjectScopedMemories()
    {
        var projectMemory = TestDataHelper.CreateMemory(
            title: "Project Only", scope: MemoryScope.Project, projectId: Guid.NewGuid());
        var globalMemory = TestDataHelper.CreateMemory(
            title: "Global Memory", scope: MemoryScope.Global);

        _fixture.Context.MemoryEntries.AddRange(projectMemory, globalMemory);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "Memory", projectId: null);
        var results = await _provider.GetCandidatesAsync(request);

        results.Should().NotContain(m => m.Id == projectMemory.Id,
            "Project-scoped memories should not be returned without project context");
        results.Should().Contain(m => m.Id == globalMemory.Id);
    }

    // ── Workspace Isolation ──

    [Fact]
    public async Task RetrieveAsync_WorkspaceMemory_OnlyWithMatchingWorkspaceId()
    {
        var memoryA = TestDataHelper.CreateMemory(
            title: "Workspace A Config", scope: MemoryScope.Workspace, workspaceId: "ws-A");
        var memoryB = TestDataHelper.CreateMemory(
            title: "Workspace B Config", scope: MemoryScope.Workspace, workspaceId: "ws-B");

        _fixture.Context.MemoryEntries.AddRange(memoryA, memoryB);
        await _fixture.Context.SaveChangesAsync();

        var requestA = TestDataHelper.CreateRetrievalRequest(query: "Config", workspaceId: "ws-A");
        var resultsA = await _provider.GetCandidatesAsync(requestA);

        resultsA.Should().Contain(m => m.Id == memoryA.Id);
        resultsA.Should().NotContain(m => m.Id == memoryB.Id,
            "Workspace B memory must not be returned for Workspace A request");
    }

    [Fact]
    public async Task RetrieveAsync_NoWorkspaceContext_ExcludesWorkspaceMemories()
    {
        var workspaceMemory = TestDataHelper.CreateMemory(
            title: "Workspace Config", scope: MemoryScope.Workspace, workspaceId: "ws-1");

        _fixture.Context.MemoryEntries.Add(workspaceMemory);
        await _fixture.Context.SaveChangesAsync();

        var requestNoWorkspace = TestDataHelper.CreateRetrievalRequest(
            query: "Workspace", workspaceId: null);
        var resultsNoWorkspace = await _provider.GetCandidatesAsync(requestNoWorkspace);

        resultsNoWorkspace.Should().BeEmpty("Workspace memory should not appear without workspace context");
    }

    [Fact]
    public async Task RetrieveAsync_PrivateMemory_OnlyForMatchingUser()
    {
        var userAMemory = TestDataHelper.CreateMemory(
            title: "User A Notes", scope: MemoryScope.Private, userId: "user-A");
        var userBMemory = TestDataHelper.CreateMemory(
            title: "User B Notes", scope: MemoryScope.Private, userId: "user-B");

        _fixture.Context.MemoryEntries.AddRange(userAMemory, userBMemory);
        await _fixture.Context.SaveChangesAsync();

        var requestA = TestDataHelper.CreateRetrievalRequest(
            query: "Notes", userId: "user-A");
        var resultsA = await _provider.GetCandidatesAsync(requestA);

        resultsA.Should().Contain(m => m.Id == userAMemory.Id);
        resultsA.Should().NotContain(m => m.Id == userBMemory.Id,
            "User B private memory must not be returned for User A");
    }

    [Fact]
    public async Task RetrieveAsync_NoUserContext_ExcludesPrivateMemories()
    {
        var privateMemory = TestDataHelper.CreateMemory(
            title: "Private Notes", scope: MemoryScope.Private, userId: "user-1");

        _fixture.Context.MemoryEntries.Add(privateMemory);
        await _fixture.Context.SaveChangesAsync();

        var requestNoUser = TestDataHelper.CreateRetrievalRequest(
            query: "Notes", userId: string.Empty);
        var resultsNoUser = await _provider.GetCandidatesAsync(requestNoUser);

        resultsNoUser.Should().BeEmpty("Private memory should not appear without user context");
    }

    [Fact]
    public async Task RetrieveAsync_DeletedMemories_Excluded()
    {
        var deletedMemory = TestDataHelper.CreateMemory(
            title: "Deleted Memory", scope: MemoryScope.Global, state: MemoryState.Deleted);

        _fixture.Context.MemoryEntries.Add(deletedMemory);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "Deleted");
        var results = await _provider.GetCandidatesAsync(request);

        results.Should().BeEmpty("Deleted memories must never appear in retrieval");
    }

    // ── Keyword Search ──

    [Fact]
    public async Task RetrieveAsync_KeywordSearch_MatchesTitleAndContent()
    {
        var matchingMemory = TestDataHelper.CreateMemory(
            title: "Authentication Setup", content: "How to configure JWT authentication",
            scope: MemoryScope.Global);
        var nonMatchingMemory = TestDataHelper.CreateMemory(
            title: "Deployment Guide", content: "How to deploy to production",
            scope: MemoryScope.Global);

        _fixture.Context.MemoryEntries.AddRange(matchingMemory, nonMatchingMemory);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(query: "authentication");
        var results = await _provider.GetCandidatesAsync(request);

        results.Should().Contain(m => m.Id == matchingMemory.Id);
        results.Should().NotContain(m => m.Id == nonMatchingMemory.Id);
    }

    // ── Cross-Project Full Scenario ──

    [Fact]
    public async Task RetrieveAsync_FullIsolation_OnlyCorrectMemoriesReturned()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var memories = new List<MemoryEntry>
        {
            TestDataHelper.CreateMemory("Global", scope: MemoryScope.Global),
            TestDataHelper.CreateMemory("Project A", scope: MemoryScope.Project, projectId: projectA),
            TestDataHelper.CreateMemory("Project B", scope: MemoryScope.Project, projectId: projectB),
            TestDataHelper.CreateMemory("Workspace 1", scope: MemoryScope.Workspace, workspaceId: "ws-1"),
            TestDataHelper.CreateMemory("Workspace 2", scope: MemoryScope.Workspace, workspaceId: "ws-2"),
            TestDataHelper.CreateMemory("User 1", scope: MemoryScope.Private, userId: "user-1"),
            TestDataHelper.CreateMemory("User 2", scope: MemoryScope.Private, userId: "user-2"),
        };

        _fixture.Context.MemoryEntries.AddRange(memories);
        await _fixture.Context.SaveChangesAsync();

        var request = TestDataHelper.CreateRetrievalRequest(
            query: "", projectId: projectA, workspaceId: "ws-1", userId: "user-1");
        var results = await _provider.GetCandidatesAsync(request);

        results.Should().HaveCount(4);
        var titles = results.Select(m => m.Title).ToList();
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
