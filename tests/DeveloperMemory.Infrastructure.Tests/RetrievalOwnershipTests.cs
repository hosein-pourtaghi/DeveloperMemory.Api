using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// Tests that retrieval providers enforce OwnerId isolation across all paths.
/// </summary>
public class RetrievalOwnershipTests : IDisposable
{
    private readonly InMemoryDbFixture _fixture;
    private readonly KeywordRetrievalProvider _keywordProvider;

    private const string OwnerA = "user-a";
    private const string OwnerB = "user-b";

    public RetrievalOwnershipTests()
    {
        _fixture = new InMemoryDbFixture();
        _fixture.ClearDatabase();
        _keywordProvider = new KeywordRetrievalProvider(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private async Task<MemoryEntry> CreateMemoryAsync(string ownerId, string title, string content, MemoryScope scope = MemoryScope.Global)
    {
        var entry = new MemoryEntry
        {
            Title = title,
            Content = content,
            Scope = scope,
            State = MemoryState.Active,
            OwnerId = ownerId,
            Importance = 0.8,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _fixture.Context.MemoryEntries.Add(entry);
        await _fixture.Context.SaveChangesAsync();
        return entry;
    }

    [Fact]
    public async Task KeywordRetrieval_ExcludesOtherOwners_Global()
    {
        var memA = await CreateMemoryAsync(OwnerA, "Secret Architecture", "PostgreSQL database design");
        var memB = await CreateMemoryAsync(OwnerB, "Secret Architecture", "PostgreSQL database design");

        var request = new RetrievalRequest { Query = "PostgreSQL", OwnerId = OwnerA };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Single(results);
        Assert.Equal(memA.Id, results[0].Id);
    }

    [Fact]
    public async Task KeywordRetrieval_ExcludesOtherOwners_Project()
    {
        var projectId = Guid.NewGuid();
        var memA = await CreateMemoryAsync(OwnerA, "Project Secret", "Database config");
        memA.Scope = MemoryScope.Project;
        memA.ProjectId = projectId;
        await _fixture.Context.SaveChangesAsync();

        var memB = await CreateMemoryAsync(OwnerB, "Project Secret", "Database config");
        memB.Scope = MemoryScope.Project;
        memB.ProjectId = projectId;
        await _fixture.Context.SaveChangesAsync();

        var request = new RetrievalRequest { Query = "Database", OwnerId = OwnerA, ProjectId = projectId };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Single(results);
        Assert.Equal(memA.Id, results[0].Id);
    }

    [Fact]
    public async Task KeywordRetrieval_ExcludesOtherOwners_Workspace()
    {
        var memA = await CreateMemoryAsync(OwnerA, "Workspace Secret", "Config");
        memA.Scope = MemoryScope.Workspace;
        memA.WorkspaceId = "ws-1";
        await _fixture.Context.SaveChangesAsync();

        var memB = await CreateMemoryAsync(OwnerB, "Workspace Secret", "Config");
        memB.Scope = MemoryScope.Workspace;
        memB.WorkspaceId = "ws-1";
        await _fixture.Context.SaveChangesAsync();

        var request = new RetrievalRequest { Query = "Config", OwnerId = OwnerA, WorkspaceId = "ws-1" };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Single(results);
        Assert.Equal(memA.Id, results[0].Id);
    }

    [Fact]
    public async Task KeywordRetrieval_ExcludesOtherOwners_Private()
    {
        var memA = await CreateMemoryAsync(OwnerA, "Private Secret", "My notes");
        memA.Scope = MemoryScope.Private;
        memA.UserId = OwnerA;
        await _fixture.Context.SaveChangesAsync();

        var memB = await CreateMemoryAsync(OwnerB, "Private Secret", "My notes");
        memB.Scope = MemoryScope.Private;
        memB.UserId = OwnerB;
        await _fixture.Context.SaveChangesAsync();

        var request = new RetrievalRequest { Query = "Secret", OwnerId = OwnerA, UserId = OwnerA };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Single(results);
        Assert.Equal(memA.Id, results[0].Id);
    }

    [Fact]
    public async Task KeywordRetrieval_AdversarialSimilarContent()
    {
        // Both owners create memories with identical content
        var memA = await CreateMemoryAsync(OwnerA, "Database Architecture", "PostgreSQL vector embeddings hybrid search");
        var memB = await CreateMemoryAsync(OwnerB, "Database Architecture", "PostgreSQL vector embeddings hybrid search");

        var requestA = new RetrievalRequest { Query = "PostgreSQL vector embeddings", OwnerId = OwnerA };
        var resultsA = await _keywordProvider.GetCandidatesAsync(requestA);

        var requestB = new RetrievalRequest { Query = "PostgreSQL vector embeddings", OwnerId = OwnerB };
        var resultsB = await _keywordProvider.GetCandidatesAsync(requestB);

        Assert.Single(resultsA);
        Assert.Equal(memA.Id, resultsA[0].Id);

        Assert.Single(resultsB);
        Assert.Equal(memB.Id, resultsB[0].Id);
    }

    [Fact]
    public async Task KeywordRetrieval_EmptyOwnerId_ReturnsNothing_FailClosed()
    {
        // When OwnerId is empty, the system MUST fail closed — return nothing
        await CreateMemoryAsync(OwnerA, "Memory A", "Content A");
        await CreateMemoryAsync(OwnerB, "Memory B", "Content B");

        var request = new RetrievalRequest { Query = "Memory" };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Empty(results); // Fail closed: no owner context = no results
    }

    [Fact]
    public async Task KeywordRetrieval_NullOwnerId_ReturnsNothing_FailClosed()
    {
        // When OwnerId is null, the system MUST fail closed
        await CreateMemoryAsync(OwnerA, "Memory A", "Content A");

        var request = new RetrievalRequest { Query = "Memory", OwnerId = null! };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Empty(results);
    }

    [Fact]
    public async Task KeywordRetrieval_NoResults_WhenOwnerHasNothing()
    {
        await CreateMemoryAsync(OwnerB, "Owner B Memory", "Some content");

        var request = new RetrievalRequest { Query = "Owner", OwnerId = OwnerA };
        var results = await _keywordProvider.GetCandidatesAsync(request);

        Assert.Empty(results);
    }
}
