using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class Phase11BackwardCompatibilityTests
{
    [Fact]
    public void ExistingPromptProfileConfigurationStillWorks()
    {
        var profile = new PromptProfile
        {
            Name = "Test",
            ConfigurationJson = "{\"tokenBudget\":4000,\"intentPolicy\":{\"useLlmAnalysis\":false}}"
        };

        var config = profile.GetConfiguration();

        Assert.Equal(4000, config.TokenBudget);
        Assert.False(config.IntentPolicy.UseLlmAnalysis);
    }

    [Fact]
    public void DeterministicQualityEvaluator_Standalone()
    {
        // Quality evaluator must work without any LLM or external dependencies
        var evaluator = new DeterministicPromptQualityEvaluator();

        var score = evaluator.Evaluate(
            "Use PostgreSQL for database",
            "--- SYSTEM INSTRUCTIONS ---\nUse PostgreSQL for database\n--- USER REQUEST ---\nImplement it\n---");

        Assert.NotNull(score);
        Assert.True(score.Overall > 0);
        Assert.Equal("deterministic", score.Evaluator);
    }

    [Fact]
    public void InMemoryAudit_Standalone()
    {
        // Audit must work without database
        var audit = new InMemoryPromptAudit();

        var task = audit.RecordEventAsync(new PromptAuditEvent
        {
            CorrelationId = "test",
            EventType = PromptAuditEventType.PromptAnalyzed
        });

        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void PromptAuditEventType_AllValues_Covered()
    {
        var allTypes = Enum.GetValues<PromptAuditEventType>();

        Assert.Equal(13, allTypes.Length);
    }
}
