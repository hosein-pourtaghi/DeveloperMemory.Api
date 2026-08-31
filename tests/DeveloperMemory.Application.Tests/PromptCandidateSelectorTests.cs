using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class PromptCandidateSelectorTests
{
    private readonly IPromptCandidateSelector _selector;

    public PromptCandidateSelectorTests()
    {
        var deterministic = new DeterministicPromptQualityEvaluator();
        var pipeline = new HybridQualityEvaluationPipeline(
            deterministic,
            new Mock<ILogger<HybridQualityEvaluationPipeline>>().Object);
        _selector = new PromptCandidateSelector(
            pipeline,
            deterministic,
            new Mock<ILogger<PromptCandidateSelector>>().Object);
    }

    [Fact]
    public async Task CompareAndSelect_SingleCandidate_SelectsIt()
    {
        var request = new PromptComparisonRequest
        {
            OriginalPrompt = "--- USER REQUEST ---\nFix the bug\n---",
            Candidates =
            [
                new PromptCandidate
                {
                    Name = "deterministic",
                    Prompt = "--- USER REQUEST ---\nFix the bug\n---",
                    OptimizationMode = "Deterministic"
                }
            ]
        };

        var result = await _selector.CompareAndSelectAsync(request);

        Assert.NotNull(result.BestCandidate);
        Assert.Equal("deterministic", result.BestCandidate!.Name);
    }

    [Fact]
    public async Task CompareAndSelect_EmptyCandidates_FallsBackToOriginal()
    {
        var request = new PromptComparisonRequest
        {
            OriginalPrompt = "Fix the bug",
            Candidates = []
        };

        var result = await _selector.CompareAndSelectAsync(request);

        Assert.NotNull(result.BestCandidate);
        Assert.Equal("original", result.BestCandidate!.Name);
        Assert.True(result.Comparison.FallbackUsed);
    }

    [Fact]
    public async Task CompareAndSelect_ComparisonIncludesOriginalScore()
    {
        var request = new PromptComparisonRequest
        {
            OriginalPrompt = "Fix the bug",
            Candidates =
            [
                new PromptCandidate
                {
                    Name = "optimized",
                    Prompt = "--- USER REQUEST ---\nFix the bug\n---"
                }
            ]
        };

        var result = await _selector.CompareAndSelectAsync(request);

        Assert.True(result.Comparison.OriginalScore >= 0);
    }
}
