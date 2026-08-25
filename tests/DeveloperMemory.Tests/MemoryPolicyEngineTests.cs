using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Tests;

public class MemoryPolicyEngineTests
{
    private readonly Mock<ILogger<MemoryPolicyEngine>> _loggerMock = new();
    private readonly MemoryPolicyEngine _engine;

    public MemoryPolicyEngineTests()
    {
        _engine = new MemoryPolicyEngine(_loggerMock.Object);
    }

    [Fact]
    public void Evaluate_EmptyContent_Ignores()
    {
        var candidate = new MemoryCandidate
        {
            Content = "",
            MemoryType = MemoryType.Fact
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldIgnore);
        Assert.Contains("Empty", decision.Reason);
    }

    [Fact]
    public void Evaluate_ShortContent_Ignores()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Hi",
            MemoryType = MemoryType.Fact
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldIgnore);
        Assert.Contains("short", decision.Reason);
    }

    [Fact]
    public void Evaluate_SecretContent_RequiresReview()
    {
        var candidate = new MemoryCandidate
        {
            Content = "The API key is sk-1234567890",
            MemoryType = MemoryType.Fact
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.RequiresReview);
        Assert.Contains("sensitive", decision.Reason);
    }

    [Fact]
    public void Evaluate_SecurityConstraint_Persists()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Never expose API keys in logs",
            MemoryType = MemoryType.UserConstraint
        };

        var decision = _engine.Evaluate(candidate);

        // Security instructions are legitimate
        Assert.False(decision.ShouldIgnore);
    }

    [Fact]
    public void Evaluate_TemporaryContext_Ignores()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Today I'm working on Phase 8 implementation",
            MemoryType = MemoryType.WorkingContext,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldIgnore);
        Assert.Contains("Temporary", decision.Reason);
    }

    [Fact]
    public void Evaluate_HighConfidence_Persists()
    {
        var candidate = new MemoryCandidate
        {
            Content = "This project must use PostgreSQL for the database",
            MemoryType = MemoryType.UserConstraint,
            Importance = 0.9,
            Confidence = 0.95
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldPersist);
        Assert.Contains("High confidence", decision.Reason);
    }

    [Fact]
    public void Evaluate_ModerateConfidence_Persists()
    {
        var candidate = new MemoryCandidate
        {
            Content = "The project uses .NET 10 for the backend",
            MemoryType = MemoryType.ProjectContext,
            Importance = 0.7,
            Confidence = 0.75
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldPersist);
    }

    [Fact]
    public void Evaluate_LowConfidence_RequiresReview()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Maybe they prefer React for the frontend",
            MemoryType = MemoryType.UserPreference,
            Importance = 0.5,
            Confidence = 0.4
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.RequiresReview);
    }

    [Fact]
    public void Evaluate_VeryLowConfidence_Ignores()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Something vague",
            MemoryType = MemoryType.Other,
            Importance = 0.3,
            Confidence = 0.2
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldIgnore);
    }

    [Fact]
    public void Evaluate_SupersedesOlderMemory()
    {
        var candidate = new MemoryCandidate
        {
            Content = "We use PostgreSQL for the database",
            MemoryType = MemoryType.ProjectContext,
            Importance = 0.8,
            Confidence = 0.9
        };

        var relatedMemories = new List<MemoryEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "We use SQLite for the database",
                MemoryType = MemoryType.ProjectContext,
                Importance = 0.6
            }
        };

        var decision = _engine.Evaluate(candidate, relatedMemories);

        Assert.True(decision.ShouldPersist);
    }

    [Fact]
    public void Evaluate_Instruction_GetsHighImportance()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Always use async patterns for I/O operations",
            MemoryType = MemoryType.Instruction,
            Importance = 0.5, // Will be boosted
            Confidence = 0.8
        };

        var decision = _engine.Evaluate(candidate);

        Assert.True(decision.ShouldPersist);
        Assert.True(decision.FinalImportance >= 0.8);
    }

    [Fact]
    public void Evaluate_ValuesClamped()
    {
        var candidate = new MemoryCandidate
        {
            Content = "Test content that is long enough to pass validation",
            MemoryType = MemoryType.Fact,
            Importance = 1.5, // Out of range
            Confidence = -0.5 // Out of range
        };

        var decision = _engine.Evaluate(candidate);

        Assert.InRange(decision.FinalImportance, 0.0, 1.0);
        Assert.InRange(decision.FinalConfidence, 0.0, 1.0);
    }
}
