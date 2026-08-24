using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// HTTP client for communicating with any OpenAI-compatible LLM provider.
/// Supports both streaming and non-streaming requests. Configured via AppSettings.
/// </summary>
public class FreeLlmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _appSettings;
    private readonly ILogger<FreeLlmApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public FreeLlmApiClient(HttpClient httpClient, IOptions<AppSettings> appSettings, ILogger<FreeLlmApiClient> logger)
    {
        _httpClient = httpClient;
        _appSettings = appSettings.Value;
        _logger = logger;

        // Configure base address and timeout
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Generous timeout for large completions

        if (!string.IsNullOrEmpty(_appSettings.FreeLlmApi.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _appSettings.FreeLlmApi.ApiKey);
        }
    }

    /// <summary>
    /// Builds a full endpoint URL from the configured BaseUrl and an endpoint path.
    /// </summary>
    private string BuildEndpointUrl(string endpoint)
    {
        var baseUrl = _appSettings.FreeLlmApi.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith(endpoint, StringComparison.OrdinalIgnoreCase))
            return baseUrl;
        if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return $"{baseUrl}{endpoint}";
        return $"{baseUrl}/v1{endpoint}";
    }

    /// <summary>
    /// Resolves the model to use for a request.
    /// Priority: per-request override > configured DefaultModel > "auto".
    /// </summary>
    public string ResolveModel(string? requestModel)
    {
        if (!string.IsNullOrWhiteSpace(requestModel))
            return requestModel;

        if (!string.IsNullOrWhiteSpace(_appSettings.FreeLlmApi.DefaultModel))
            return _appSettings.FreeLlmApi.DefaultModel;

        return "auto";
    }

    /// <summary>
    /// Checks whether the downstream provider is configured and reachable.
    /// Returns true if the base URL is configured.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_appSettings.FreeLlmApi.BaseUrl);

    // ── Non-Streaming ──────────────────────────────────────────────────────

    /// <summary>
    /// Sends a chat completion request to the downstream provider (non-streaming).
    /// The request should already be enriched with DeveloperMemory context.
    /// </summary>
    public async Task<OpenAIChatCompletionResponse> SendCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        request.Model = ResolveModel(request.Model);
        request.Stream = false;

        var endpointUrl = BuildEndpointUrl("/chat/completions");

        _logger.LogInformation(
            "Sending non-streaming request to provider: url={Url}, model={Model}, messages={MessageCount}",
            endpointUrl, request.Model, request.Messages.Count);

        var requestBody = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpointUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Provider returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new DownstreamProviderException(response.StatusCode, errorContent);
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseContent, JsonOptions)
                   ?? new OpenAIChatCompletionResponse();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize provider response. Raw: {Response}",
                responseContent.Length > 500 ? responseContent[..500] : responseContent);
            throw;
        }
    }

    // ── Streaming ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sends a streaming chat completion request to the downstream provider.
    /// Returns the raw HTTP response message so the caller can stream the body
    /// directly to the client without buffering.
    /// The caller is responsible for disposing the response.
    /// </summary>
    public async Task<HttpResponseMessage> SendStreamingCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        request.Model = ResolveModel(request.Model);
        request.Stream = true;

        var endpointUrl = BuildEndpointUrl("/chat/completions");

        _logger.LogInformation(
            "Sending streaming request to provider: url={Url}, model={Model}, messages={MessageCount}",
            endpointUrl, request.Model, request.Messages.Count);

        var requestBody = JsonSerializer.Serialize(request, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        // Use ResponseHeadersRead so we can start streaming immediately
        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Provider returned streaming error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            response.Dispose();
            throw new DownstreamProviderException(response.StatusCode, errorContent);
        }

        return response;
    }

    // ── Model Listing ──────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the list of available models from the upstream provider.
    /// Returns empty list on failure (does not throw).
    /// </summary>
    public async Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return [];

        try
        {
            var modelsUrl = BuildEndpointUrl("/models");
            var response = await _httpClient.GetAsync(modelsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch models from upstream. Status: {StatusCode}", response.StatusCode);
                return [];
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var modelResponse = JsonSerializer.Deserialize<OpenAIModelListResponse>(responseContent, JsonOptions);
                return modelResponse?.Data?.Select(m => m.Id).ToList() ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize models response from upstream");
                return [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models from upstream provider");
            return [];
        }
    }

    /// <summary>
    /// Fetches details for a specific model from the upstream provider.
    /// </summary>
    public async Task<OpenAIModel?> GetModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return null;

        try
        {
            var modelsUrl = BuildEndpointUrl($"/models/{modelId}");
            var response = await _httpClient.GetAsync(modelsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<OpenAIModel>(responseContent, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching model {ModelId} from upstream provider", modelId);
            return null;
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Downstream LLM provider is not configured. Set AppSettings:FreeLlmApi:BaseUrl in appsettings.json.");
        }
    }
}

/// <summary>
/// Exception thrown when the downstream provider returns an error.
/// Carries the HTTP status code and raw error content for translation into OpenAI-compatible errors.
/// </summary>
public class DownstreamProviderException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string RawErrorContent { get; }

    public DownstreamProviderException(HttpStatusCode statusCode, string rawErrorContent)
        : base($"Downstream provider returned {statusCode}: {rawErrorContent}")
    {
        StatusCode = statusCode;
        RawErrorContent = rawErrorContent;
    }
}
