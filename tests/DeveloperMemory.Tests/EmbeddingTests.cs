using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeveloperMemory.Tests;

public class EmbeddingTests
{
    private readonly InMemoryEmbeddingProvider _provider = new(dimensions: 64);
    private readonly InMemoryVectorStore _store = new();
    private readonly EmbeddingService _service;

    public EmbeddingTests()
    {
        var logger = new LoggerFactory().CreateLogger<EmbeddingService>();
        _service = new EmbeddingService(_provider, _store, logger);
        _store.Clear();
    }

    [Fact]
    public async Task GenerateAsync_ValidText_ReturnsSuccess()
    {
        var result = await _provider.GenerateAsync("Hello world");

        Assert.True(result.Success);
        Assert.NotNull(result.Embedding);
        Assert.Equal(64, result.Embedding.Dimensions);
        Assert.True(result.Embedding.IsValid());
    }

    [Fact]
    public async Task GenerateAsync_EmptyText_ReturnsFailure()
    {
        var result = await _provider.GenerateAsync("");

        Assert.False(result.Success);
        Assert.Contains("required", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateAsync_Deterministic_SameInputSameOutput()
    {
        var result1 = await _provider.GenerateAsync("test input");
        var result2 = await _provider.GenerateAsync("test input");

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(result1.Embedding!.Values, result2.Embedding!.Values);
    }

    [Fact]
    public async Task GenerateAsync_DifferentInput_DifferentOutput()
    {
        var result1 = await _provider.GenerateAsync("input one");
        var result2 = await _provider.GenerateAsync("input two");

        Assert.NotEqual(result1.Embedding!.Values, result2.Embedding!.Values);
    }

    [Fact]
    public async Task GenerateBatchAsync_MultipleTexts_ReturnsAll()
    {
        var texts = new[] { "text one", "text two", "text three" };

        var results = await _provider.GenerateBatchAsync(texts);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public void IsAvailable_AlwaysTrue_ForInMemoryProvider()
    {
        Assert.True(_provider.IsAvailable);
    }

    [Fact]
    public void Profile_HasCorrectDimensions()
    {
        Assert.Equal(64, _provider.Profile.Dimensions);
    }

    [Fact]
    public async Task VectorStore_UpsertAndSearch_FindsSimilar()
    {
        var embedding1 = (await _provider.GenerateAsync("database choice")).Embedding!;
        var embedding2 = (await _provider.GenerateAsync("frontend framework")).Embedding!;

        await _store.UpsertAsync(Guid.NewGuid(), embedding1);
        await _store.UpsertAsync(Guid.NewGuid(), embedding2);

        var results = await _store.SearchAsync(embedding1.Values, 10);

        Assert.NotEmpty(results);
        Assert.True(results[0].SimilarityScore > 0.9); // Same vector should match perfectly
    }

    [Fact]
    public async Task VectorStore_Delete_RemovesVector()
    {
        var id = Guid.NewGuid();
        var embedding = (await _provider.GenerateAsync("test")).Embedding!;

        await _store.UpsertAsync(id, embedding);
        var deleted = await _store.DeleteAsync(id);

        Assert.True(deleted);
        var stored = await _store.GetAsync(id);
        Assert.Null(stored);
    }

    [Fact]
    public async Task VectorStore_Count_ReturnsCorrectCount()
    {
        var count0 = await _store.CountAsync();
        Assert.Equal(0, count0);

        await _store.UpsertAsync(Guid.NewGuid(), (await _provider.GenerateAsync("a")).Embedding!);
        await _store.UpsertAsync(Guid.NewGuid(), (await _provider.GenerateAsync("b")).Embedding!);

        var count2 = await _store.CountAsync();
        Assert.Equal(2, count2);
    }

    [Fact]
    public async Task EmbeddingService_GenerateAndStore_PersistsVector()
    {
        var id = Guid.NewGuid();
        var result = await _service.GenerateAndStoreAsync(id, "test content");

        Assert.True(result.Success);

        var stored = await _service.GetAsync(id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task EmbeddingService_Delete_RemovesVector()
    {
        var id = Guid.NewGuid();
        await _service.GenerateAndStoreAsync(id, "test content");

        var deleted = await _service.DeleteAsync(id);
        Assert.True(deleted);

        var stored = await _service.GetAsync(id);
        Assert.Null(stored);
    }

    [Fact]
    public async Task EmbeddingService_Rebuild_ReplacesVector()
    {
        var id = Guid.NewGuid();
        await _service.GenerateAndStoreAsync(id, "original text");

        var rebuildResult = await _service.RebuildAsync(id, "rebuilt text");
        Assert.True(rebuildResult.Success);

        var stored = await _service.GetAsync(id);
        Assert.NotNull(stored);
    }

    [Fact]
    public void IsSemanticAvailable_WhenProviderAndStoreAvailable_ReturnsTrue()
    {
        Assert.True(_service.IsSemanticAvailable);
    }
}
