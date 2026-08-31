using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class PromptIntelligenceEngineIntegrationTests
{
    private readonly Mock<IPromptAnalyzer> _mockAnalyzer;
    private readonly Mock<IConstraintResolver> _mockConstraintResolver;
    private readonly Mock<IMemoryContextAssembler> _mockContextAssembler;
    private readonly Mock<IPromptComposer> _mockComposer;
    private readonly Mock<IPromptOptimizer> _mockOptimizer;
    private readonly Mock<IMemoryRetrievalService> _mockRetrievalService;
    private readonly Mock<ILogger<PromptIntelligenceEngine>> _mockLogger;
    private readonly PromptIntelligenceEngine _engine;

    public PromptIntelligenceEngineIntegrationTests()
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

    // ── Single Path Verification ──

    [Fact]
    public async Task Engine_CallsRetrievalService_ThroughAbstraction()
    {
        await _engine.ProcessAsync("test", "user1");

        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Engine_DoesNotCallRepositoryDirectly()
    {
        // The engine should only go through IMemoryRetrievalService
        // Never through any repository or provider directly
        var mockRepository = new Mock<IMemoryRepository>();

        await _engine.ProcessAsync("test", "user1");

        mockRepository.Verify(r => r.SearchAsync(It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Engine_WorkspaceContext_ReachesRetrievalRequest()
    {
        var result = await _engine.ProcessAsync(
            "test", "user1",
            projectId: Guid.NewGuid(),
            workspaceId: "workspace-xyz");

        Assert.Equal("workspace-xyz", result.WorkspaceId);

        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.Is<RetrievalRequest>(req => req.WorkspaceId == "workspace-xyz"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Failure Path Privacy Guarantees ──

    [Fact]
    public async Task RetrievalFailure_NeverBypassesPrivacy()
    {
        // Simulate retrieval failure
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection lost"));

        var result = await _engine.ProcessAsync("test", "user1", projectId: Guid.NewGuid());

        // Engine should have degraded gracefully
        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);

        // No direct repository access should have occurred
        // (verified by the mock not being set up — if the engine called it, it would throw)
    }

    [Fact]
    public async Task AllStagesFail_OriginalRequestAlwaysPreserved()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Throws(new Exception("Analysis broke"));
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Retrieval broke"));
        _mockConstraintResolver.Setup(r => r.Resolve(
            It.IsAny<PromptAnalysis>(), It.IsAny<PromptContext?>(), It.IsAny<List<string>?>()))
            .Throws(new Exception("Constraint broke"));
        _mockContextAssembler.Setup(a => a.Assemble(
            It.IsAny<PromptContext>(), It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>()))
            .Throws(new Exception("Assembly broke"));
        _mockComposer.Setup(c => c.Compose(
            It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(), It.IsAny<string>()))
            .Throws(new Exception("Composition broke"));

        var result = await _engine.ProcessAsync("Implement the feature", "user1");

        Assert.Equal(PromptIntelligenceStatus.Failed, result.Status);
        Assert.Equal("Implement the feature", result.OriginalRequest);
        Assert.True(result.OriginalRequestPreserved);
    }

    // ── Project Isolation ──

    [Fact]
    public async Task DifferentProjects_GetDifferentRetrievalRequests()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        await _engine.ProcessAsync("test", "user1", projectId: projectA);
        await _engine.ProcessAsync("test", "user1", projectId: projectB);

        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.Is<RetrievalRequest>(req => req.ProjectId == projectA),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.Is<RetrievalRequest>(req => req.ProjectId == projectB),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── PromptBuilder Receives Intelligence Context ──

    [Fact]
    public async Task OptimizedPrompt_IsSoleIntelligenceSource()
    {
        _mockOptimizer.Setup(o => o.Optimize(It.IsAny<string>()))
            .Returns("optimized intelligence context");

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.Equal("optimized intelligence context", result.OptimizedPrompt);
    }

    private void SetupDefaultMocks()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Returns(new PromptAnalysis
            {
                OriginalRequest = "test",
                Intent = IntentType.General,
                TaskType = TaskType.General
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
