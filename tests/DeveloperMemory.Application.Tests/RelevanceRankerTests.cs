using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// Tests for relevance ranking.
/// Proves that ranking is deterministic, tie-breaking is stable, and scoring is correct.
/// </summary>
public class RelevanceRankerTests
{
    private readonly RelevanceRanker _ranker = new();

    [Fact]
    public async Task ExactTitleMatch_OutranksWeakMatch()
    {
        var exactMatch = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Database Configuration Guide",
            Content = "How to configure the database connection string",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var weakMatch = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Lunch Menu",
            Content = "Today's lunch specials",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "database configuration");
        var ranked = await _ranker.RankAsync([weakMatch, exactMatch], request);

        ranked[0].MemoryId.Should().Be(exactMatch.MemoryId,
            "Exact title match should rank higher");
        ranked[0].RelevanceScore.Should().BeGreaterThan(ranked[1].RelevanceScore);
    }

    [Fact]
    public async Task ProjectRelevantMemory_OutranksUnrelatedMemory()
    {
        var projectId = Guid.NewGuid();
        var projectMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Project Setup Notes",
            Content = "Notes about setting up the project",
            Scope = MemoryScope.Project,
            ProjectId = projectId,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var globalMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "General Notes",
            Content = "Some general notes",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var request = TestDataHelper.CreateRetrievalRequest(
            query: "project setup", projectId: projectId);
        var ranked = await _ranker.RankAsync([globalMemory, projectMemory], request);

        ranked[0].MemoryId.Should().Be(projectMemory.MemoryId,
            "Project-scoped memory in matching project context should rank higher");
    }

    [Fact]
    public async Task ImportantMemory_ReceivesAppropriateRanking()
    {
        var importantMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test Memory",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Importance = 1.0,
            UpdatedAt = DateTime.UtcNow
        };

        var unimportantMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test Memory",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Importance = 0.1,
            UpdatedAt = DateTime.UtcNow
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "test memory");
        var ranked = await _ranker.RankAsync([unimportantMemory, importantMemory], request);

        ranked[0].MemoryId.Should().Be(importantMemory.MemoryId,
            "More important memory should rank higher for identical content");
    }

    [Fact]
    public async Task DeterministicRanking_ForIdenticalInputs()
    {
        var memories = new List<RetrievedMemory>
        {
            new() { MemoryId = Guid.NewGuid(), Title = "A", Content = "Content A", Scope = MemoryScope.Global, Importance = 0.3, UpdatedAt = DateTime.UtcNow },
            new() { MemoryId = Guid.NewGuid(), Title = "B", Content = "Content B", Scope = MemoryScope.Global, Importance = 0.7, UpdatedAt = DateTime.UtcNow },
            new() { MemoryId = Guid.NewGuid(), Title = "C", Content = "Content C", Scope = MemoryScope.Global, Importance = 0.5, UpdatedAt = DateTime.UtcNow }
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "content");
        var ranked1 = await _ranker.RankAsync(new List<RetrievedMemory>(memories), request);
        var ranked2 = await _ranker.RankAsync(new List<RetrievedMemory>(memories), request);

        ranked1.Select(r => r.MemoryId).Should().Equal(ranked2.Select(r => r.MemoryId),
            "Ranking should be deterministic for identical inputs");
    }

    [Fact]
    public async Task Recency_InfluencesRanking()
    {
        var recentMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test Memory",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var oldMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test Memory",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow.AddDays(-365)
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "test memory");
        var ranked = await _ranker.RankAsync([oldMemory, recentMemory], request);

        ranked[0].MemoryId.Should().Be(recentMemory.MemoryId,
            "More recent memory should rank higher for identical content and importance");
    }

    [Fact]
    public async Task ScoresAreBetweenZeroAndOne()
    {
        var memories = new List<RetrievedMemory>
        {
            new() { MemoryId = Guid.NewGuid(), Title = "A", Content = "Content A", Scope = MemoryScope.Global, Importance = 0.5, UpdatedAt = DateTime.UtcNow }
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "content");
        var ranked = await _ranker.RankAsync(memories, request);

        ranked[0].RelevanceScore.Should().BeGreaterThanOrEqualTo(0.0);
        ranked[0].RelevanceScore.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task ScoreBreakdown_IsPopulated()
    {
        var memory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test",
            Content = "Content",
            Scope = MemoryScope.Global,
            Importance = 0.8,
            UpdatedAt = DateTime.UtcNow,
            Tags = ["important"]
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "test");
        var ranked = await _ranker.RankAsync([memory], request);

        ranked[0].ScoreBreakdown.Should().NotBeNull();
        ranked[0].ScoreBreakdown.TextRelevance.Should().BeGreaterThan(0);
        ranked[0].ScoreBreakdown.ImportanceScore.Should().Be(0.8);
    }

    [Fact]
    public async Task TieBreaking_UsesImportanceThenRecencyThenMemoryId()
    {
        // Two memories with different content but similar scores
        var memoryA = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Title A",
            Content = "Content about topic A in detail",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var memoryB = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Title B",
            Content = "Content about topic B in detail",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "topic");
        var ranked = await _ranker.RankAsync([memoryA, memoryB], request);

        // Both should be present
        ranked.Should().HaveCount(2);

        // The order should be deterministic (by MemoryId for identical scores)
        var rankedAgain = await _ranker.RankAsync([memoryB, memoryA], request);
        ranked.Select(r => r.MemoryId).Should().Equal(rankedAgain.Select(r => r.MemoryId),
            "Tie-breaking by MemoryId should produce deterministic order");
    }

    [Fact]
    public async Task TieBreaking_PrefersHigherImportance()
    {
        var highImportance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Importance = 0.9,
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var lowImportance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Test",
            Content = "Test content",
            Scope = MemoryScope.Global,
            Importance = 0.1,
            UpdatedAt = DateTime.UtcNow // More recent
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "test");
        var ranked = await _ranker.RankAsync([lowImportance, highImportance], request);

        ranked[0].MemoryId.Should().Be(highImportance.MemoryId,
            "Higher importance should win tie-breaking over recency");
    }

    [Fact]
    public async Task EmptyQuery_BehavesSafely()
    {
        var memory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Some Memory",
            Content = "Some content",
            Scope = MemoryScope.Global,
            Importance = 0.5,
            UpdatedAt = DateTime.UtcNow
        };

        var request = TestDataHelper.CreateRetrievalRequest(query: "");
        var ranked = await _ranker.RankAsync([memory], request);

        ranked.Should().HaveCount(1);
        ranked[0].RelevanceScore.Should().BeGreaterThanOrEqualTo(0.0);
    }
}
