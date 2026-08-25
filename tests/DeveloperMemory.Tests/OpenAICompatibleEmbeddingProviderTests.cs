using DeveloperMemory.Infrastructure.Configuration;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DeveloperMemory.Tests;

public class OpenAICompatibleEmbeddingProviderTests
{
    private readonly Mock<IOptions<EmbeddingOptions>> _optionsMock = new();
    private readonly Mock<ILogger<OpenAICompatibleEmbeddingProvider>> _loggerMock = new();

    private EmbeddingOptions CreateOptions(
        bool enabled = true,
        string baseUrl = "https://api.example.com/v1",
        string model = "test-model",
        string apiKey = "test-key",
        int dimensions = 3)
    {
        return new EmbeddingOptions
        {
            Enabled = enabled,
            Provider = "test",
            BaseUrl = baseUrl,
            Model = model,
            ApiKey = apiKey,
            Dimensions = dimensions,
            TimeoutSeconds = 30
        };
    }

    private HttpClient CreateMockHttpClient(float[] embeddingValues)
    {
        var handler = new Mock<HttpMessageHandler>();
        var response = new EmbeddingApiResponse
        {
            Data =
            [
                new EmbeddingApiData
                {
                    Embedding = embeddingValues,
                    Index = 0
                }
            ],
            Model = "test-model"
        };

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(response, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })
            });

        return new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.example.com/v1")
        };
    }

    [Fact]
    public async Task GenerateAsync_Success_ReturnsValidEmbedding()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var expectedValues = new float[] { 0.1f, 0.2f, 0.3f };
        var httpClient = CreateMockHttpClient(expectedValues);

        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await provider.GenerateAsync("test text");

        Assert.True(result.Success);
        Assert.NotNull(result.Embedding);
        Assert.Equal(3, result.Embedding.Dimensions);
        Assert.Equal(expectedValues, result.Embedding.Values);
        Assert.Equal("test", result.Embedding.Provider);
        Assert.Equal("test-model", result.Embedding.Model);
        Assert.True(result.Embedding.IsValid());
    }

    [Fact]
    public async Task GenerateAsync_EmptyText_ReturnsFailure()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var httpClient = CreateMockHttpClient(new float[] { 0.1f, 0.2f, 0.3f });
        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await provider.GenerateAsync("");

        Assert.False(result.Success);
        Assert.Contains("required", result.ErrorMessage!);
    }

    [Fact]
    public async Task GenerateAsync_DisabledProvider_ReturnsFailure()
    {
        var options = CreateOptions(enabled: false);
        _optionsMock.Setup(o => o.Value).Returns(options);

        var httpClient = CreateMockHttpClient(new float[] { 0.1f, 0.2f, 0.3f });
        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await provider.GenerateAsync("test");

        Assert.False(result.Success);
        Assert.Contains("not configured", result.ErrorMessage!);
    }

    [Fact]
    public async Task GenerateAsync_NoBaseUrl_ReturnsFailure()
    {
        var options = CreateOptions(baseUrl: "");
        _optionsMock.Setup(o => o.Value).Returns(options);

        var httpClient = CreateMockHttpClient(new float[] { 0.1f, 0.2f, 0.3f });
        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await provider.GenerateAsync("test");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GenerateAsync_HttpError_ReturnsFailure()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.example.com/v1")
        };

        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await provider.GenerateAsync("test");

        Assert.False(result.Success);
        Assert.Contains("HTTP", result.ErrorMessage!);
    }

    [Fact]
    public async Task GenerateAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.example.com/v1")
        };

        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.GenerateAsync("test", new CancellationToken(true)));
    }

    [Fact]
    public async Task GenerateBatchAsync_MultipleTexts_ReturnsAll()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var response = new EmbeddingApiResponse
        {
            Data =
            [
                new EmbeddingApiData { Embedding = new float[] { 0.1f, 0.2f, 0.3f }, Index = 0 },
                new EmbeddingApiData { Embedding = new float[] { 0.4f, 0.5f, 0.6f }, Index = 1 },
                new EmbeddingApiData { Embedding = new float[] { 0.7f, 0.8f, 0.9f }, Index = 2 }
            ],
            Model = "test-model"
        };

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                It.IsAny<HttpRequestMessage>(),
                It.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(response, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })
            });

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.example.com/v1")
        };

        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        var results = await provider.GenerateBatchAsync(["text1", "text2", "text3"]);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public void IsAvailable_WhenEnabled_ReturnsTrue()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var httpClient = CreateMockHttpClient([0.1f, 0.2f, 0.3f]);
        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        Assert.True(provider.IsAvailable);
    }

    [Fact]
    public void Profile_ReturnsCorrectValues()
    {
        var options = CreateOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        var httpClient = CreateMockHttpClient([0.1f, 0.2f, 0.3f]);
        var provider = new OpenAICompatibleEmbeddingProvider(
            httpClient, _optionsMock.Object, _loggerMock.Object);

        Assert.Equal("test", provider.Profile.Provider);
        Assert.Equal("test-model", provider.Profile.Model);
        Assert.Equal(3, provider.Profile.Dimensions);
    }
}
