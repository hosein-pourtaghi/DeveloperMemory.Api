using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Tests;

public class HybridRetrievalTests
{
    [Fact]
    public void HybridRanker_MergesLexicalAndSemanticScores()
    {
        var semanticScores = new Dictionary<Guid, double>
        {
            [Guid.NewGuid()] = 0.9,
            [Guid.NewGuid()] = 0.3
        };

        var ranker = new HybridRanker(semanticScores);

        var candidates = new List<RetrievedMemory>
        {
            new RetrievedMemory
            {
                MemoryId = semanticScores.Keys.First(),
                Title = "Database Choice",
                Content = "Uses PostgreSQL",
                Scope = MemoryScope.Project,
                Importance = 0.7,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active,
                ProjectId = Guid.NewGuid()
            },
            new RetrievedMemory
            {
                MemoryId = semanticScores.Keys.Last(),
                Title = "Other Memory",
                Content = "Some content",
                Scope = MemoryScope.Global,
                Importance = 0.5,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active
            }
        };

        var request = new RetrievalRequest { Query = "database" };

        var result = ranker.RankAsync(candidates, request).Result;

        Assert.Equal(2, result.Count);
        Assert.True(result[0].RelevanceScore > 0);
    }

    [Fact]
    public void HybridRanker_WithoutSemanticScores_LexicalOnly()
    {
        var ranker = new HybridRanker(null);

        var candidates = new List<RetrievedMemory>
        {
            new RetrievedMemory
            {
                MemoryId = Guid.NewGuid(),
                Title = "PostgreSQL Setup",
                Content = "Use PostgreSQL for the database",
                Scope = MemoryScope.Project,
                Importance = 0.5,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active
            }
        };

        var request = new RetrievalRequest { Query = "PostgreSQL" };

        var result = ranker.RankAsync(candidates, request).Result;

        Assert.Single(result);
        Assert.True(result[0].RelevanceScore > 0);
    }

    [Fact]
    public void HybridRanker_Deterministic_ForSameInput()
    {
        var ranker = new HybridRanker();

        var candidates = new List<RetrievedMemory>
        {
            new RetrievedMemory
            {
                MemoryId = Guid.NewGuid(),
                Title = "Test",
                Content = "Content",
                Scope = MemoryScope.Global,
                Importance = 0.5,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active
            }
        };

        var request = new RetrievalRequest { Query = "test" };

        var result1 = ranker.RankAsync(candidates, request).Result;
        var result2 = ranker.RankAsync(candidates, request).Result;

        Assert.Equal(result1[0].RelevanceScore, result2[0].RelevanceScore);
    }

    [Fact]
    public void HybridRanker_ScoresInRange()
    {
        var ranker = new HybridRanker();

        var candidates = new List<RetrievedMemory>
        {
            new RetrievedMemory
            {
                MemoryId = Guid.NewGuid(),
                Title = "Test",
                Content = "Content",
                Scope = MemoryScope.Global,
                Importance = 0.5,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active
            }
        };

        var request = new RetrievalRequest { Query = "test" };

        var result = ranker.RankAsync(candidates, request).Result;

        Assert.InRange(result[0].RelevanceScore, 0.0, 1.0);
    }

    [Fact]
    public void HybridRanker_Importance_InfluencesScore()
    {
        var ranker = new HybridRanker();

        var highImportance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Important",
            Content = "Critical instruction",
            Scope = MemoryScope.Project,
            Importance = 1.0,
            Tags = [],
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active,
            ProjectId = Guid.NewGuid()
        };

        var lowImportance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Minor",
            Content = "Minor fact",
            Scope = MemoryScope.Global,
            Importance = 0.1,
            Tags = [],
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var request = new RetrievalRequest { Query = "test" };

        var result = ranker.RankAsync([lowImportance, highImportance], request).Result;

        Assert.Equal(highImportance.MemoryId, result[0].MemoryId);
    }

    [Fact]
    public void InMemoryVectorStore_CosineSimilarity_WorksCorrectly()
    {
        var store = new InMemoryVectorStore();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        // Store two different vectors
        store.UpsertAsync(id1, new Embedding
        {
            Values = [1.0f, 0.0f, 0.0f],
            Provider = "test",
            Model = "test"
        }).Wait();

        store.UpsertAsync(id2, new Embedding
        {
            Values = [0.0f, 1.0f, 0.0f],
            Provider = "test",
            Model = "test"
        }).Wait();

        // Search for first vector — should find it with high similarity
        var results = store.SearchAsync([1.0f, 0.0f, 0.0f], 10).Result;

        Assert.NotEmpty(results);
        Assert.Equal(id1, results[0].MemoryId);
        Assert.True(results[0].SimilarityScore > 0.99);
    }

    [Fact]
    public void InMemoryVectorStore_EmptyStore_ReturnsEmpty()
    {
        var store = new InMemoryVectorStore();

        var results = store.SearchAsync([1.0f, 0.0f], 10).Result;

        Assert.Empty(results);
    }
}
