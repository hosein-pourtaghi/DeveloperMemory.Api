using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// Integration tests verifying the PromptIntelligenceEngine correctly orchestrates
/// conversational memory ingestion alongside the existing intelligence pipeline.
/// </summary>
public class PromptIntelligenceEngineIngestionTests
{
    private readonly Mock<IPromptAnalyzer> _mockAnalyzer;
    private readonly Mock<IConstraintResolver> _mockConstraintResolver;
    private readonly Mock<IMemoryContextAssembler> _mockContextAssembler;
    private readonly Mock<IPromptComposer> _mockComposer;
    private readonly Mock<IPromptOptimizer> _mockOptimizer;
    private readonly Mock<IMemoryRetrievalService> _mockRetrievalService;
    private readonly Mock<IConversationalMemoryService> _mockIngestionService;
    private readonly Mock<ILogger<PromptIntelligenceEngine>> _mockLogger;
    private readonly PromptIntelligenceEngine _engine;

    public PromptIntelligenceEngineIngestionTests()
    {
        _mockAnalyzer = new Mock<IPromptAnalyzer>();
        _mockConstraintResolver = new Mock<IConstraintResolver>();
        _mockContextAssembler = new Mock<IMemoryContextAssembler>();
        _mockComposer = new Mock<IPromptComposer>();
        _mockOptimizer = new Mock<IPromptOptimizer>();
        _mockRetrievalService = new Mock<IMemoryRetrievalService>();
        _mockIngestionService = new Mock<IConversationalMemoryService>();
        _mockLogger = new Mock<ILogger<PromptIntelligenceEngine>>();

        _engine = new PromptIntelligenceEngine(
            _mockAnalyzer.Object,
            _mockConstraintResolver.Object,
            _mockContextAssembler.Object,
            _mockComposer.Object,
            _mockOptimizer.Object,
            _mockRetrievalService.Object,
            _mockLogger.Object,
            _mockIngestionService.Object);

        SetupDefaultMocks();
    }

    // ── Ingestion is called for memory-worthy messages ──

    [Fact]
    public async Task ProcessAsync_DurableMessage_CallsIngestionService()
    {
        await _engine.ProcessAsync(
            "I prefer PostgreSQL for my projects.", "user1");

        _mockIngestionService.Verify(s => s.TryIngestAsync(
            "I prefer PostgreSQL for my projects.",
            "user1",
            null,   // projectId
            null,   // workspaceId
            null,   // tags
            null,   // conversationHistory
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithProjectAndTags_PassesContextToIngestion()
    {
        var projectId = Guid.NewGuid();
        var tags = new List<string> { "architecture" };

        await _engine.ProcessAsync(
            "I prefer PostgreSQL.", "user1",
            projectId: projectId,
            workspaceId: "ws-abc",
            tags: tags);

        _mockIngestionService.Verify(s => s.TryIngestAsync(
            "I prefer PostgreSQL.",
            "user1",
            projectId,
            "ws-abc",
            tags,
            null,   // conversationHistory
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Ingestion is non-fatal ──

    [Fact]
    public async Task ProcessAsync_IngestionFails_PipelineContinues()
    {
        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Ingestion service unavailable"));

        var result = await _engine.ProcessAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.NotNull(result);
        Assert.Equal(PromptIntelligenceStatus.Full, result.Status);
        Assert.Equal("I prefer PostgreSQL.", result.OriginalRequest);
    }

    [Fact]
    public async Task ProcessAsync_IngestionFails_RetrievalStillRuns()
    {
        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Ingestion broke"));

        await _engine.ProcessAsync("I prefer PostgreSQL.", "user1");

        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_IngestionFails_AllSubsequentStagesRun()
    {
        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Ingestion broke"));

        var result = await _engine.ProcessAsync(
            "I prefer PostgreSQL.", "user1");

        // All downstream stages should have run
        _mockConstraintResolver.Verify(r => r.Resolve(
            It.IsAny<PromptAnalysis>(),
            It.IsAny<PromptContext?>(),
            It.IsAny<List<string>?>()),
            Times.Once);
        _mockContextAssembler.Verify(a => a.Assemble(
            It.IsAny<PromptContext>(),
            It.IsAny<PromptAnalysis>(),
            It.IsAny<List<PromptConstraint>>()),
            Times.Once);
        _mockComposer.Verify(c => c.Compose(
            It.IsAny<PromptAnalysis>(),
            It.IsAny<List<PromptConstraint>>(),
            It.IsAny<List<ContextSection>>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>()),
            Times.Once);
        _mockOptimizer.Verify(o => o.Optimize(It.IsAny<string>()), Times.Once);
    }

    // ── Ingestion metadata is captured ──

    [Fact]
    public async Task ProcessAsync_IngestionDetected_CapturesMetadata()
    {
        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationalMemoryIngestionResult
            {
                Detected = true,
                Persisted = true,
                CreatedCount = 1,
                DuplicateCount = 0,
                SupersededCount = 0
            });

        var result = await _engine.ProcessAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.True(result.Metadata.IngestionDetected);
        Assert.Equal(1, result.Metadata.IngestionCreatedCount);
        Assert.True(result.Metadata.IngestionDurationMs >= 0);
    }

    [Fact]
    public async Task ProcessAsync_IngestionNotDetected_MetadataReflectsNoDetection()
    {
        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationalMemoryIngestionResult
            {
                Detected = false,
                Persisted = false,
                CreatedCount = 0
            });

        var result = await _engine.ProcessAsync(
            "What is dependency injection?", "user1");

        Assert.False(result.Metadata.IngestionDetected);
        Assert.Equal(0, result.Metadata.IngestionCreatedCount);
    }

    // ── Null service is handled ──

    [Fact]
    public async Task ProcessAsync_NullIngestionService_SkipsIngestion()
    {
        var engineNoIngestion = new PromptIntelligenceEngine(
            _mockAnalyzer.Object,
            _mockConstraintResolver.Object,
            _mockContextAssembler.Object,
            _mockComposer.Object,
            _mockOptimizer.Object,
            _mockRetrievalService.Object,
            _mockLogger.Object,
            null); // No ingestion service

        var result = await engineNoIngestion.ProcessAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.NotNull(result);
        Assert.Equal(PromptIntelligenceStatus.Full, result.Status);
        Assert.False(result.Metadata.IngestionDetected);
    }

    // ── Pipeline ordering: ingestion before retrieval ──

    [Fact]
    public async Task ProcessAsync_IngestionRunsBeforeRetrieval()
    {
        var callOrder = new List<string>();

        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("ingestion"))
            .ReturnsAsync(new ConversationalMemoryIngestionResult { Detected = true, Persisted = true, CreatedCount = 1 });

        _mockRetrievalService.Setup(r => r.BuildPromptContextAsync(
                It.IsAny<RetrievalRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("retrieval"))
            .ReturnsAsync(new PromptContext
            {
                OriginalQuery = "test",
                UserId = "user1",
                RetrievedMemories = []
            });

        await _engine.ProcessAsync("I prefer PostgreSQL.", "user1");

        Assert.Equal(2, callOrder.Count);
        Assert.Equal("ingestion", callOrder[0]);
        Assert.Equal("retrieval", callOrder[1]);
    }

    // ── Tags are forwarded ──

    [Fact]
    public async Task ProcessAsync_WithTags_ForwardedToIngestion()
    {
        var tags = new List<string> { "database", "preference" };

        await _engine.ProcessAsync(
            "I prefer PostgreSQL.", "user1", tags: tags);

        _mockIngestionService.Verify(s => s.TryIngestAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            tags,
            null,   // conversationHistory
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Backward compatibility: existing behavior unchanged ──

    [Fact]
    public async Task ProcessAsync_IngestionDisabled_PipelineWorksNormally()
    {
        var engine = new PromptIntelligenceEngine(
            _mockAnalyzer.Object,
            _mockConstraintResolver.Object,
            _mockContextAssembler.Object,
            _mockComposer.Object,
            _mockOptimizer.Object,
            _mockRetrievalService.Object,
            _mockLogger.Object,
            null); // No ingestion service

        var result = await engine.ProcessAsync("test request", "user1");

        Assert.NotNull(result);
        Assert.Equal("test request", result.OriginalRequest);
        Assert.Equal("user1", result.UserId);
        Assert.Equal(PromptIntelligenceStatus.Full, result.Status);
        Assert.NotEmpty(result.OptimizedPrompt);

        // All existing stages still ran
        _mockRetrievalService.Verify(r => r.BuildPromptContextAsync(
            It.IsAny<RetrievalRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
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
                It.IsAny<RetrievalRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromptContext
            {
                OriginalQuery = "test",
                UserId = "user1",
                RetrievedMemories = []
            });

        _mockConstraintResolver.Setup(r => r.Resolve(
                It.IsAny<PromptAnalysis>(),
                It.IsAny<PromptContext?>(),
                It.IsAny<List<string>?>()))
            .Returns([]);

        _mockContextAssembler.Setup(a => a.Assemble(
                It.IsAny<PromptContext>(),
                It.IsAny<PromptAnalysis>(),
                It.IsAny<List<PromptConstraint>>()))
            .Returns(new ContextAssemblyResult());

        _mockComposer.Setup(c => c.Compose(
                It.IsAny<PromptAnalysis>(),
                It.IsAny<List<PromptConstraint>>(),
                It.IsAny<List<ContextSection>>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(new PromptCompositionResult
            {
                Instructions = "instructions",
                ComposedPrompt = "composed"
            });

        _mockOptimizer.Setup(o => o.Optimize(It.IsAny<string>()))
            .Returns((string s) => s);

        _mockIngestionService.Setup(s => s.TryIngestAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationalMemoryIngestionResult
            {
                Detected = false,
                Persisted = false
            });
    }
}
