using Xunit;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using System.Net;
using System.IO;
using System.Text;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// In-memory implementation of IModelGateway for testing.
/// Demonstrates that consumers can depend on the abstraction.
/// </summary>
public class InMemoryModelGateway : IModelGateway
{
    public bool Configured { get; set; } = true;
    public bool IsConfigured => Configured;

    public string DefaultModel { get; set; } = "test-model";
    public List<string> AvailableModels { get; set; } = ["model-a", "model-b"];
    public List<OpenAIChatCompletionRequest> ReceivedRequests { get; } = [];

    public string ResolveModel(string? requestModel)
    {
        if (!string.IsNullOrWhiteSpace(requestModel))
            return requestModel;

        if (!string.IsNullOrWhiteSpace(DefaultModel))
            return DefaultModel;

        return "auto";
    }

    public Task<OpenAIChatCompletionResponse> SendCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        var response = new OpenAIChatCompletionResponse
        {
            Id = "test-response-id",
            Object = "chat.completion",
            Model = request.Model ?? DefaultModel,
            Choices =
            [
                new Choice
                {
                    Index = 0,
                    Message = new Message { Role = "assistant", Content = "Test response" },
                    FinishReason = "stop"
                }
            ],
            Usage = new Usage { PromptTokens = 10, CompletionTokens = 5, TotalTokens = 15 }
        };

        return Task.FromResult(response);
    }

    public Task<Stream> SendStreamingCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        var sseContent = "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\ndata: [DONE]\n\n";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseContent));

        return Task.FromResult<Stream>(stream);
    }

    public Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AvailableModels.ToList());
    }

    public Task<OpenAIModel?> GetModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (AvailableModels.Contains(modelId))
        {
            return Task.FromResult<OpenAIModel?>(new OpenAIModel
            {
                Id = modelId,
                Object = "model",
                OwnedBy = "test-provider"
            });
        }

        return Task.FromResult<OpenAIModel?>(null);
    }
}

public class IModelGatewayTests
{
    [Fact]
    public async Task SendCompletionAsync_ReturnsResponse()
    {
        var gateway = new InMemoryModelGateway();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        var response = await gateway.SendCompletionAsync(request);

        Assert.NotNull(response);
        Assert.Equal("test-model", response.Model);
        Assert.Single(response.Choices);
        Assert.Equal("Test response", response.Choices[0].Message.Content);
    }

    [Fact]
    public async Task SendCompletionAsync_RecordsRequest()
    {
        var gateway = new InMemoryModelGateway();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        await gateway.SendCompletionAsync(request);

        Assert.Single(gateway.ReceivedRequests);
        Assert.Equal("test-model", gateway.ReceivedRequests[0].Model);
    }

    [Fact]
    public async Task SendStreamingCompletionAsync_ReturnsReadableStream()
    {
        var gateway = new InMemoryModelGateway();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Stream = true,
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        await using var stream = await gateway.SendStreamingCompletionAsync(request);

        Assert.NotNull(stream);
        Assert.True(stream.CanRead);

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("[DONE]", content);
    }

    [Fact]
    public async Task SendStreamingCompletionAsync_RecordsRequest()
    {
        var gateway = new InMemoryModelGateway();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Stream = true,
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        await using var stream = await gateway.SendStreamingCompletionAsync(request);

        Assert.Single(gateway.ReceivedRequests);
        Assert.Equal("test-model", gateway.ReceivedRequests[0].Model);
    }

    [Fact]
    public void ResolveModel_UsesProvidedModel()
    {
        var gateway = new InMemoryModelGateway();

        var resolved = gateway.ResolveModel("specific-model");

        Assert.Equal("specific-model", resolved);
    }

    [Fact]
    public void ResolveModel_FallsBackToDefault()
    {
        var gateway = new InMemoryModelGateway();

        var resolved = gateway.ResolveModel(null);

        Assert.Equal("test-model", resolved);
    }

    [Fact]
    public void ResolveModel_FallsBackToAuto()
    {
        var gateway = new InMemoryModelGateway { DefaultModel = string.Empty };

        var resolved = gateway.ResolveModel(null);

        Assert.Equal("auto", resolved);
    }

    [Fact]
    public void IsConfigured_ReflectsConfiguration()
    {
        var gateway = new InMemoryModelGateway { Configured = true };
        Assert.True(gateway.IsConfigured);

        gateway.Configured = false;
        Assert.False(gateway.IsConfigured);
    }

    [Fact]
    public async Task GetModelsAsync_ReturnsAvailableModels()
    {
        var gateway = new InMemoryModelGateway();

        var models = await gateway.GetModelsAsync();

        Assert.Equal(2, models.Count);
        Assert.Contains("model-a", models);
        Assert.Contains("model-b", models);
    }

    [Fact]
    public async Task GetModelAsync_ReturnsModel_WhenExists()
    {
        var gateway = new InMemoryModelGateway();

        var model = await gateway.GetModelAsync("model-a");

        Assert.NotNull(model);
        Assert.Equal("model-a", model!.Id);
    }

    [Fact]
    public async Task GetModelAsync_ReturnsNull_WhenNotFound()
    {
        var gateway = new InMemoryModelGateway();

        var model = await gateway.GetModelAsync("nonexistent");

        Assert.Null(model);
    }
}

/// <summary>
/// Tests verifying that the FreeLlmApiClient correctly implements IModelGateway contract.
/// These are static validation tests — they verify interface compliance without requiring
/// an actual HTTP connection.
/// </summary>
public class FreeLlmApiClientContractTests
{
    [Fact]
    public void FreeLlmApiClient_ImplementsIModelGateway()
    {
        var gatewayType = typeof(Services.FreeLlmApiClient);
        var interfaceType = typeof(IModelGateway);

        Assert.True(interfaceType.IsAssignableFrom(gatewayType),
            $"FreeLlmApiClient should implement IModelGateway");
    }

    [Fact]
    public void IModelGateway_Interface_HasExpectedMethods()
    {
        var interfaceType = typeof(IModelGateway);

        var sendCompletion = interfaceType.GetMethod(nameof(IModelGateway.SendCompletionAsync));
        var sendStreaming = interfaceType.GetMethod(nameof(IModelGateway.SendStreamingCompletionAsync));
        var getModels = interfaceType.GetMethod(nameof(IModelGateway.GetModelsAsync));
        var getModel = interfaceType.GetMethod(nameof(IModelGateway.GetModelAsync));
        var resolveModel = interfaceType.GetMethod(nameof(IModelGateway.ResolveModel));

        Assert.NotNull(sendCompletion);
        Assert.NotNull(sendStreaming);
        Assert.NotNull(getModels);
        Assert.NotNull(getModel);
        Assert.NotNull(resolveModel);
    }

    [Fact]
    public void IModelGateway_Interface_HasIsConfiguredProperty()
    {
        var interfaceType = typeof(IModelGateway);
        var property = interfaceType.GetProperty(nameof(IModelGateway.IsConfigured));

        Assert.NotNull(property);
        Assert.Equal(typeof(bool), property.PropertyType);
        Assert.True(property.CanRead);
    }

    [Fact]
    public void IModelGateway_SendStreaming_ReturnsStream_NotHttpResponseMessage()
    {
        var interfaceType = typeof(IModelGateway);
        var method = interfaceType.GetMethod(nameof(IModelGateway.SendStreamingCompletionAsync));

        Assert.NotNull(method);
        var returnType = method!.ReturnType;

        // The return type should be Task<Stream>, not Task<HttpResponseMessage>
        Assert.Equal(typeof(Task<Stream>), returnType);
        Assert.NotEqual(typeof(Task<HttpResponseMessage>), returnType);
    }

    [Fact]
    public void DownstreamProviderException_IsInAbstractionsNamespace()
    {
        var exceptionType = typeof(DownstreamProviderException);
        Assert.Equal("DeveloperMemory.Api.Abstractions", exceptionType.Namespace);
    }

    [Fact]
    public void DownstreamProviderException_CarriesStatusCodeAndContent()
    {
        var ex = new DownstreamProviderException(HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}");

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal("{\"error\":\"rate limited\"}", ex.RawErrorContent);
        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public void IModelGateway_DoesNotExposeHttpResponseMessage()
    {
        // Verify that IModelGateway's public interface does not expose HttpResponseMessage
        // in any of its method signatures or properties
        var interfaceType = typeof(IModelGateway);

        foreach (var method in interfaceType.GetMethods())
        {
            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                Assert.False(UnwrapTaskType(param.ParameterType) == typeof(HttpResponseMessage),
                    $"IModelGateway method {method.Name} parameter {param.Name} should not use HttpResponseMessage");
            }

            Assert.False(UnwrapTaskType(method.ReturnType) == typeof(HttpResponseMessage),
                $"IModelGateway method {method.Name} should not return HttpResponseMessage");
        }

        foreach (var prop in interfaceType.GetProperties())
        {
            Assert.False(UnwrapTaskType(prop.PropertyType) == typeof(HttpResponseMessage),
                $"IModelGateway property {prop.Name} should not be HttpResponseMessage");
        }
    }

    private static Type UnwrapTaskType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)
            ? type.GetGenericArguments()[0]
            : type;
}
