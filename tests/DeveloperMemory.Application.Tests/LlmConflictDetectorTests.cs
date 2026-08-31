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

public class LlmConflictDetectorTests
{
    private readonly Mock<ILogger<MemoryConflictDetector>> _deterministicLoggerMock = new();
    private readonly Mock<ILogger<LlmConflictDetector>> _llmLoggerMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<IOptions<MemoryIntelligenceOptions>> _optionsMock = new();
    private readonly MemoryConflictDetector _deterministicDetector;

    public LlmConflictDetectorTests()
    {
        _deterministicDetector = new MemoryConflictDetector();
        _optionsMock.Setup(o => o.Value).Returns(new MemoryIntelligenceOptions
        {
            Enabled = false
        });
    }

    [Fact]
    public void DetectConflicts_NoExistingMemories_ReturnsEmpty()
    {
        var detector = new LlmConflictDetector(
            _deterministicDetector,
            _httpClientFactoryMock.Object,
            _optionsMock.Object,
            _llmLoggerMock.Object);

        var candidate = new MemoryEntry
        {
            Content = "We use PostgreSQL",
            MemoryType = MemoryType.ProjectContext
        };

        var conflicts = detector.DetectConflicts(candidate, []);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_DuplicateContent_DetectsConflict()
    {
        var detector = new LlmConflictDetector(
            _deterministicDetector,
            _httpClientFactoryMock.Object,
            _optionsMock.Object,
            _llmLoggerMock.Object);

        var existingId = Guid.NewGuid();
        var candidate = new MemoryEntry
        {
            Content = "We use PostgreSQL for the database",
            MemoryType = MemoryType.ProjectContext
        };

        var existingMemories = new List<MemoryEntry>
        {
            new()
            {
                Id = existingId,
                Content = "We use PostgreSQL for the database",
                MemoryType = MemoryType.ProjectContext
            }
        };

        var conflicts = detector.DetectConflicts(candidate, existingMemories);

        Assert.NotEmpty(conflicts);
        Assert.Contains(conflicts, c => c.ExistingMemory.Id == existingId);
    }

    [Fact]
    public void DetectConflicts_DifferentContent_NoConflict()
    {
        var detector = new LlmConflictDetector(
            _deterministicDetector,
            _httpClientFactoryMock.Object,
            _optionsMock.Object,
            _llmLoggerMock.Object);

        var candidate = new MemoryEntry
        {
            Content = "We use PostgreSQL for the database",
            MemoryType = MemoryType.ProjectContext
        };

        var existingMemories = new List<MemoryEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "We prefer React for the frontend",
                MemoryType = MemoryType.ProjectContext
            }
        };

        var conflicts = detector.DetectConflicts(candidate, existingMemories);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_LLMUnavailable_UsesDeterministicOnly()
    {
        // LLM not available (Enabled = false)
        var optionsMock = new Mock<IOptions<MemoryIntelligenceOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new MemoryIntelligenceOptions
        {
            Enabled = false
        });

        var detector = new LlmConflictDetector(
            _deterministicDetector,
            _httpClientFactoryMock.Object,
            optionsMock.Object,
            _llmLoggerMock.Object);

        var candidate = new MemoryEntry
        {
            Content = "We use PostgreSQL for the database",
            MemoryType = MemoryType.ProjectContext
        };

        var existingMemories = new List<MemoryEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Content = "We use PostgreSQL for the database",
                MemoryType = MemoryType.ProjectContext
            }
        };

        var conflicts = detector.DetectConflicts(candidate, existingMemories);

        // Should still detect via deterministic
        Assert.NotEmpty(conflicts);
    }
}
