using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Tests;

public class PromptIntelligenceEngineTests
{
    private readonly Mock<IPromptAnalyzer> _mockAnalyzer;
    private readonly Mock<IConstraintResolver> _mockConstraintResolver;
    private readonly Mock<IMemoryContextAssembler> _mockContextAssembler;
    private readonly Mock<IPromptComposer> _mockComposer;
    private readonly Mock<IPromptOptimizer> _mockOptimizer;
    private readonly Mock<IMemoryRetrievalService> _mockRetrievalService;
    private readonly Mock<ILogger<PromptIntelligenceEngine>> _mockLogger;
    private readonly PromptIntelligenceEngine _engine;

    public PromptIntelligenceEngineTests()
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
    }

    [Fact]
    public async Task ProcessAsync_CallsAnalyzerWithRequest()
    {
        SetupMocks();

        await _engine.ProcessAsync("Implement a feature", "user1");

        _mockAnalyzer.Verify(a => a.Analyze("Implement a feature", It.IsAny<PromptContext?>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CallsRetrievalService()
    {
        SetupMocks();

        await _engine.ProcessAsync("Implement a feature", "user1", projectId: Guid.NewGuid());

        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.Is<RetrievalRequest>(req =>
                req.UserId == "user1" &&
                req.Query == "Implement a feature"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CallsConstraintResolver()
    {
        SetupMocks();

        await _engine.ProcessAsync("Implement a feature", "user1");

        _mockConstraintResolver.Verify(r => r.Resolve(
            It.IsAny<PromptAnalysis>(),
            It.IsAny<PromptContext?>(),
            It.IsAny<List<string>?>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CallsContextAssembler()
    {
        SetupMocks();

        await _engine.ProcessAsync("Implement a feature", "user1");

        _mockContextAssembler.Verify(a => a.Assemble(
            It.IsAny<PromptContext>(),
            It.IsAny<PromptAnalysis>(),
            It.IsAny<List<PromptConstraint>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CallsComposer()
    {
        SetupMocks();

        await _engine.ProcessAsync("Implement a feature", "user1");

        _mockComposer.Verify(c => c.Compose(
            It.IsAny<PromptAnalysis>(),
            It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(),
            "Implement a feature"),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_CallsOptimizer()
    {
        SetupMocks();

        await _engine.ProcessAsync("Implement a feature", "user1");

        _mockOptimizer.Verify(o => o.Optimize(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsCompletePackage()
    {
        SetupMocks();

        var result = await _engine.ProcessAsync("Implement a feature", "user1");

        Assert.NotNull(result);
        Assert.Equal("Implement a feature", result.OriginalRequest);
        Assert.Equal("user1", result.UserId);
        Assert.NotEmpty(result.OptimizedPrompt);
        Assert.NotNull(result.Analysis);
        Assert.NotNull(result.Metadata);
    }

    [Fact]
    public async Task ProcessAsync_IncludesMetadata()
    {
        SetupMocks();

        var result = await _engine.ProcessAsync("Implement a feature", "user1");

        Assert.True(result.Metadata.TotalDurationMs > 0);
        Assert.True(result.Metadata.AnalysisDurationMs > 0);
        Assert.True(result.Metadata.FinalPromptLength > 0);
    }

    [Fact]
    public async Task ProcessAsync_RetrievalFails_ReturnsDegradedPackage()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Returns(new PromptAnalysis { OriginalRequest = "test", Intent = IntentType.General });
        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));
        _mockConstraintResolver.Setup(r => r.Resolve(
            It.IsAny<PromptAnalysis>(), It.IsAny<PromptContext?>(), It.IsAny<List<string>?>()))
            .Returns([]);
        _mockContextAssembler.Setup(a => a.Assemble(
            It.IsAny<PromptContext>(), It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>()))
            .Returns(new ContextAssemblyResult());
        _mockComposer.Setup(c => c.Compose(
            It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(), It.IsAny<string>()))
            .Returns(new PromptCompositionResult { Instructions = "test", ComposedPrompt = "test" });
        _mockOptimizer.Setup(o => o.Optimize(It.IsAny<string>()))
            .Returns("optimized");

        var result = await _engine.ProcessAsync("test request", "user1");

        Assert.NotNull(result);
        Assert.Equal("optimized", result.OptimizedPrompt);
        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);
        Assert.Contains(result.Warnings, w => w.Contains("retrieval"));
    }

    [Fact]
    public void ProcessWithContext_UsesProvidedContext()
    {
        var context = new PromptContext
        {
            OriginalQuery = "test",
            UserId = "user1",
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Test",
                    Content = "Content",
                    Scope = MemoryScope.Global,
                    Importance = 0.5
                }
            ]
        };

        _mockAnalyzer.Setup(a => a.Analyze("test", context))
            .Returns(new PromptAnalysis { OriginalRequest = "test", Intent = IntentType.General });
        _mockConstraintResolver.Setup(r => r.Resolve(
            It.IsAny<PromptAnalysis>(), context, It.IsAny<List<string>?>()))
            .Returns([]);
        _mockContextAssembler.Setup(a => a.Assemble(
            context, It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>()))
            .Returns(new ContextAssemblyResult());
        _mockComposer.Setup(c => c.Compose(
            It.IsAny<PromptAnalysis>(), It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(), "test"))
            .Returns(new PromptCompositionResult { Instructions = "ctx", ComposedPrompt = "ctx" });
        _mockOptimizer.Setup(o => o.Optimize("ctx")).Returns("optimized");

        var result = _engine.ProcessWithContext("test", context);

        Assert.NotNull(result);
        Assert.Equal("user1", result.UserId);
        Assert.Equal("optimized", result.OptimizedPrompt);
    }

    [Fact]
    public async Task ProcessAsync_PassesProjectIdAndWorkspaceId()
    {
        var projectId = Guid.NewGuid();
        SetupMocks();

        var result = await _engine.ProcessAsync("test", "user1", projectId, "ws-123");

        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("ws-123", result.WorkspaceId);
    }

    [Fact]
    public async Task ProcessAsync_PopulatesRetrievalMetadata()
    {
        SetupMocks();

        var result = await _engine.ProcessAsync("test", "user1");

        Assert.NotNull(result.RetrievalMetadata);
    }

    private void SetupMocks()
    {
        _mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>(), It.IsAny<PromptContext?>()))
            .Returns(new PromptAnalysis
            {
                OriginalRequest = "test",
                Intent = IntentType.Coding,
                TaskType = TaskType.Coding,
                Keywords = ["test"]
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
