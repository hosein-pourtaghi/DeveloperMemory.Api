using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DeveloperMemory.Tests;

/// <summary>
/// Tests for context budgeting.
/// Proves that budgeting constraints are respected and edge cases are handled safely.
/// </summary>
public class ContextBudgeterTests
{
    private readonly CharacterContextBudgeter _budgeter = new();

    [Fact]
    public async Task ContextNeverExceedsConfiguredBudget()
    {
        var memories = Enumerable.Range(1, 10)
            .Select(i => new RetrievedMemory
            {
                MemoryId = Guid.NewGuid(),
                Title = $"Memory {i}",
                Content = new string('x', 400),
                EstimatedTokens = 100,
                RelevanceScore = 1.0 - (i * 0.05)
            })
            .ToList();

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: 500);

        var totalTokens = selected.Sum(m => m.EstimatedTokens);
        totalTokens.Should().BeLessThanOrEqualTo(500,
            "Context budget must never be exceeded");
    }

    [Fact]
    public async Task HighestRankedMemoriesAreSelectedFirst()
    {
        var highRelevance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(), Title = "High", Content = "High relevance",
            EstimatedTokens = 100, RelevanceScore = 0.95
        };
        var mediumRelevance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(), Title = "Medium", Content = "Medium relevance",
            EstimatedTokens = 100, RelevanceScore = 0.60
        };
        var lowRelevance = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(), Title = "Low", Content = "Low relevance",
            EstimatedTokens = 100, RelevanceScore = 0.20
        };

        var ranked = new List<RetrievedMemory> { lowRelevance, mediumRelevance, highRelevance };
        var selected = await _budgeter.SelectWithinBudgetAsync(ranked, tokenBudget: 250);

        selected.Should().HaveCount(2);
        selected[0].MemoryId.Should().Be(highRelevance.MemoryId);
        selected[1].MemoryId.Should().Be(mediumRelevance.MemoryId);
        selected.Should().NotContain(m => m.MemoryId == lowRelevance.MemoryId,
            "Low relevance memory should be excluded when budget is insufficient");
    }

    [Fact]
    public async Task ZeroBudget_ReturnsEmpty()
    {
        var memories = new List<RetrievedMemory>
        {
            new() { MemoryId = Guid.NewGuid(), Title = "A", Content = "A", EstimatedTokens = 100 }
        };

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: 0);

        selected.Should().BeEmpty();
    }

    [Fact]
    public async Task NegativeBudget_ReturnsEmpty()
    {
        var memories = new List<RetrievedMemory>
        {
            new() { MemoryId = Guid.NewGuid(), Title = "A", Content = "A", EstimatedTokens = 100 }
        };

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: -100);

        selected.Should().BeEmpty("Negative budget should return empty safely");
    }

    [Fact]
    public async Task VerySmallBudget_BehavesSafely()
    {
        var memories = new List<RetrievedMemory>
        {
            new() { MemoryId = Guid.NewGuid(), Title = "A", Content = "A", EstimatedTokens = 100 }
        };

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: 1);

        selected.Should().BeEmpty("Very small budget should not include any memories");
    }

    [Fact]
    public async Task AllMemoriesFit_AllAreSelected()
    {
        var memories = Enumerable.Range(1, 5)
            .Select(i => new RetrievedMemory
            {
                MemoryId = Guid.NewGuid(),
                Title = $"Memory {i}",
                Content = $"Content {i}",
                EstimatedTokens = 50
            })
            .ToList();

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: 10000);

        selected.Should().HaveCount(5);
    }

    [Fact]
    public async Task EmptyInput_ReturnsEmpty()
    {
        var memories = new List<RetrievedMemory>();

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: 1000);

        selected.Should().BeEmpty();
    }

    [Fact]
    public async Task OversizedMemory_SkippedGracefully()
    {
        var oversizedMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Huge Memory",
            Content = new string('x', 40000),
            EstimatedTokens = 10000,
            RelevanceScore = 0.9
        };

        var smallMemory = new RetrievedMemory
        {
            MemoryId = Guid.NewGuid(),
            Title = "Small Memory",
            Content = "Small",
            EstimatedTokens = 10,
            RelevanceScore = 0.5
        };

        var ranked = new List<RetrievedMemory> { oversizedMemory, smallMemory };
        var selected = await _budgeter.SelectWithinBudgetAsync(ranked, tokenBudget: 100);

        selected.Should().HaveCount(1);
        selected[0].MemoryId.Should().Be(smallMemory.MemoryId,
            "Oversized memory should be skipped, smaller memory should be selected");
    }

    [Fact]
    public async Task SelectionPreservesRankingOrder()
    {
        var memories = Enumerable.Range(1, 5)
            .Select(i => new RetrievedMemory
            {
                MemoryId = Guid.NewGuid(),
                Title = $"Memory {i}",
                Content = $"Content {i}",
                EstimatedTokens = 50,
                RelevanceScore = 1.0 - (i * 0.1)
            })
            .ToList();

        var selected = await _budgeter.SelectWithinBudgetAsync(memories, tokenBudget: 200);

        // Should select first 4 (4 × 50 = 200)
        selected.Should().HaveCount(4);
        // Order should be preserved
        for (int i = 0; i < selected.Count; i++)
        {
            selected[i].MemoryId.Should().Be(memories[i].MemoryId,
                "Selection should preserve input ranking order");
        }
    }
}
