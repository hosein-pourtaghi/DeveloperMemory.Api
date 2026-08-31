using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// Tests for the ConversationalMemoryService.
/// Covers the full ingestion pipeline: detection → extraction → validation → persistence.
/// </summary>
public class ConversationalMemoryServiceTests
{
    private readonly Mock<IConversationalMemoryDetector> _mockDetector;
    private readonly Mock<IExtractionOrchestrator> _mockExtractionOrchestrator;
    private readonly Mock<IMemoryIngestionService> _mockIngestionService;
    private readonly Mock<IMemoryRepository> _mockMemoryRepository;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IProjectRepository> _mockProjectRepository;
    private readonly Mock<ILogger<ConversationalMemoryService>> _mockLogger;
    private readonly ConversationalMemoryService _service;

    public ConversationalMemoryServiceTests()
    {
        _mockDetector = new Mock<IConversationalMemoryDetector>();
        _mockExtractionOrchestrator = new Mock<IExtractionOrchestrator>();
        _mockIngestionService = new Mock<IMemoryIngestionService>();
        _mockMemoryRepository = new Mock<IMemoryRepository>();
        _mockProjectService = new Mock<IProjectService>();
        _mockProjectRepository = new Mock<IProjectRepository>();
        _mockLogger = new Mock<ILogger<ConversationalMemoryService>>();

        _service = new ConversationalMemoryService(
            _mockDetector.Object,
            _mockExtractionOrchestrator.Object,
            _mockIngestionService.Object,
            _mockMemoryRepository.Object,
            _mockProjectService.Object,
            _mockProjectRepository.Object,
            _mockLogger.Object);
    }

    // ── No detection ──

    [Fact]
    public async Task TryIngestAsync_NoDetection_ReturnsNotDetected()
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), null))
            .Returns(new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0,
                Reason = "No patterns matched"
            });

        var result = await _service.TryIngestAsync("What is dependency injection?", "user1");

        Assert.False(result.Detected);
        Assert.False(result.Persisted);
        Assert.Equal(0, result.CreatedCount);
        _mockExtractionOrchestrator.Verify(
            e => e.ExtractAsync(It.IsAny<MemoryExtractionRequest>(), It.IsAny<ExtractionMode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Positive capture ──

    [Fact]
    public async Task TryIngestAsync_DetectedAndExtracted_PersistsMemory()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL for my projects.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.CreatedCount);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task TryIngestAsync_ExplicitRemember_PersistsMemory()
    {
        SetupDetector(true, MemoryType.Instruction);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "Remember that I use Freebuff as my coding agent.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.CreatedCount);
    }

    [Fact]
    public async Task TryIngestAsync_ConstraintStatement_PersistsMemory()
    {
        SetupDetector(true, MemoryType.UserConstraint);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "Don't recommend paid services to me.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.CreatedCount);
    }

    // ── Duplicate detection ──

    [Fact]
    public async Task TryIngestAsync_DuplicateDetected_ReportsDuplicate()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.IgnoredDuplicate);

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL for my projects.", "user1");

        Assert.True(result.Detected);
        Assert.False(result.Persisted);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.DuplicateCount);
    }

    [Fact]
    public async Task TryIngestAsync_SameStatementTwice_NoDoublePersist()
    {
        // First call
        SetupDetector(true, MemoryType.UserPreference);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result1 = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");
        Assert.Equal(1, result1.CreatedCount);

        // Second call — same statement
        SetupIngestion(MemoryIngestionOutcome.IgnoredDuplicate);

        var result2 = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");
        Assert.Equal(1, result2.DuplicateCount);
    }

    // ── Conflict / supersession ──

    [Fact]
    public async Task TryIngestAsync_ContradictionSupersedes_ReportsSuperseded()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.SupersededExisting);

        var result = await _service.TryIngestAsync(
            "I've switched to SQL Server.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.SupersededCount);
        Assert.Equal(1, result.CreatedCount);
    }

    // ── No extraction candidates ──

    [Fact]
    public async Task TryIngestAsync_DetectedButNoExtractionCandidates_ReturnsWithWarning()
    {
        SetupDetector(true, MemoryType.UserPreference);
        _mockExtractionOrchestrator.Setup(e => e.ExtractAsync(
                It.IsAny<MemoryExtractionRequest>(),
                It.IsAny<ExtractionMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionOrchestrationResult
            {
                Candidates = [],
                FinalCount = 0,
                StrategyUsed = "deterministic"
            });

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.True(result.Detected);
        Assert.False(result.Persisted);
        Assert.Single(result.Warnings, w => w.Contains("no candidates"));
    }

    // ── Requires review ──

    [Fact]
    public async Task TryIngestAsync_RequiresReview_AddsWarning()
    {
        SetupDetector(true, MemoryType.Fact);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.RequiresReview);

        var result = await _service.TryIngestAsync(
            "The API has 5 endpoints.", "user1");

        Assert.True(result.Detected);
        Assert.False(result.Persisted);
        Assert.Single(result.Warnings, w => w.Contains("review"));
    }

    // ── Rejected ──

    [Fact]
    public async Task TryIngestAsync_Rejected_AddsWarning()
    {
        SetupDetector(true, MemoryType.Other);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Rejected);

        var result = await _service.TryIngestAsync(
            "Some marginal content.", "user1");

        Assert.True(result.Detected);
        Assert.False(result.Persisted);
        Assert.Single(result.Warnings, w => w.Contains("rejected"));
    }

    // ── Scope inference ──

    [Fact]
    public async Task TryIngestAsync_WithProjectId_InfersProjectScope()
    {
        var projectId = Guid.NewGuid();
        SetupDetector(true, MemoryType.ProjectContext);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        await _service.TryIngestAsync(
            "This project uses .NET 10.", "user1", projectId: projectId);

        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Project &&
                r.ProjectId == projectId),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryIngestAsync_WithWorkspaceId_InfersWorkspaceScope()
    {
        SetupDetector(true, MemoryType.Fact);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        await _service.TryIngestAsync(
            "I use Freebuff.", "user1", workspaceId: "ws-abc");

        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Workspace &&
                r.WorkspaceId == "ws-abc"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryIngestAsync_NoMetadata_InfersGlobalScope()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Global &&
                r.ProjectId == null &&
                r.WorkspaceId == null),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Tags passthrough ──

    [Fact]
    public async Task TryIngestAsync_WithTags_PassesTagsToIngestion()
    {
        var tags = new List<string> { "architecture", "dotnet" };
        SetupDetector(true, MemoryType.ProjectContext);
        SetupExtraction(1);
        SetupIngestion(MemoryIngestionOutcome.Created);

        await _service.TryIngestAsync(
            "This project uses Clean Architecture.", "user1", tags: tags);

        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Tags != null && r.Tags.Contains("architecture")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Failure isolation ──

    [Fact]
    public async Task TryIngestAsync_DetectionFails_ReturnsFailed()
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), null))
            .Throws(new Exception("Detector broke"));

        var result = await _service.TryIngestAsync("test", "user1");

        Assert.False(result.Detected);
        Assert.True(result.Failed);
        Assert.Contains("Detector broke", result.FailureReason);
    }

    [Fact]
    public async Task TryIngestAsync_ExtractionFails_ContinuesGracefully()
    {
        SetupDetector(true, MemoryType.UserPreference);
        _mockExtractionOrchestrator.Setup(e => e.ExtractAsync(
                It.IsAny<MemoryExtractionRequest>(),
                It.IsAny<ExtractionMode>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Extraction service unavailable"));

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.True(result.Detected);
        Assert.False(result.Persisted);
        // The service should not crash
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TryIngestAsync_IngestionThrows_ContinuesGracefully()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupExtraction(1);
        _mockIngestionService.Setup(i => i.IngestAsync(
                It.IsAny<MemoryIngestionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection lost"));

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.True(result.Detected);
        Assert.False(result.Persisted);
        Assert.True(result.Warnings.Count > 0);
    }

    [Fact]
    public async Task TryIngestAsync_Cancellation_ThrowsOperationCanceled()
    {
        SetupDetector(true, MemoryType.UserPreference);
        _mockExtractionOrchestrator.Setup(e => e.ExtractAsync(
                It.IsAny<MemoryExtractionRequest>(),
                It.IsAny<ExtractionMode>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.TryIngestAsync("I prefer PostgreSQL.", "user1"));
    }

    // ── Multiple candidates ──

    [Fact]
    public async Task TryIngestAsync_MultipleCandidates_PersistsAll()
    {
        SetupDetector(true, MemoryType.UserPreference);
        _mockExtractionOrchestrator.Setup(e => e.ExtractAsync(
                It.IsAny<MemoryExtractionRequest>(),
                It.IsAny<ExtractionMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionOrchestrationResult
            {
                Candidates =
                [
                    new MemoryCandidate { Content = "prefer PostgreSQL", MemoryType = MemoryType.UserPreference, Importance = 0.8, Confidence = 0.9, Source = "deterministic", Title = "Pref1" },
                    new MemoryCandidate { Content = "use async patterns", MemoryType = MemoryType.Instruction, Importance = 0.9, Confidence = 0.85, Source = "deterministic", Title = "Instr1" }
                ],
                FinalCount = 2,
                StrategyUsed = "deterministic"
            });
        _mockIngestionService.Setup(i => i.IngestAsync(
                It.IsAny<MemoryIngestionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryIngestionResult
            {
                Outcome = MemoryIngestionOutcome.Created,
                WasPersisted = true,
                Memory = new MemoryEntry { Id = Guid.NewGuid() }
            });

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL and I always use async patterns.", "user1");

        Assert.True(result.Persisted);
        Assert.Equal(2, result.CreatedCount);
    }

    // ── Helper methods ──

    private void SetupDetector(bool detected, MemoryType suggestedType)
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), null))
            .Returns(new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = detected,
                Confidence = 0.8,
                Reason = "Test detection",
                SuggestedMemoryType = suggestedType.ToString(),
                ExtractedContent = detected ? "extracted content" : null
            });
    }

    private void SetupExtraction(int candidateCount)
    {
        var candidates = new List<MemoryCandidate>();
        for (int i = 0; i < candidateCount; i++)
        {
            candidates.Add(new MemoryCandidate
            {
                Title = $"Candidate {i}",
                Content = $"Extracted content {i}",
                MemoryType = MemoryType.UserPreference,
                Importance = 0.7,
                Confidence = 0.8,
                Source = "deterministic",
                ExtractionReason = "Test"
            });
        }

        _mockExtractionOrchestrator.Setup(e => e.ExtractAsync(
                It.IsAny<MemoryExtractionRequest>(),
                It.IsAny<ExtractionMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionOrchestrationResult
            {
                Candidates = candidates,
                FinalCount = candidateCount,
                StrategyUsed = "deterministic"
            });
    }

    private void SetupIngestion(MemoryIngestionOutcome outcome)
    {
        _mockIngestionService.Setup(i => i.IngestAsync(
                It.IsAny<MemoryIngestionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryIngestionResult
            {
                Outcome = outcome,
                WasPersisted = outcome == MemoryIngestionOutcome.Created ||
                              outcome == MemoryIngestionOutcome.SupersededExisting,
                Memory = outcome == MemoryIngestionOutcome.Created
                    ? new MemoryEntry { Id = Guid.NewGuid() }
                    : null,
                RelatedMemory = outcome == MemoryIngestionOutcome.SupersededExisting
                    ? new MemoryEntry { Id = Guid.NewGuid() }
                    : null,
                Reason = outcome.ToString()
            });
    }
}
