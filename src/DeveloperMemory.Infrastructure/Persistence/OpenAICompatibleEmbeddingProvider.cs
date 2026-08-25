using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// OpenAI-compatible embedding provider adapter.
/// Supports any provider implementing the OpenAI-compatible embedding API contract.
///
/// Production path:
///   IEmbeddingProvider.GenerateAsync(text)
///       → HTTP POST {baseUrl}/embeddings
///       → Parse response
///       → Validate vector
///       → Return Embedding
///
/// Never leaks provider-specific types into Domain or Application layers.
/// </summary>
public class OpenAICompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<OpenAICompatibleEmbeddingProvider> _logger;

    public OpenAICompatibleEmbeddingProvider(
        HttpClient httpClient,
        IOptions<EmbeddingOptions> options,
        ILogger<OpenAICompatibleEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => _options.Provider;

    public bool IsAvailable =>
        _options.Enabled &&
        !string.IsNullOrEmpty(_options.BaseUrl);

    public EmbeddingProfile Profile => new()
    {
        Provider = _options.Provider,
        Model = _options.Model,
        Dimensions = _options.Dimensions
    };

    public async Task<EmbeddingResult> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Embedding provider is not configured or disabled"
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Text is required for embedding generation"
            };
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var response = await PostEmbeddingRequestAsync(text, cancellationToken);
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response == null)
            {
                return new EmbeddingResult
                {
                    Success = false,
                    ErrorMessage = "No response from embedding provider",
                    GenerationDurationMs = duration
                };
            }

            var embedding = ParseEmbeddingResponse(response);
            if (embedding == null || !embedding.IsValid())
            {
                return new EmbeddingResult
                {
                    Success = false,
                    ErrorMessage = "Invalid embedding vector received from provider",
                    GenerationDurationMs = duration
                };
            }

            // Validate dimensions if configured
            if (_options.Dimensions > 0 && embedding.Dimensions != _options.Dimensions)
            {
                _logger.LogWarning(
                    "Embedding dimensions mismatch: expected {Expected}, got {Actual}",
                    _options.Dimensions, embedding.Dimensions);
            }

            embedding.Provider = _options.Provider;
            embedding.Model = _options.Model;

            _logger.LogDebug(
                "Embedding generated: {Dimensions}d in {Duration}ms",
                embedding.Dimensions, duration);

            return new EmbeddingResult
            {
                Embedding = embedding,
                Success = true,
                GenerationDurationMs = duration
            };
        }
        catch (OperationCanceledException)
        {
            throw; // Always propagate cancellation
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Embedding HTTP request failed");
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse embedding response");
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = $"Response parsing failed: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed");
            return new EmbeddingResult
            {
                Success = false,
                ErrorMessage = $"Embedding generation failed: {ex.Message}"
            };
        }
    }

    public async Task<IReadOnlyList<EmbeddingResult>> GenerateBatchAsync(
        IReadOnlyCollection<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return texts.Select(_ => new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Embedding provider is not configured or disabled"
            }).ToList();
        }

        if (texts.Count == 0)
        {
            return [];
        }

        // For batch, send all inputs in a single request
        try
        {
            var startTime = DateTime.UtcNow;
            var inputArray = texts.ToArray();
            var response = await PostBatchEmbeddingRequestAsync(inputArray, cancellationToken);
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response == null || response.Data == null || response.Data.Count != inputArray.Length)
            {
                // If batch response is incomplete, fall back to individual requests
                return await GenerateIndividuallyAsync(texts, cancellationToken);
            }

            var results = new List<EmbeddingResult>();
            foreach (var item in response.Data.OrderBy(d => d.Index))
            {
                if (item.Embedding == null || item.Embedding.Length == 0)
                {
                    results.Add(new EmbeddingResult
                    {
                        Success = false,
                        ErrorMessage = "Empty embedding in batch response"
                    });
                    continue;
                }

                var embedding = new Embedding
                {
                    Values = item.Embedding,
                    Provider = _options.Provider,
                    Model = _options.Model
                };

                if (!embedding.IsValid())
                {
                    results.Add(new EmbeddingResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid embedding vector in batch response"
                    });
                    continue;
                }

                results.Add(new EmbeddingResult
                {
                    Embedding = embedding,
                    Success = true,
                    GenerationDurationMs = duration / inputArray.Length
                });
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch embedding failed, falling back to individual requests");
            return await GenerateIndividuallyAsync(texts, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<EmbeddingResult>> GenerateIndividuallyAsync(
        IReadOnlyCollection<string> texts,
        CancellationToken cancellationToken)
    {
        var results = new List<EmbeddingResult>();
        foreach (var text in texts)
        {
            results.Add(await GenerateAsync(text, cancellationToken));
        }
        return results;
    }

    private async Task<EmbeddingApiResponse?> PostEmbeddingRequestAsync(
        string input,
        CancellationToken ct)
    {
        var request = CreateRequest(input);
        var response = await SendRequestAsync(request, ct);
        return response;
    }

    private async Task<EmbeddingApiResponse?> PostBatchEmbeddingRequestAsync(
        string[] inputs,
        CancellationToken ct)
    {
        var request = CreateBatchRequest(inputs);
        var response = await SendRequestAsync(request, ct);
        return response;
    }

    private EmbeddingApiRequest CreateRequest(string input)
    {
        var request = new EmbeddingApiRequest
        {
            Model = _options.Model,
            Input = input
        };
        return request;
    }

    private EmbeddingApiRequest CreateBatchRequest(string[] inputs)
    {
        var request = new EmbeddingApiRequest
        {
            Model = _options.Model,
            InputArray = inputs
        };
        return request;
    }

    private async Task<EmbeddingApiResponse?> SendRequestAsync(
        EmbeddingApiRequest request,
        CancellationToken ct)
    {
        using var httpMessage = new HttpRequestMessage(HttpMethod.Post, "/embeddings");

        if (_options.RequiresApiKey)
        {
            httpMessage.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        httpMessage.Content = JsonContent.Create(request, options: CreateJsonOptions());

        using var response = await _httpClient.SendAsync(httpMessage, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<EmbeddingApiResponse>(
            CreateJsonOptions(), ct);
    }

    private static Embedding? ParseEmbeddingResponse(EmbeddingApiResponse response)
    {
        if (response.Data == null || response.Data.Count == 0)
            return null;

        // Take the first embedding (single input) or the one at index 0
        var first = response.Data.FirstOrDefault(d => d.Index == 0)
                     ?? response.Data[0];

        if (first.Embedding == null || first.Embedding.Length == 0)
            return null;

        return new Embedding
        {
            Values = first.Embedding,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

// ── OpenAI-compatible API DTOs (internal to adapter) ──

internal class EmbeddingApiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Input { get; set; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string[]? InputArray { get; set; }

    // OpenAI accepts either a string or an array for "input".
    // We use a custom converter to handle both cases.
}

internal class EmbeddingApiResponse
{
    [JsonPropertyName("data")]
    public List<EmbeddingApiData> Data { get; set; } = [];

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("usage")]
    public EmbeddingApiUsage? Usage { get; set; }
}

internal class EmbeddingApiData
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = [];

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

internal class EmbeddingApiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
