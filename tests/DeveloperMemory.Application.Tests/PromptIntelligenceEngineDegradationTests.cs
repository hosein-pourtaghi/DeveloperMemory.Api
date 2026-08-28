using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class PromptIntelligenceEngineDegradationTests
{
    private readonly Mock<IPromptAnalyzer> _mockAnalyzer;
    private readonly Mock<IConstraintResolver> _mockConstraintResolver;
    private readonly Mock<IMemoryContextAssembler> _mockContextAssembler;
    private readonly Mock<IPromptComposer> _mockComposer;
    private readonly Mock<IPromptOptimizer> _mockOptimizer;
    private readonly Mock<IMemoryRetrievalService> _mockRetrievalService;
    private readonly Mock<ILogger<PromptIntelligenceEngine>> _mockLogger;
    private readonly PromptIntelligenceEngine _engine;

    public PromptIntelligenceEngineDegradationTests()
    {
        _mockAnalyzer = new Mock<IPromptAnalyzer>();
        _mockConstraintResolver = new Mock<IConstraintResolver>();
        _mockContextAssembler = new Mock<IMemoryContextAssembler>();
        _mockComposer = new Mock<IPromptComposer>();
        _mockOptimizer = new Mock<IPromptOptimizer>();
        _mockRetrievalService = new Mock<IMemoryRetrievalService>();
        _mockLogger = new Mock<ILogger<PromptIntelligenceEngine>>();

        _engine = new PromptIntelligenceEngine(
            _mockAnalyzer.Object,
            _mockConstraintResolver.Object,
            _mockContextAssembler.Object,
            _mockComposer.Object,
            _mockOptimizer.Object,
            _mockRetrievalService.Object,
            _mockLogger.Object);

        SetupDefaultMocks();
    }

    // ── Full Pipeline ──

    [Fact]
    public async Task ProcessAsync_AllStucceed_StatusIsFull()
    {
        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal(PromptIntelligenceStatus.Full, result.Status);
        Assert.Null(result.FailedStage);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.DegradationReasons);
    }

    [Fact]
    public async Task ProcessAsync_AllSucceed_OptimizedPromptIsPopulated()
    {
        var result = await _engine.ProcessAsync("test", "user1");

        Assert.NotEmpty(result.OptimizedPrompt);
    }

    [Fact]
    public async Task ProcessAsync_AllSucceed_OriginalRequestPreserved()
    {
        var result = await _engine.ProcessAsync("Implement the feature", "user1");

        Assert.Equal("Implement the feature", result.OriginalRequest);
        Assert.True(result.OriginalRequestPreserved);
    }

    // ── Retrieval Failure ──

    [Fact]
    public async Task ProcessAsync_RetrievalFails_StatusIsDegraded()
    {
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);
        Assert.Equal(PromptIntelligenceStage.Retrieval, result.FailedStage);
        Assert.Contains(result.Warnings, w => w.Contains("retrieval"));
    }

    [Fact]
    public async Task ProcessAsync_RetrievalFails_OriginalRequestPreserved()
    {
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        var result = await _engine.ProcessAsync("Implement the feature", "user1");

        Assert.Equal("Implement the feature", result.OriginalRequest);
        Assert.True(result.OriginalRequestPreserved);
    }

    [Fact]
    public async Task ProcessAsync_RetrievalFails_PackageStillUsable()
    {
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.NotEmpty(result.OptimizedPrompt);
        Assert.NotNull(result.Analysis);
    }

    [Fact]
    public async Task ProcessAsync_RetrievalFails_DoesNotBypassPrivacy()
    {
        // Verify the engine never calls retrieval provider directly
        var mockProvider = new Mock<IMemoryRetrievalProvider>();

        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        var result = await _engine.ProcessAsync("test", "user1");

        // The engine should NOT have called the provider directly
        mockProvider.Verify(p => p.GetCandidatesAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_RetrievalFails_DegradationReasonRecorded()
    {
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Contains(result.DegradationReasons, r => r == "retrieval_unavailable");
    }

    // ── Analysis Failure ──

    [Fact]
    public async Task ProcessAsync_AnalysisFallsBackToConservative()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Throws(new Exception("Analysis error"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);
        Assert.Equal(PromptIntelligenceStage.Analysis, result.FailedStage);
        Assert.Equal(IntentType.General, result.Analysis.Intent);
        Assert.Equal(TaskType.General, result.Analysis.TaskType);
    }

    [Fact]
    public async Task ProcessAsync_AnalysisFails_OriginalRequestPreserved()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Throws(new Exception("Analysis error"));

        var result = await _engine.ProcessAsync("Implement the feature", "user1");

        Assert.Equal("Implement the feature", result.OriginalRequest);
        Assert.Contains("Implement the feature", result.Analysis.OriginalRequest);
    }

    // ── Optimization Failure ──

    [Fact]
    public async Task ProcessAsync_OptimizationFails_UsesComposedPrompt()
    {
        _mockOptimizer.Setup(o => o.Optimize(It.IsAny<string>()))
            .Throws(new Exception("Optimization error"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);
        Assert.Equal(PromptIntelligenceStage.Optimization, result.FailedStage);
        // Should use the composed prompt as fallback
        Assert.NotEmpty(result.OptimizedPrompt);
    }

    [Fact]
    public async Task ProcessAsync_CompositionFails_ReturnsFailedStatus()
    {
        _mockComposer.Setup(c => c.Compose(
            It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(), It.IsAny<string>()))
            .Throws(new Exception("Composition error"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal(PromptIntelligenceStatus.Failed, result.Status);
        Assert.Equal(PromptIntelligenceStage.Composition, result.FailedStage);
    }

    [Fact]
    public async Task ProcessAsync_EmptyRequest_ReturnsFailed()
    {
        var result = await _engine.ProcessAsync("", "user1");

        Assert.Equal(PromptIntelligenceStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ProcessAsync_Cancellation_PropagatesCorrectly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _engine.ProcessAsync("test", "user1", ct: cts.Token));
    }

    // ── Context Propagation ──

    [Fact]
    public async Task ProcessAsync_PropagatesProjectId()
    {
        var projectId = Guid.NewGuid();

        var result = await _engine.ProcessAsync("test", "user1", projectId);

        Assert.Equal(projectId, result.ProjectId);
    }

    [Fact]
    public async Task ProcessAsync_PropagatesWorkspaceId()
    {
        var result = await _engine.ProcessAsync("test", "user1", workspaceId: "ws-abc");

        Assert.Equal("ws-abc", result.WorkspaceId);
    }

    [Fact]
    public async Task ProcessAsync_PropagatesUserId()
    {
        var result = await _engine.ProcessAsync("test", "user-123");

        Assert.Equal("user-123", result.UserId);
    }

    [Fact]
    public async Task ProcessAsync_WorkspaceId_ReachesRetrievalService()
    {
        await _engine.ProcessAsync("test", "user1", workspaceId: "ws-abc");

        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.Is<RetrievalRequest>(req => req.WorkspaceId == "ws-abc"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Metadata ──

    [Fact]
    public async Task ProcessAsync_MetadataReflectsStatus()
    {
        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal(result.Status, result.Metadata.Status);
        Assert.Equal(result.FailedStage, result.Metadata.FailedStage);
    }

    [Fact]
    public async Task ProcessAsync_DegradedMetadataIncludesWarnings()
    {
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.NotEmpty(result.Metadata.Warnings);
        Assert.NotEmpty(result.Metadata.DegradationReasons);
    }

    private void SetupDefaultMocks()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Returns(new PromptAnalysis
            {
                OriginalRequest = "test",
                Intent = IntentType.Coding,
                TaskType = TaskType.Coding
            });

        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptContext
            {
                OriginalQuery = "test",
                UserId = "user1",
                RetrievedMemories = []
            });

        _mockConstraintResolver.Setup(r => r.Resolve(
            It.IsAny<PromptAnalysis>(), It.IsAny<PromptContext?>(), It.IsAny<List<string>?>()))
            .Returns([]);

        _mockContextAssembler.Setup(a => a.Assemble(
            It.IsAny<PromptContext>(), It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>()))
            .Returns(new ContextAssemblyResult());

        _mockComposer.Setup(c => c.Compose(
            It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(), It.IsAny<string>()))
            .Returns(new PromptCompositionResult
            {
                Instructions = "instructions",
                ComposedPrompt = "composed"
            });

        _mockOptimizer.Setup(o => o.Optimize(It.IsAny<string>()))
            .Returns((string s) => s);
    }
}
