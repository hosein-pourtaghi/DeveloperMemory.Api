using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Tests;

public class MemoryIngestionServiceTests
{
    private readonly Mock<IMemoryRepository> _mockRepository;
    private readonly Mock<IMemoryConflictDetector> _mockConflictDetector;
    private readonly Mock<ILogger<MemoryIngestionService>> _mockLogger;
    private readonly MemoryIngestionService _service;

    public MemoryIngestionServiceTests()
    {
        _mockRepository = new Mock<IMemoryRepository>();
        _mockConflictDetector = new Mock<IMemoryConflictDetector>();
        _mockLogger = new Mock<ILogger<MemoryIngestionService>>();

        _service = new MemoryIngestionService(
            _mockRepository.Object,
            _mockConflictDetector.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task IngestAsync_EmptyContent_ReturnsRejected()
    {
        var request = new MemoryIngestionRequest { Content = "" };

        var result = await _service.IngestAsync(request);

        Assert.Equal(MemoryIngestionOutcome.Rejected, result.Outcome);
        Assert.Contains("required", result.Reason);
    }

    [Fact]
    public async Task IngestAsync_ContentTooLong_ReturnsRejected()
    {
        var request = new MemoryIngestionRequest
        {
            Content = new string('x', 10001)
        };

        var result = await _service.IngestAsync(request);

        Assert.Equal(MemoryIngestionOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task IngestAsync_NoConflicts_CreatesMemory()
    {
        _mockRepository.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        _mockRepository.Setup(r => r.CreateAsync(
            It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken ct) => e);

        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(), It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns([]);

        var request = new MemoryIngestionRequest
        {
            Content = "Use PostgreSQL",
            Title = "DB Choice",
            MemoryType = MemoryType.TechnicalDecision
        };

        var result = await _service.IngestAsync(request);

        Assert.Equal(MemoryIngestionOutcome.Created, result.Outcome);
        Assert.True(result.WasPersisted);
    }

    [Fact]
    public async Task IngestAsync_ExactDuplicate_ReturnsIgnoredDuplicate()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        _mockRepository.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { existing });

        var request = new MemoryIngestionRequest
        {
            Content = "Use PostgreSQL",
            Title = "DB Choice",
            Scope = MemoryScope.Project,
            MemoryType = MemoryType.TechnicalDecision
        };

        var result = await _service.IngestAsync(request);

        Assert.Equal(MemoryIngestionOutcome.IgnoredDuplicate, result.Outcome);
        Assert.True(result.DuplicateDetected);
    }

    [Fact]
    public async Task IngestAsync_NormalizedDuplicate_ReturnsIgnoredDuplicate()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use PostgreSQL!",
            NormalizedContent = "use postgresql",
            Scope = MemoryScope.Project,
            State = MemoryState.Active
        };

        _mockRepository.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { existing });

        var request = new MemoryIngestionRequest
        {
            Content = "Use PostgreSQL.",
            Title = "DB Choice",
            Scope = MemoryScope.Project,
            MemoryType = MemoryType.TechnicalDecision
        };

        var result = await _service.IngestAsync(request);

        Assert.Equal(MemoryIngestionOutcome.IgnoredDuplicate, result.Outcome);
        Assert.True(result.DuplicateDetected);
    }

    [Fact]
    public async Task IngestAsync_ConflictDetected_ReturnsRequiresReview()
    {
        var existing = new MemoryEntry
        {
            Id = Guid.NewGuid(),
            Content = "Use Angular",
            Scope = MemoryScope.Project,
            State = MemoryState.Active,
            MemoryType = MemoryType.TechnicalDecision
        };

        _mockRepository.Setup(r => r.SearchAsync(
            It.IsAny<string>(), It.IsAny<MemoryScope?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry> { existing });

        _mockConflictDetector.Setup(d => d.DetectConflicts(
            It.IsAny<MemoryEntry>(), It.IsAny<IReadOnlyList<MemoryEntry>>()))
            .Returns(new List<MemoryConflict>
            {
                new MemoryConflict
                {
                    ExistingMemory = existing,
                    ConflictType = MemoryConflictType.Contradiction,
                    Explanation = "Potential contradiction",
                    ShouldSupersede = false,
                    Confidence = 0.6
                }
            });

        var request = new MemoryIngestionRequest
        {
            Content = "Use React",
            Title = "Frontend",
            Scope = MemoryScope.Project,
            MemoryType = MemoryType.TechnicalDecision
        };

        var result = await _service.IngestAsync(request);

        Assert.Equal(MemoryIngestionOutcome.RequiresReview, result.Outcome);
        Assert.True(result.ConflictDetected);
    }
}
