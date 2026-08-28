using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

public class EmbeddingCacheTests
{
    [Fact]
    public async Task GetAsync_EmptyCache_ReturnsNull()
    {
        var cache = new InMemoryEmbeddingCache();
        var result = await cache.GetAsync("provider", "model", null, "hash123");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsEmbedding()
    {
        var cache = new InMemoryEmbeddingCache();
        var embedding = new Embedding
        {
            Values = [0.1f, 0.2f, 0.3f],
            Provider = "test",
            Model = "model"
        };

        await cache.SetAsync("provider", "model", null, "hash123", embedding);
        var result = await cache.GetAsync("provider", "model", null, "hash123");

        Assert.NotNull(result);
        Assert.Equal(embedding.Values, result!.Values);
    }

    [Fact]
    public async Task GetAsync_WrongProfile_ReturnsNull()
    {
        var cache = new InMemoryEmbeddingCache();
        var embedding = new Embedding
        {
            Values = [0.1f, 0.2f, 0.3f],
            Provider = "provider",
            Model = "model-v1"
        };

        await cache.SetAsync("provider", "model-v1", null, "hash123", embedding);
        var result = await cache.GetAsync("provider", "model-v2", null, "hash123");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WrongHash_ReturnsNull()
    {
        var cache = new InMemoryEmbeddingCache();
        var embedding = new Embedding
        {
            Values = [0.1f, 0.2f, 0.3f],
            Provider = "test",
            Model = "model"
        };

        await cache.SetAsync("provider", "model", null, "hash123", embedding);
        var result = await cache.GetAsync("provider", "model", null, "hash456");

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_ExistingEntry_ReturnsTrue()
    {
        var cache = new InMemoryEmbeddingCache();
        var embedding = new Embedding
        {
            Values = [0.1f, 0.2f, 0.3f],
            Provider = "test",
            Model = "model"
        };

        await cache.SetAsync("provider", "model", null, "hash123", embedding);
        var removed = await cache.RemoveAsync("provider", "model", null, "hash123");

        Assert.True(removed);
        var result = await cache.GetAsync("provider", "model", null, "hash123");
        Assert.Null(result);
    }

    [Fact]
    public async Task ClearByProfileAsync_RemovesMatchingEntries()
    {
        var cache = new InMemoryEmbeddingCache();
        var embedding1 = new Embedding { Values = [0.1f], Provider = "test", Model = "model1" };
        var embedding2 = new Embedding { Values = [0.2f], Provider = "test", Model = "model2" };

        await cache.SetAsync("provider", "model1", null, "hash1", embedding1);
        await cache.SetAsync("provider", "model2", null, "hash2", embedding2);

        var removed = await cache.ClearByProfileAsync("provider", "model1");

        Assert.Equal(1, removed);
        Assert.Null(await cache.GetAsync("provider", "model1", null, "hash1"));
        Assert.NotNull(await cache.GetAsync("provider", "model2", null, "hash2"));
    }

    [Fact]
    public void ComputeTextHash_Deterministic()
    {
        var hash1 = IEmbeddingCache.ComputeTextHash("Hello World");
        var hash2 = IEmbeddingCache.ComputeTextHash("Hello World");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeTextHash_DifferentText_DifferentHash()
    {
        var hash1 = IEmbeddingCache.ComputeTextHash("Hello World");
        var hash2 = IEmbeddingCache.ComputeTextHash("Goodbye World");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeTextHash_CaseInsensitive()
    {
        var hash1 = IEmbeddingCache.ComputeTextHash("hello world");
        var hash2 = IEmbeddingCache.ComputeTextHash("HELLO WORLD");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task GetAsync_ExpiredEntry_ReturnsNull()
    {
        var cache = new InMemoryEmbeddingCache(TimeSpan.FromMilliseconds(1));
        var embedding = new Embedding
        {
            Values = [0.1f, 0.2f, 0.3f],
            Provider = "test",
            Model = "model"
        };

        await cache.SetAsync("provider", "model", null, "hash123", embedding);
        await Task.Delay(10); // Wait for expiration
        var result = await cache.GetAsync("provider", "model", null, "hash123");

        Assert.Null(result);
    }
}
