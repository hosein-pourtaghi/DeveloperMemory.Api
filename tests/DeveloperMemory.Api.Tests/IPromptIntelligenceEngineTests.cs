using Xunit;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// In-memory implementation of IPromptIntelligenceEngine for testing consumers.
/// Demonstrates that consumers can depend on the abstraction.
/// </summary>
public class InMemoryPromptIntelligenceEngine : IPromptIntelligenceEngine
{
    public OpenAIChatCompletionRequest? LastRequest { get; private set; }
    public List<CancellationToken> ReceivedTokens { get; } = [];
    public PromptIntelligenceResult ResultToReturn { get; set; } = new()
    {
        EnrichedRequest = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "system", Content = "You are a helpful assistant." }]
        },
        SearchQuery = "test query"
    };

    public Task<PromptIntelligenceResult> PreparePromptAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken ct = default)
    {
        LastRequest = request;
        ReceivedTokens.Add(ct);
        return Task.FromResult(ResultToReturn);
    }
}

/// <summary>
/// Behavioral tests verifying the IPromptIntelligenceEngine abstraction
/// works correctly through an in-memory implementation.
/// </summary>
public class IPromptIntelligenceEngineTests
{
    [Fact]
    public async Task PreparePromptAsync_ReturnsEnrichedRequest()
    {
        var engine = new InMemoryPromptIntelligenceEngine();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        var result = await engine.PreparePromptAsync(request);

        Assert.NotNull(result);
        Assert.NotNull(result.EnrichedRequest);
        Assert.Equal("test query", result.SearchQuery);
    }

    [Fact]
    public async Task PreparePromptAsync_RecordsRequest()
    {
        var engine = new InMemoryPromptIntelligenceEngine();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        await engine.PreparePromptAsync(request);

        Assert.Same(request, engine.LastRequest);
    }

    [Fact]
    public async Task PreparePromptAsync_PassesCancellationToken()
    {
        var engine = new InMemoryPromptIntelligenceEngine();

        using var cts = new CancellationTokenSource();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        await engine.PreparePromptAsync(request, cts.Token);

        Assert.Single(engine.ReceivedTokens);
        Assert.Equal(cts.Token, engine.ReceivedTokens[0]);
    }

    [Fact]
    public async Task PreparePromptAsync_CanReturnCustomResult()
    {
        var customResult = new PromptIntelligenceResult
        {
            EnrichedRequest = new OpenAIChatCompletionRequest
            {
                Model = "custom-model",
                Messages =
                [
                    new Message { Role = "system", Content = "Custom context" },
                    new Message { Role = "user", Content = "Hello" }
                ]
            },
            SearchQuery = "custom query"
        };

        var engine = new InMemoryPromptIntelligenceEngine
        {
            ResultToReturn = customResult
        };

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        var result = await engine.PreparePromptAsync(request);

        Assert.Equal("custom-model", result.EnrichedRequest.Model);
        Assert.Equal(2, result.EnrichedRequest.Messages.Count);
        Assert.Equal("Custom context", result.EnrichedRequest.Messages[0].Content);
        Assert.Equal("custom query", result.SearchQuery);
    }
}

/// <summary>
/// Contract tests verifying IPromptIntelligenceEngine interface structure
/// and that PromptIntelligenceService correctly implements the interface.
/// </summary>
public class IPromptIntelligenceEngineContractTests
{
    [Fact]
    public void IPromptIntelligenceEngine_HasPreparePromptAsyncMethod()
    {
        var interfaceType = typeof(IPromptIntelligenceEngine);
        var method = interfaceType.GetMethod(nameof(IPromptIntelligenceEngine.PreparePromptAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PromptIntelligenceResult>), method!.ReturnType);
    }

    [Fact]
    public void IPromptIntelligenceEngine_DoesNotExposeInfrastructureTypes()
    {
        var interfaceType = typeof(IPromptIntelligenceEngine);

        foreach (var method in interfaceType.GetMethods())
        {
            foreach (var param in method.GetParameters())
            {
                Assert.False(param.ParameterType.Name.Contains("DbContext"),
                    $"IPromptIntelligenceEngine parameter {param.Name} should not expose DbContext");
                Assert.False(param.ParameterType.Name.Contains("HttpClient"),
                    $"IPromptIntelligenceEngine parameter {param.Name} should not expose HttpClient");
                Assert.False(param.ParameterType.Name.Contains("HttpResponseMessage"),
                    $"IPromptIntelligenceEngine parameter {param.Name} should not expose HttpResponseMessage");
                Assert.False(param.ParameterType.Name.Contains("Npgsql"),
                    $"IPromptIntelligenceEngine parameter {param.Name} should not expose Npgsql types");
            }
        }
    }

    [Fact]
    public void PromptIntelligenceService_ImplementsIPromptIntelligenceEngine()
    {
        var serviceType = typeof(Services.PromptIntelligenceService);
        var interfaceType = typeof(IPromptIntelligenceEngine);

        Assert.True(interfaceType.IsAssignableFrom(serviceType),
            "PromptIntelligenceService should implement IPromptIntelligenceEngine");
    }

    [Fact]
    public void PromptIntelligenceResult_HasExpectedProperties()
    {
        var resultType = typeof(PromptIntelligenceResult);

        var enrichedRequest = resultType.GetProperty(nameof(PromptIntelligenceResult.EnrichedRequest));
        var searchQuery = resultType.GetProperty(nameof(PromptIntelligenceResult.SearchQuery));

        Assert.NotNull(enrichedRequest);
        Assert.NotNull(searchQuery);
        Assert.Equal(typeof(OpenAIChatCompletionRequest), enrichedRequest!.PropertyType);
        Assert.Equal(typeof(string), searchQuery!.PropertyType);
    }

    [Fact]
    public void PromptIntelligenceResult_SearchQuery_DefaultsToEmpty()
    {
        var result = new PromptIntelligenceResult();

        Assert.NotNull(result.EnrichedRequest);
        Assert.Equal(string.Empty, result.SearchQuery);
    }

    [Fact]
    public void InMemoryPromptIntelligenceEngine_ImplementsIPromptIntelligenceEngine()
    {
        var type = typeof(InMemoryPromptIntelligenceEngine);
        var interfaceType = typeof(IPromptIntelligenceEngine);

        Assert.True(interfaceType.IsAssignableFrom(type),
            "InMemoryPromptIntelligenceEngine should implement IPromptIntelligenceEngine");
    }
}
