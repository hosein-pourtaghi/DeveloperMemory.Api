using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class Phase12BackwardCompatibilityTests
{
    [Fact]
    public void DeterministicEvaluator_Standalone_Works()
    {
        var evaluator = new DeterministicPromptQualityEvaluator();
        var score = evaluator.Evaluate(
            "Fix the bug in database",
            "--- SYSTEM ---\nFix the bug\n--- USER ---\nFix the bug in database");

        Assert.NotNull(score);
        Assert.True(score.Overall > 0);
        Assert.Equal("deterministic", score.Evaluator);
    }

    [Fact]
    public void InMemoryMetrics_Standalone_Works()
    {
        var metrics = new InMemoryPromptMetrics();
        metrics.RecordProcessingRequest(new PromptProcessingMetric
        {
            Intent = "Test",
            ProcessingDurationMs = 50
        });

        var summary = metrics.GetSummary();
        Assert.Equal(1, summary.TotalRequests);
    }

    [Fact]
    public void ExperimentStatus_AllValues_Defined()
    {
        var statuses = Enum.GetValues<ExperimentStatus>();

        Assert.Contains(ExperimentStatus.Draft, statuses);
        Assert.Contains(ExperimentStatus.Running, statuses);
        Assert.Contains(ExperimentStatus.Paused, statuses);
        Assert.Contains(ExperimentStatus.Completed, statuses);
        Assert.Contains(ExperimentStatus.Cancelled, statuses);
    }

    [Fact]
    public void PromptProfileConfiguration_BackwardCompatible()
    {
        var profile = new PromptProfile
        {
            ConfigurationJson = "{\"tokenBudget\":4000,\"intentPolicy\":{\"useLlmAnalysis\":false}}"
        };

        var config = profile.GetConfiguration();

        Assert.Equal(4000, config.TokenBudget);
        Assert.False(config.IntentPolicy.UseLlmAnalysis);
    }
}
