using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Domain.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class ExtractionOrchestratorTests
{
    private readonly Mock<IMemoryPolicy> _policyMock = new();
    private readonly Mock<ILogger<ExtractionOrchestrator>> _loggerMock = new();
    private readonly Mock<IOptions<MemoryIntelligenceOptions>> _optionsMock = new();
    private readonly DeterministicExtractionStrategy _deterministicStrategy;

    public ExtractionOrchestratorTests()
    {
        _deterministicStrategy = new DeterministicExtractionStrategy();
        _optionsMock.Setup(o => o.Value).Returns(new MemoryIntelligenceOptions
        {
            Enabled = false,
            ExtractionMode = "Deterministic"
        });
    }

    [Fact]
    public async Task ExtractAsync_DeterministicOnly_ReturnsCandidates()
    {
        var policyMock = new Mock<IMemoryPolicy>();
        policyMock.Setup(p => p.Evaluate(It.IsAny<MemoryCandidate>(), It.IsAny<IReadOnlyList<MemoryEntry>?>()))
            .Returns(new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Persist,
                Reason = "Test",
                FinalImportance = 0.7,
                FinalConfidence = 0.8
            });

        var orchestrator = new ExtractionOrchestrator(
            _deterministicStrategy,
            policyMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            null);

        var request = new MemoryExtractionRequest
        {
            Content = "I prefer PostgreSQL for personal projects"
        };

        var result = await orchestrator.ExtractAsync(request, ExtractionMode.Deterministic);

        Assert.NotEmpty(result.Candidates);
        Assert.Contains("deterministic", result.StrategyUsed);
        Assert.False(result.LlmUsed);
    }

    [Fact]
    public async Task ExtractAsync_EmptyContent_ReturnsEmpty()
    {
        var policyMock = new Mock<IMemoryPolicy>();
        var orchestrator = new ExtractionOrchestrator(
            _deterministicStrategy,
            policyMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            null);

        var request = new MemoryExtractionRequest
        {
            Content = ""
        };

        var result = await orchestrator.ExtractAsync(request);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task ExtractAsync_PolicyIgnoresCandidate_ExcludesFromResults()
    {
        var policyMock = new Mock<IMemoryPolicy>();
        policyMock.Setup(p => p.Evaluate(It.IsAny<MemoryCandidate>(), It.IsAny<IReadOnlyList<MemoryEntry>?>()))
            .Returns(new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Ignore,
                Reason = "Not important",
                FinalImportance = 0,
                FinalConfidence = 0
            });

        var orchestrator = new ExtractionOrchestrator(
            _deterministicStrategy,
            policyMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            null);

        var request = new MemoryExtractionRequest
        {
            Content = "Thanks for your help"
        };

        var result = await orchestrator.ExtractAsync(request);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task ExtractAsync_PolicyRequiresReview_IncludesInResults()
    {
        var policyMock = new Mock<IMemoryPolicy>();
        policyMock.Setup(p => p.Evaluate(It.IsAny<MemoryCandidate>(), It.IsAny<IReadOnlyList<MemoryEntry>?>()))
            .Returns(new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.RequiresReview,
                Reason = "Low confidence",
                FinalImportance = 0.5,
                FinalConfidence = 0.4
            });

        var orchestrator = new ExtractionOrchestrator(
            _deterministicStrategy,
            policyMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            null);

        var request = new MemoryExtractionRequest
        {
            Content = "Maybe they want to use React"
        };

        var result = await orchestrator.ExtractAsync(request);

        Assert.NotEmpty(result.Candidates);
        Assert.Contains(result.Warnings, w => w.Contains("review"));
    }

    [Fact]
    public async Task ExtractAsync_DeduplicatesCandidates()
    {
        var policyMock = new Mock<IMemoryPolicy>();
        policyMock.Setup(p => p.Evaluate(It.IsAny<MemoryCandidate>(), It.IsAny<IReadOnlyList<MemoryEntry>?>()))
            .Returns(new MemoryPolicyDecision
            {
                Action = MemoryPolicyAction.Persist,
                Reason = "Test",
                FinalImportance = 0.7,
                FinalConfidence = 0.8
            });

        var orchestrator = new ExtractionOrchestrator(
            _deterministicStrategy,
            policyMock.Object,
            _optionsMock.Object,
            _loggerMock.Object,
            null);

        var request = new MemoryExtractionRequest
        {
            Content = "I prefer PostgreSQL. I prefer PostgreSQL."
        };

        var result = await orchestrator.ExtractAsync(request);

        // Should deduplicate identical content
        Assert.True(result.FinalCount <= result.Candidates.Count);
    }
}
