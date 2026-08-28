using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class HybridQualityEvaluationPipelineTests
{
    [Fact]
    public async Task Evaluate_DeterministicOnly_ReturnsDeterministicScore()
    {
        var deterministic = new DeterministicPromptQualityEvaluator();
        var pipeline = new HybridQualityEvaluationPipeline(
            deterministic,
            new Mock<ILogger<HybridQualityEvaluationPipeline>>().Object);

        var request = new PromptEvaluationRequest
        {
            OriginalPrompt = "Fix the bug",
            OptimizedPrompt = "--- SYSTEM INSTRUCTIONS ---\nFix the bug\n---"
        };

        var result = await pipeline.EvaluateAsync(request, "Deterministic");

        Assert.False(result.LlmUsed);
        Assert.False(result.FallbackUsed);
        Assert.Equal("deterministic", result.EvaluatorUsed);
        Assert.NotNull(result.Score);
    }

    [Fact]
    public async Task Evaluate_WithNullLlmEvaluator_FallsBack()
    {
        var deterministic = new DeterministicPromptQualityEvaluator();
        var pipeline = new HybridQualityEvaluationPipeline(
            deterministic,
            new Mock<ILogger<HybridQualityEvaluationPipeline>>().Object,
            llmEvaluator: null);

        var request = new PromptEvaluationRequest
        {
            OriginalPrompt = "Test",
            OptimizedPrompt = "Test prompt"
        };

        var result = await pipeline.EvaluateAsync(request, "LLM");

        Assert.True(result.FallbackUsed);
        Assert.Contains("LLM evaluator not available", result.Issues[0]);
    }

    [Fact]
    public async Task Evaluate_AutoModeWithoutLlm_Deterministic()
    {
        var deterministic = new DeterministicPromptQualityEvaluator();
        var pipeline = new HybridQualityEvaluationPipeline(
            deterministic,
            new Mock<ILogger<HybridQualityEvaluationPipeline>>().Object);

        var request = new PromptEvaluationRequest
        {
            OriginalPrompt = "Test",
            OptimizedPrompt = "Test prompt"
        };

        var result = await pipeline.EvaluateAsync(request, "Auto");

        Assert.False(result.LlmUsed);
        Assert.Equal("deterministic", result.EvaluatorUsed);
    }

    [Fact]
    public async Task Evaluate_EvaluationDurationTracked()
    {
        var deterministic = new DeterministicPromptQualityEvaluator();
        var pipeline = new HybridQualityEvaluationPipeline(
            deterministic,
            new Mock<ILogger<HybridQualityEvaluationPipeline>>().Object);

        var request = new PromptEvaluationRequest
        {
            OriginalPrompt = "Test",
            OptimizedPrompt = "Test prompt"
        };

        var result = await pipeline.EvaluateAsync(request, "Deterministic");

        Assert.True(result.EvaluationDurationMs >= 0);
    }
}
