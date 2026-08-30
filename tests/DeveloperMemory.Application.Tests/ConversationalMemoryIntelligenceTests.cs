using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// Phase M: Focused tests for conversational context, memory &amp; agent-accessible intelligence.
/// Tests the enhanced ConversationalMemoryService with project resolution, scope inference,
/// conversation history, and contradiction handling.
/// </summary>
public class ConversationalMemoryIntelligenceTests
{
    private readonly Mock<IConversationalMemoryDetector> _mockDetector;
    private readonly Mock<IExtractionOrchestrator> _mockExtractionOrchestrator;
    private readonly Mock<IMemoryIngestionService> _mockIngestionService;
    private readonly Mock<IMemoryRepository> _mockMemoryRepository;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IProjectRepository> _mockProjectRepository;
    private readonly Mock<ILogger<ConversationalMemoryService>> _mockLogger;
    private readonly ConversationalMemoryService _service;

    public ConversationalMemoryIntelligenceTests()
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

    // ── User Memory ──

    [Fact]
    public async Task UserPreference_IPreferConciseAnswers_GlobalScope()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupSingleExtraction("User Preference", "I prefer concise answers", MemoryType.UserPreference);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "I prefer concise answers.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.CreatedCount);

        // Should be Global scope since no project context
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Global),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UserPreference_IPreferPostgreSQL_GlobalScope()
    {
        SetupDetector(true, MemoryType.UserPreference);
        SetupSingleExtraction("User Preference", "I prefer PostgreSQL", MemoryType.UserPreference);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
    }

    // ── Explicit Memory ──

    [Fact]
    public async Task ExplicitRemember_RememberThatIPreferPostgreSQL_PersistsMemory()
    {
        SetupDetector(true, MemoryType.Instruction);
        SetupSingleExtraction("Instruction", "Remember that I prefer PostgreSQL", MemoryType.Instruction);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "Remember that I prefer PostgreSQL.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.CreatedCount);
    }

    // ── Project Discovery ──

    [Fact]
    public async Task ProjectDiscovery_IMWorkingOnProject_ResolvesProject()
    {
        // Arrange: project exists in the database
        var project = new ProjectDto { Id = Guid.NewGuid(), Name = "DeveloperMemory.Api" };
        _mockProjectService.Setup(s => s.GetByNameAsync("DeveloperMemory.Api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        SetupDetector(true, MemoryType.Fact);
        SetupSingleExtraction("Fact", "Working on DeveloperMemory.Api", MemoryType.Fact);
        SetupIngestion(MemoryIngestionOutcome.Created);

        // Act
        var result = await _service.TryIngestAsync(
            "I'm working on DeveloperMemory.Api.", "user1");

        // Assert: project should be resolved
        Assert.True(result.Detected);
        Assert.True(result.Persisted);

        // Should use Project scope since project was resolved
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Project &&
                r.ProjectId == project.Id),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProjectDiscovery_ProjectNotFound_FallsBackToGlobal()
    {
        // Arrange: no project found
        _mockProjectService.Setup(s => s.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDto?)null);
        _mockProjectService.Setup(s => s.SearchByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDto>());

        SetupDetector(true, MemoryType.UserPreference);
        SetupSingleExtraction("User Preference", "I prefer PostgreSQL", MemoryType.UserPreference);
        SetupIngestion(MemoryIngestionOutcome.Created);

        // Act
        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL for my projects.", "user1");

        // Assert: falls back to Global scope
        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Global),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Project Decision ──

    [Fact]
    public async Task ProjectDecision_DeveloperMemoryShouldRemain_OpenAICompatible()
    {
        // Arrange
        var project = new ProjectDto { Id = Guid.NewGuid(), Name = "DeveloperMemory.Api" };
        _mockProjectService.Setup(s => s.GetByNameAsync("DeveloperMemory.Api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        SetupDetector(true, MemoryType.ArchitectureDecision);
        SetupSingleExtraction("Decision", "DeveloperMemory.Api should remain OpenAI-compatible", MemoryType.ArchitectureDecision);
        SetupIngestion(MemoryIngestionOutcome.Created);

        // Act
        var result = await _service.TryIngestAsync(
            "DeveloperMemory.Api should remain OpenAI-compatible.", "user1");

        // Assert
        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Project &&
                r.ProjectId == project.Id &&
                r.MemoryType == MemoryType.ArchitectureDecision),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Context Reference ──

    [Fact]
    public async Task ContextReference_ThisProject_ResolvesFromConversationHistory()
    {
        // Arrange: project was mentioned in conversation history
        var project = new ProjectDto { Id = Guid.NewGuid(), Name = "DeveloperMemory.Api" };
        _mockProjectService.Setup(s => s.GetByNameAsync("DeveloperMemory.Api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var conversationHistory = new List<string>
        {
            "I'm working on DeveloperMemory.Api.",
            "Use PostgreSQL for this project."
        };

        SetupDetector(true, MemoryType.ProjectContext);
        SetupSingleExtraction("Project Context", "Use PostgreSQL for this project", MemoryType.ProjectContext);
        SetupIngestion(MemoryIngestionOutcome.Created);

        // Act: second message with "this project" reference
        var result = await _service.TryIngestAsync(
            "Use PostgreSQL for this project.", "user1",
            conversationHistory: conversationHistory);

        // Assert: "this project" should resolve to DeveloperMemory.Api
        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Project &&
                r.ProjectId == project.Id),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Conversation History Passed to Detector ──

    [Fact]
    public async Task ConversationHistory_IsPassedToDetector()
    {
        var history = new List<string> { "I'm working on DeveloperMemory.Api." };

        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), history))
            .Returns(new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0,
                Reason = "Test"
            });

        var result = await _service.TryIngestAsync(
            "What is the capital of France?", "user1",
            conversationHistory: history);

        Assert.False(result.Detected);
        // Verify detector was called with the conversation history
        _mockDetector.Verify(d => d.Detect(
            "What is the capital of France?", history), Times.Once);
    }

    // ── No False Memory ──

    [Fact]
    public async Task NoFalseMemory_QuestionAboutPostgreSQL_NoMemoryCreated()
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), null))
            .Returns(new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0.1,
                Reason = "Question, not a durable fact"
            });

        var result = await _service.TryIngestAsync(
            "What is PostgreSQL?", "user1");

        Assert.False(result.Detected);
        Assert.False(result.Persisted);
        Assert.Equal(0, result.CreatedCount);

        // Should not reach extraction or ingestion
        _mockExtractionOrchestrator.Verify(
            e => e.ExtractAsync(It.IsAny<MemoryExtractionRequest>(), It.IsAny<ExtractionMode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Temporary Statement ──

    [Fact]
    public async Task TemporaryStatement_TemporarilyUsingSQLite_NotDetectedAsDurable()
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), null))
            .Returns(new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = false,
                Confidence = 0.2,
                Reason = "Temporary context - not durable"
            });

        var result = await _service.TryIngestAsync(
            "For this test I'm temporarily using SQLite.", "user1");

        Assert.False(result.Detected);
        Assert.False(result.Persisted);
    }

    // ── No Metadata Required ──

    [Fact]
    public async Task NoMetadata_StandardOpenAIRequest_IntelligenceStillWorks()
    {
        // Simulates a standard Open WebUI request with no project/tags/workspace
        SetupDetector(true, MemoryType.UserConstraint);
        SetupSingleExtraction("Constraint", "Don't recommend paid tools", MemoryType.UserConstraint);
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "Don't recommend paid tools to me.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);

        // Should use Global scope (no metadata provided)
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Global &&
                r.ProjectId == null &&
                r.WorkspaceId == null),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Contradiction / Supersession ──

    [Fact]
    public async Task Contradiction_SwitchToSqlServer_SupersedesExisting()
    {
        SetupDetector(true, MemoryType.TechnicalDecision);
        SetupSingleExtraction("Technical Decision", "Switch to SQL Server", MemoryType.TechnicalDecision);
        SetupIngestion(MemoryIngestionOutcome.SupersededExisting);

        var result = await _service.TryIngestAsync(
            "We switched from PostgreSQL to SQL Server.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);
        Assert.Equal(1, result.SupersededCount);
        Assert.Equal(1, result.CreatedCount);
    }

    // ── Scope Inference ──

    [Fact]
    public async Task ScopeInference_ExplicitProjectId_UsesProjectScope()
    {
        var projectId = Guid.NewGuid();
        SetupDetector(true, MemoryType.ProjectContext);
        SetupSingleExtraction("Project Context", "Uses Clean Architecture", MemoryType.ProjectContext);
        SetupIngestion(MemoryIngestionOutcome.Created);

        await _service.TryIngestAsync(
            "This project uses Clean Architecture.", "user1",
            projectId: projectId);

        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Project &&
                r.ProjectId == projectId),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScopeInference_ExplicitWorkspaceId_UsesWorkspaceScope()
    {
        SetupDetector(true, MemoryType.Fact);
        SetupSingleExtraction("Fact", "Uses Freebuff", MemoryType.Fact);
        SetupIngestion(MemoryIngestionOutcome.Created);

        await _service.TryIngestAsync(
            "I use Freebuff as my coding agent.", "user1",
            workspaceId: "ws-abc");

        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Workspace &&
                r.WorkspaceId == "ws-abc"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Failure Isolation ──

    [Fact]
    public async Task FailureIsolation_ProjectResolutionFails_StillPersistsAsGlobal()
    {
        // Arrange: project service throws
        _mockProjectService.Setup(s => s.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));
        _mockProjectService.Setup(s => s.SearchByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDto>());

        SetupDetector(true, MemoryType.UserPreference);
        SetupSingleExtraction("User Preference", "I prefer PostgreSQL", MemoryType.UserPreference);
        SetupIngestion(MemoryIngestionOutcome.Created);

        // Act: should not throw
        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        // Assert: memory is still persisted (as Global scope)
        Assert.True(result.Detected);
        Assert.True(result.Persisted);
    }

    [Fact]
    public async Task FailureIsolation_DetectorFails_ReturnsFailed()
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), null))
            .Throws(new Exception("Detector unavailable"));

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL.", "user1");

        Assert.False(result.Detected);
        Assert.True(result.Failed);
        Assert.Contains("Detector unavailable", result.FailureReason);
    }

    // ── Multiple Extraction Candidates ──

    [Fact]
    public async Task MultipleCandidates_AllPersisted()
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
                    new MemoryCandidate { Title = "Pref1", Content = "prefer PostgreSQL", MemoryType = MemoryType.UserPreference, Importance = 0.8, Confidence = 0.9, Source = "test", ExtractionReason = "test" },
                    new MemoryCandidate { Title = "Instr1", Content = "use async patterns", MemoryType = MemoryType.Instruction, Importance = 0.9, Confidence = 0.85, Source = "test", ExtractionReason = "test" },
                    new MemoryCandidate { Title = "Fact1", Content = "uses .NET 10", MemoryType = MemoryType.Fact, Importance = 0.7, Confidence = 0.8, Source = "test", ExtractionReason = "test" }
                ],
                FinalCount = 3,
                StrategyUsed = "deterministic"
            });
        SetupIngestion(MemoryIngestionOutcome.Created);

        var result = await _service.TryIngestAsync(
            "I prefer PostgreSQL, I always use async patterns, and we use .NET 10.", "user1");

        Assert.True(result.Persisted);
        Assert.Equal(3, result.CreatedCount);
    }

    // ── Project Name Resolution: Partial Match ──

    [Fact]
    public async Task ProjectResolution_PartialMatch_FindsExistingProject()
    {
        // Arrange: user says "Memory" but the real project name is "DeveloperMemory.Api"
        var project = new ProjectDto { Id = Guid.NewGuid(), Name = "DeveloperMemory.Api" };
        _mockProjectService.Setup(s => s.GetByNameAsync("Memory", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectDto?)null);
        _mockProjectService.Setup(s => s.SearchByNameAsync("Memory", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectDto> { project });

        SetupDetector(true, MemoryType.ProjectContext);
        SetupSingleExtraction("Project Context", "Uses PostgreSQL", MemoryType.ProjectContext);
        SetupIngestion(MemoryIngestionOutcome.Created);

        // Act: "Memory uses PostgreSQL" — triggers pattern match for "X uses ..."
        var result = await _service.TryIngestAsync(
            "Memory uses PostgreSQL.", "user1");

        Assert.True(result.Detected);
        Assert.True(result.Persisted);

        // Should resolve to the existing project via partial match
        _mockIngestionService.Verify(i => i.IngestAsync(
            It.Is<MemoryIngestionRequest>(r =>
                r.Scope == MemoryScope.Project &&
                r.ProjectId == project.Id),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helper Methods ──

    private void SetupDetector(bool detected, MemoryType suggestedType)
    {
        _mockDetector.Setup(d => d.Detect(It.IsAny<string>(), It.IsAny<List<string>?>()))
            .Returns(new ConversationalMemoryDetectionResult
            {
                ContainsDurableInformation = detected,
                Confidence = 0.8,
                Reason = "Test detection",
                SuggestedMemoryType = suggestedType.ToString(),
                ExtractedContent = detected ? "extracted content" : null
            });
    }

    private void SetupSingleExtraction(string title, string content, MemoryType type)
    {
        _mockExtractionOrchestrator.Setup(e => e.ExtractAsync(
                It.IsAny<MemoryExtractionRequest>(),
                It.IsAny<ExtractionMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionOrchestrationResult
            {
                Candidates =
                [
                    new MemoryCandidate
                    {
                        Title = title,
                        Content = content,
                        MemoryType = type,
                        Importance = 0.7,
                        Confidence = 0.8,
                        Source = "test",
                        ExtractionReason = "Phase M test"
                    }
                ],
                FinalCount = 1,
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
                Memory = outcome is MemoryIngestionOutcome.Created or MemoryIngestionOutcome.SupersededExisting
                    ? new MemoryEntry { Id = Guid.NewGuid() }
                    : null,
                RelatedMemory = outcome == MemoryIngestionOutcome.SupersededExisting
                    ? new MemoryEntry { Id = Guid.NewGuid() }
                    : null,
                Reason = outcome.ToString()
            });
    }
}
