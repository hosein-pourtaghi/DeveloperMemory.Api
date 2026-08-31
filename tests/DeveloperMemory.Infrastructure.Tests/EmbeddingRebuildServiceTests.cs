using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

public class EmbeddingRebuildServiceTests
{
    private readonly Mock<IEmbeddingProvider> _embeddingProviderMock = new();
    private readonly Mock<IVectorStore> _vectorStoreMock = new();
    private readonly Mock<IEmbeddingCache> _cacheMock = new();
    private readonly Mock<ILogger<EmbeddingRebuildService>> _loggerMock = new();

    [Fact]
    public async Task RebuildAsync_EmptyText_ReturnsFailure()
    {
        var service = new EmbeddingRebuildService(
            _embeddingProviderMock.Object,
            _vectorStoreMock.Object,
            _loggerMock.Object);

        var result = await service.RebuildAsync(Guid.NewGuid(), "");

        Assert.False(result.Success);
        Assert.Contains("required", result.ErrorMessage!);
    }

    [Fact]
    public async Task RebuildAsync_ProviderFails_ReturnsFailure()
    {
        var memoryId = Guid.NewGuid();
        _vectorStoreMock.Setup(v => v.DeleteAsync(memoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _embeddingProviderMock.Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Provider error"
            });

        var service = new EmbeddingRebuildService(
            _embeddingProviderMock.Object,
            _vectorStoreMock.Object,
            _loggerMock.Object);

        var result = await service.RebuildAsync(memoryId, "test text");

        Assert.False(result.Success);
        Assert.Contains("Provider error", result.ErrorMessage!);
    }

    [Fact]
    public async Task RebuildAsync_CacheEnabled_ClearsCacheBeforeRebuild()
    {
        var memoryId = Guid.NewGuid();
        var embedding = new Embedding
        {
            Values = [0.1f, 0.2f, 0.3f],
            Provider = "test",
            Model = "model"
        };

        _embeddingProviderMock.Setup(p => p.Profile).Returns(new EmbeddingProfile
        {
            Provider = "test",
            Model = "model"
        });
        _vectorStoreMock.Setup(v => v.DeleteAsync(memoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _vectorStoreMock.Setup(v => v.UpsertAsync(memoryId, It.IsAny<Embedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _embeddingProviderMock.Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embedding = embedding
            });

        var service = new EmbeddingRebuildService(
            _embeddingProviderMock.Object,
            _vectorStoreMock.Object,
            _loggerMock.Object,
            _cacheMock.Object);

        await service.RebuildAsync(memoryId, "test text");

        _cacheMock.Verify(c => c.RemoveAsync(
            "test", "model", null, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsStats()
    {
        _vectorStoreMock.Setup(v => v.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _embeddingProviderMock.Setup(p => p.IsAvailable).Returns(true);
        _embeddingProviderMock.Setup(p => p.ProviderName).Returns("test");
        _embeddingProviderMock.Setup(p => p.Profile).Returns(new EmbeddingProfile
        {
            Provider = "test",
            Model = "model"
        });

        var service = new EmbeddingRebuildService(
            _embeddingProviderMock.Object,
            _vectorStoreMock.Object,
            _loggerMock.Object);

        var stats = await service.GetStatsAsync();

        Assert.Equal(42, stats.TotalVectors);
        Assert.Equal("test", stats.CurrentProvider);
        Assert.Equal("model", stats.CurrentModel);
        Assert.True(stats.ProviderAvailable);
    }

    [Fact]
    public async Task RebuildBatchAsync_MultipleRequests_ReturnsSuccessCount()
    {
        var requests = new List<EmbeddingRebuildRequest>
        {
            new() { MemoryId = Guid.NewGuid(), Text = "text1" },
            new() { MemoryId = Guid.NewGuid(), Text = "text2" },
            new() { MemoryId = Guid.NewGuid(), Text = "text3" }
        };

        _vectorStoreMock.Setup(v => v.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _vectorStoreMock.Setup(v => v.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Embedding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _embeddingProviderMock.Setup(p => p.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embedding = new Embedding
                {
                    Values = [0.1f, 0.2f, 0.3f],
                    Provider = "test",
                    Model = "model"
                }
            });

        var service = new EmbeddingRebuildService(
            _embeddingProviderMock.Object,
            _vectorStoreMock.Object,
            _loggerMock.Object);

        var successCount = await service.RebuildBatchAsync(requests);

        Assert.Equal(3, successCount);
    }
}
