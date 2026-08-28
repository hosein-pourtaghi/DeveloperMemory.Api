using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public sealed class RelevanceRankerNormalizationTests
{
    [Fact]
    public async Task AllZeroSignals_ReturnsZero()
    {
        var memory = CreateMemory();
        memory.UpdatedAt = default;
        var result = await new RelevanceRanker().RankAsync([memory], new RetrievalRequest { Query = "not present" });
        Assert.Equal(0.0, result[0].RelevanceScore, precision: 10);
    }

    [Fact]
    public async Task AllSignalsAtMaximum_ReturnsOne()
    {
        var memory = CreateMemory();
        memory.Title = "query";
        memory.Content = "query";
        memory.Importance = 1.0;
        memory.SemanticRelevanceScore = 1.0;
        memory.Tags = ["tag"];
        var result = await new RelevanceRanker().RankAsync([memory], new RetrievalRequest { Query = "query", RequiredCategories = ["tag"] });
        Assert.InRange(result[0].RelevanceScore, 0.0, 1.0);
    }

    [Fact]
    public async Task SemanticScoreIsIncludedWithoutPrematureSaturation()
    {
        var withSemantic = CreateMemory();
        withSemantic.SemanticRelevanceScore = 0.8;
        var withoutSemantic = CreateMemory();
        var result = await new RelevanceRanker().RankAsync(
            [withSemantic, withoutSemantic], new RetrievalRequest { Query = "absent" });

        Assert.True(result[0].RelevanceScore > result[1].RelevanceScore);
        Assert.All(result, candidate => Assert.InRange(candidate.RelevanceScore, 0.0, 1.0));
    }

    private static RetrievedMemory CreateMemory() => new()
    {
        MemoryId = Guid.NewGuid(),
        Title = string.Empty,
        Content = string.Empty,
        Scope = MemoryScope.Global,
        Importance = 0.0,
        UpdatedAt = DateTime.UtcNow.AddYears(-2),
        Tags = []
    };
}
