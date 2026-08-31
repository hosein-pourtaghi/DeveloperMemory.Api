using Xunit;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// In-memory implementation of IMemoryRetriever for testing consumers.
/// Demonstrates that consumers can depend on the abstraction.
/// </summary>
public class InMemoryMemoryRetriever : IMemoryRetriever
{
    public List<MemoryDto> MemoriesToReturn { get; set; } = [];
    public List<SearchResult> KnowledgeToReturn { get; set; } = [];
    public List<string> ReceivedQueries { get; } = [];
    public List<string?> ReceivedProjects { get; } = [];
    public List<List<string>?> ReceivedTags { get; } = [];

    public Task<MemoryRetrievalResult> RetrieveContextAsync(
        string query,
        string? project = null,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        ReceivedQueries.Add(query);
        ReceivedProjects.Add(project);
        ReceivedTags.Add(tags);

        return Task.FromResult(new MemoryRetrievalResult
        {
            Memories = MemoriesToReturn.ToList(),
            KnowledgeResults = KnowledgeToReturn.ToList()
        });
    }
}

/// <summary>
/// Behavioral tests verifying the IMemoryRetriever abstraction works correctly
/// through an in-memory implementation.
/// </summary>
public class IMemoryRetrieverTests
{
    [Fact]
    public async Task RetrieveContextAsync_ReturnsCombinedResults()
    {
        var retriever = new InMemoryMemoryRetriever
        {
            MemoriesToReturn =
            [
                new MemoryDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Memory",
                    Content = "Test content",
                    Scope = MemoryScope.Global,
                    State = MemoryState.Active,
                    Classification = DataClassification.Internal,
                    Tags = ["test"],
                    Importance = 0.8,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            ],
            KnowledgeToReturn =
            [
                new SearchResult
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Knowledge",
                    Content = "Knowledge content",
                    Score = 0.9
                }
            ]
        };

        var result = await retriever.RetrieveContextAsync("test query");

        Assert.Single(result.Memories);
        Assert.Equal("Test Memory", result.Memories[0].Title);
        Assert.Single(result.KnowledgeResults);
        Assert.Equal("Test Knowledge", result.KnowledgeResults[0].Title);
    }

    [Fact]
    public async Task RetrieveContextAsync_PassesParametersCorrectly()
    {
        var retriever = new InMemoryMemoryRetriever();
        var tags = new List<string> { "tag1", "tag2" };

        await retriever.RetrieveContextAsync("my query", "my-project", tags);

        Assert.Single(retriever.ReceivedQueries);
        Assert.Equal("my query", retriever.ReceivedQueries[0]);
        Assert.Equal("my-project", retriever.ReceivedProjects[0]);
        Assert.Equal(tags, retriever.ReceivedTags[0]);
    }

    [Fact]
    public async Task RetrieveContextAsync_ReturnsEmptyResult_WhenNoMatches()
    {
        var retriever = new InMemoryMemoryRetriever
        {
            MemoriesToReturn = [],
            KnowledgeToReturn = []
        };

        var result = await retriever.RetrieveContextAsync("nonexistent");

        Assert.Empty(result.Memories);
        Assert.Empty(result.KnowledgeResults);
    }

    [Fact]
    public async Task RetrieveContextAsync_DefaultParametersWork()
    {
        var retriever = new InMemoryMemoryRetriever();

        var result = await retriever.RetrieveContextAsync("query");

        Assert.NotNull(result);
        Assert.Empty(result.Memories);
        Assert.Empty(result.KnowledgeResults);
        Assert.Equal("query", retriever.ReceivedQueries[0]);
        Assert.Null(retriever.ReceivedProjects[0]);
        Assert.Null(retriever.ReceivedTags[0]);
    }

    [Fact]
    public async Task RetrieveContextAsync_SupportsCancellation()
    {
        var retriever = new InMemoryMemoryRetriever
        {
            MemoriesToReturn = [new MemoryDto
            {
                Id = Guid.NewGuid(),
                Title = "Memory",
                Content = "Content",
                Scope = MemoryScope.Global,
                State = MemoryState.Active,
                Classification = DataClassification.Internal,
                Tags = [],
                Importance = 0.5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }]
        };

        using var cts = new CancellationTokenSource();
        var result = await retriever.RetrieveContextAsync("query", ct: cts.Token);

        Assert.Single(result.Memories);
    }

    [Fact]
    public async Task RetrieveContextAsync_ReturnsMultipleMemoriesAndKnowledge()
    {
        var retriever = new InMemoryMemoryRetriever
        {
            MemoriesToReturn =
            [
                new MemoryDto
                {
                    Id = Guid.NewGuid(), Title = "M1", Content = "C1",
                    Scope = MemoryScope.Global, State = MemoryState.Active,
                    Classification = DataClassification.Internal, Tags = [],
                    Importance = 0.9, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                },
                new MemoryDto
                {
                    Id = Guid.NewGuid(), Title = "M2", Content = "C2",
                    Scope = MemoryScope.Project, State = MemoryState.Active,
                    Classification = DataClassification.Internal, Tags = [],
                    Importance = 0.5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                },
                new MemoryDto
                {
                    Id = Guid.NewGuid(), Title = "M3", Content = "C3",
                    Scope = MemoryScope.Private, State = MemoryState.Active,
                    Classification = DataClassification.Internal, Tags = [],
                    Importance = 0.3, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            ],
            KnowledgeToReturn =
            [
                new SearchResult { Id = Guid.NewGuid(), Title = "K1", Content = "KC1", Score = 0.8 },
                new SearchResult { Id = Guid.NewGuid(), Title = "K2", Content = "KC2", Score = 0.6 }
            ]
        };

        var result = await retriever.RetrieveContextAsync("query");

        Assert.Equal(3, result.Memories.Count);
        Assert.Equal(2, result.KnowledgeResults.Count);
    }

    [Fact]
    public async Task RetrieveContextAsync_DoesNotThrowOnCancellation()
    {
        var retriever = new InMemoryMemoryRetriever();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // InMemoryMemoryRetriever doesn't check cancellation (by design for testing)
        var result = await retriever.RetrieveContextAsync("query", ct: cts.Token);
        Assert.NotNull(result);
    }
}

/// <summary>
/// Contract tests verifying IMemoryRetriever interface structure and that
/// ContextRetrievalService correctly implements the interface.
/// </summary>
public class IMemoryRetrieverContractTests
{
    [Fact]
    public void IMemoryRetriever_HasRetrieveContextAsyncMethod()
    {
        var interfaceType = typeof(IMemoryRetriever);
        var method = interfaceType.GetMethod(nameof(IMemoryRetriever.RetrieveContextAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<MemoryRetrievalResult>), method!.ReturnType);
    }

    [Fact]
    public void IMemoryRetriever_DoesNotExposePersistenceTypes()
    {
        var interfaceType = typeof(IMemoryRetriever);

        foreach (var method in interfaceType.GetMethods())
        {
            foreach (var param in method.GetParameters())
            {
                Assert.False(param.ParameterType.Name.Contains("DbContext"),
                    $"IMemoryRetriever parameter {param.Name} should not expose DbContext");
                Assert.False(param.ParameterType.Name.Contains("Npgsql"),
                    $"IMemoryRetriever parameter {param.Name} should not expose Npgsql types");
                Assert.False(param.ParameterType.Name.Contains("EntityFramework"),
                    $"IMemoryRetriever parameter {param.Name} should not expose EF Core types");
            }

            // Check return type doesn't leak persistence
            Assert.False(method.ReturnType.Name.Contains("DbContext"),
                $"IMemoryRetriever return type should not be DbContext");
        }
    }

    [Fact]
    public void ContextRetrievalService_ImplementsIMemoryRetriever()
    {
        var serviceType = typeof(Services.ContextRetrievalService);
        var interfaceType = typeof(IMemoryRetriever);

        Assert.True(interfaceType.IsAssignableFrom(serviceType),
            "ContextRetrievalService should implement IMemoryRetriever");
    }

    [Fact]
    public void MemoryRetrievalResult_HasExpectedProperties()
    {
        var resultType = typeof(MemoryRetrievalResult);

        var memories = resultType.GetProperty(nameof(MemoryRetrievalResult.Memories));
        var knowledge = resultType.GetProperty(nameof(MemoryRetrievalResult.KnowledgeResults));

        Assert.NotNull(memories);
        Assert.NotNull(knowledge);
        Assert.Equal(typeof(List<MemoryDto>), memories!.PropertyType);
        Assert.Equal(typeof(List<SearchResult>), knowledge!.PropertyType);
    }

    [Fact]
    public void MemoryRetrievalResult_DefaultValuesAreEmpty()
    {
        var result = new MemoryRetrievalResult();

        Assert.NotNull(result.Memories);
        Assert.Empty(result.Memories);
        Assert.NotNull(result.KnowledgeResults);
        Assert.Empty(result.KnowledgeResults);
    }

    [Fact]
    public void InMemoryMemoryRetriever_ImplementsIMemoryRetriever()
    {
        var type = typeof(InMemoryMemoryRetriever);
        var interfaceType = typeof(IMemoryRetriever);

        Assert.True(interfaceType.IsAssignableFrom(type),
            "InMemoryMemoryRetriever should implement IMemoryRetriever");
    }
}
