using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class MemoryRankerTests
{
    private readonly MemoryRanker _ranker = new();

    [Fact]
    public void Rank_TextMatch_RanksHigher()
    {
        var matching = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "PostgreSQL Setup",
            Content = "Use PostgreSQL for the database",
            MemoryType = MemoryType.TechnicalDecision,
            Importance = 0.5,
            Confidence = 0.8,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var nonMatching = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Frontend Framework",
            Content = "Use React for the UI",
            MemoryType = MemoryType.TechnicalDecision,
            Importance = 0.5,
            Confidence = 0.8,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var result = _ranker.Rank([nonMatching, matching], "PostgreSQL");

        Assert.Equal(matching.Id, result[0].Memory.Id);
    }

    [Fact]
    public void Rank_Importance_InfluencesScore()
    {
        var highImportance = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Memory A",
            Content = "Content A",
            MemoryType = MemoryType.Fact,
            Importance = 1.0,
            Confidence = 0.5,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var lowImportance = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Memory B",
            Content = "Content B",
            MemoryType = MemoryType.Fact,
            Importance = 0.1,
            Confidence = 0.5,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var result = _ranker.Rank([lowImportance, highImportance], "test");

        Assert.Equal(highImportance.Id, result[0].Memory.Id);
    }

    [Fact]
    public void Rank_InstructionType_RanksHigherThanFact()
    {
        var instruction = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Instruction",
            Content = "Always run tests before commit",
            MemoryType = MemoryType.Instruction,
            Importance = 0.5,
            Confidence = 0.8,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var fact = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Fact",
            Content = "The API uses .NET 10",
            MemoryType = MemoryType.Fact,
            Importance = 0.5,
            Confidence = 0.8,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var result = _ranker.Rank([fact, instruction], "test");

        Assert.Equal(instruction.Id, result[0].Memory.Id);
    }

    [Fact]
    public void Rank_Recency_InfluencesScore()
    {
        var recent = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Memory A",
            Content = "Same content",
            MemoryType = MemoryType.Fact,
            Importance = 0.5,
            Confidence = 0.5,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var old = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Memory B",
            Content = "Same content",
            MemoryType = MemoryType.Fact,
            Importance = 0.5,
            Confidence = 0.5,
            UpdatedAt = DateTime.UtcNow.AddDays(-100),
            State = MemoryState.Active
        };

        var result = _ranker.Rank([old, recent], "test");

        Assert.Equal(recent.Id, result[0].Memory.Id);
    }

    [Fact]
    public void Rank_Deterministic_ForSameInput()
    {
        var memories = new[]
        {
            new MemoryEntry
            {
                Id = Guid.NewGuid(), Title = "A", Content = "Content A",
                MemoryType = MemoryType.Fact, Importance = 0.5, Confidence = 0.5,
                UpdatedAt = DateTime.UtcNow, State = MemoryState.Active
            },
            new MemoryEntry
            {
                Id = Guid.NewGuid(), Title = "B", Content = "Content B",
                MemoryType = MemoryType.Fact, Importance = 0.7, Confidence = 0.5,
                UpdatedAt = DateTime.UtcNow, State = MemoryState.Active
            }
        };

        var result1 = _ranker.Rank(memories, "test");
        var result2 = _ranker.Rank(memories, "test");

        Assert.Equal(result1[0].Memory.Id, result2[0].Memory.Id);
        Assert.Equal(result1[0].RelevanceScore, result2[0].RelevanceScore);
    }

    [Fact]
    public void Rank_EmptyQuery_ReturnsNeutralScores()
    {
        var memories = new[]
        {
            new MemoryEntry
            {
                Id = Guid.NewGuid(), Title = "A", Content = "Content",
                MemoryType = MemoryType.Fact, Importance = 0.5, Confidence = 0.5,
                UpdatedAt = DateTime.UtcNow, State = MemoryState.Active
            }
        };

        var result = _ranker.Rank(memories, "");

        Assert.Single(result);
        Assert.True(result[0].RelevanceScore > 0);
    }

    [Fact]
    public void Rank_ProjectContext_ScopeSpecificity()
    {
        var projectId = Guid.NewGuid();
        var context = new RankingContext { ProjectId = projectId };

        var projectMemory = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Project Memory",
            Content = "Project uses PostgreSQL",
            Scope = MemoryScope.Project,
            ProjectId = projectId,
            MemoryType = MemoryType.ProjectContext,
            Importance = 0.5,
            Confidence = 0.5,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var globalMemory = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Title = "Global Memory",
            Content = "Project uses PostgreSQL",
            Scope = MemoryScope.Global,
            MemoryType = MemoryType.ProjectContext,
            Importance = 0.5,
            Confidence = 0.5,
            UpdatedAt = DateTime.UtcNow,
            State = MemoryState.Active
        };

        var result = _ranker.Rank([globalMemory, projectMemory], "PostgreSQL", context);

        Assert.Equal(projectMemory.Id, result[0].Memory.Id);
    }

    [Fact]
    public void Rank_ScoresInRange()
    {
        var memories = new[]
        {
            new MemoryEntry
            {
                Id = Guid.NewGuid(), Title = "A", Content = "Content",
                MemoryType = MemoryType.Fact, Importance = 0.5, Confidence = 0.5,
                UpdatedAt = DateTime.UtcNow, State = MemoryState.Active
            }
        };

        var result = _ranker.Rank(memories, "test");

        Assert.InRange(result[0].RelevanceScore, 0.0, 1.0);
    }
}
