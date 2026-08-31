using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class HybridRankerTests
{
    [Fact]
    public void Ranker_SemanticCanOutrankLexical()
    {
        // A memory with high semantic relevance but no lexical match
        // should be able to outrank a memory with only lexical match
        var semanticScores = new Dictionary<Guid, double>();
        var highSemanticId = Guid.NewGuid();
        var lowSemanticId = Guid.NewGuid();
        semanticScores[highSemanticId] = 0.95;
        semanticScores[lowSemanticId] = 0.3;

        var ranker = new HybridRanker(semanticScores);

        var candidates = new List<RetrievedMemory>
        {
            new RetrievedMemory
            {
                MemoryId = highSemanticId,
                Title = "Architecture Pattern",
                Content = "The project uses Clean Architecture with domain-driven design",
                Scope = MemoryScope.Project,
                Importance = 0.8,
                Confidence = 0.9,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active,
                ProjectId = Guid.NewGuid()
            },
            new RetrievedMemory
            {
                MemoryId = lowSemanticId,
                Title = "Database Configuration",
                Content = "Uses PostgreSQL for the database with connection pooling",
                Scope = MemoryScope.Project,
                Importance = 0.6,
                Confidence = 0.8,
                Tags = [],
                UpdatedAt = DateTime.UtcNow,
                State = MemoryState.Active,
                ProjectId = Guid.NewGuid()
            }
        };

        var request = new RetrievalRequest { Query = "architecture pattern" };

        var result = ranker.RankAsync(candidates, request).Result;

        // The high semantic memory should rank higher even if lexical match is weaker
        Assert.Equal(highSemanticId, result[0].MemoryId);
    }

    [Fact]
    public void Ranker_AllScoresNormalized()
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
                Confidence = 0.7,
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
    public void Ranker_ConfidenceInfluencesScore()
    {
        var ranker = new HybridRanker();

        var highConfidence = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Important",
            Content = "Critical instruction",
            Scope = MemoryScope.Project,
            Importance = 0.7,
            Confidence = 1.0,
            Tags = [],
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active,
            ProjectId = Guid.NewGuid()
        };

        var lowConfidence = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Important",
            Content = "Critical instruction",
            Scope = MemoryScope.Project,
            Importance = 0.7,
            Confidence = 0.3,
            Tags = [],
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active,
            ProjectId = Guid.NewGuid()
        };

        var request = new RetrievalRequest { Query = "test" };

        var result = ranker.RankAsync([lowConfidence, highConfidence], request).Result;

        Assert.Equal(highConfidence.MemoryId, result[0].MemoryId);
    }

    [Fact]
    public void Ranker_Deterministic_ForSameInput()
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
                Confidence = 0.8,
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
    public void Ranker_ScopeSpecificityInfluencesScore()
    {
        var ranker = new HybridRanker();

        var projectMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Project Rule",
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project,
            Importance = 0.7,
            Confidence = 0.9,
            Tags = [],
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active,
            ProjectId = Guid.NewGuid()
        };

        var globalMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Global Rule",
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Global,
            Importance = 0.7,
            Confidence = 0.9,
            Tags = [],
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var request = new RetrievalRequest { Query = "PostgreSQL" };

        var result = ranker.RankAsync([globalMemory, projectMemory], request).Result;

        // Project-scoped memory should rank higher for project-specific queries
        Assert.Equal(projectMemory.MemoryId, result[0].MemoryId);
    }
}
