using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Services;

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

        if (!string.IsNullOrEmpty(_appSettings.FreeLlmApi.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _appSettings.FreeLlmApi.ApiKey);
        }
    }

    /// <summary>
    /// Resolves the model to use for a request.
    /// Priority: per-request override > configured DefaultModel > "auto".
    /// </summary>
    private string ResolveModel(string? requestModel)
    {
        if (!string.IsNullOrWhiteSpace(requestModel))
            return requestModel;

        if (!string.IsNullOrWhiteSpace(_appSettings.FreeLlmApi.DefaultModel))
            return _appSettings.FreeLlmApi.DefaultModel;

        return "auto";
    }

    /// <summary>
    /// Forwards the full OpenAI chat completion request to the upstream LLM API.
    /// The enriched prompt (with knowledge + profile context) replaces the last user message.
    /// Model resolution: per-request override → configured DefaultModel → "auto".
    /// </summary>
    public async Task<OpenAIChatCompletionResponse> SendCompletionAsync(
        OpenAIChatCompletionRequest request,
        string enrichedPrompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appSettings.FreeLlmApi.BaseUrl))
        {
            throw new InvalidOperationException("FreeLlmApi base URL is not configured");
        }

        // Resolve model: per-request override wins, then config, then "auto"
        request.Model = ResolveModel(request.Model);

        // Replace the last user message with the enriched prompt
        var lastUserIndex = request.Messages.FindLastIndex(m => m.Role == "user");
        if (lastUserIndex >= 0)
        {
            request.Messages[lastUserIndex].Content = enrichedPrompt;
        }
        else
        {
            request.Messages.Add(new Message { Role = "user", Content = enrichedPrompt });
        }

        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        _logger.LogInformation("Sending request to FreeLLM: model={Model}, temperature={Temp}, maxTokens={MaxTokens}",
            request.Model, request.Temperature, request.MaxTokens);

        var response = await _httpClient.PostAsync(_appSettings.FreeLlmApi.BaseUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error from FreeLlm API: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Error from FreeLlm API: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var responseData = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseContent, JsonOptions);
            return responseData ?? new OpenAIChatCompletionResponse();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize FreeLlm API response");
            throw;
        }
    }

    /// <summary>
    /// Legacy method for /api/Proxy endpoint.
    /// Resolves model from: explicit requestModel → DefaultModel config → "auto".
    /// </summary>
    public async Task<string> SendPromptAsync(string prompt, string? requestModel = null, CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(requestModel);

        var request = new OpenAIChatCompletionRequest
        {
            Model = model,
            Messages = new List<Message>
            {
                new Message { Role = "user", Content = prompt }
            },
            Temperature = 0.7,
            MaxTokens = 150,
            Stream = false
        };

        var response = await SendCompletionAsync(request, prompt, cancellationToken);
        return response.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appSettings.FreeLlmApi.BaseUrl))
        {
            return new List<string>();
        }

        try
        {
            var baseUri = new Uri(_appSettings.FreeLlmApi.BaseUrl);
            var modelsUrl = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}/v1/models";

            var response = await _httpClient.GetAsync(modelsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch models from upstream API. Status code: {StatusCode}", response.StatusCode);
                return new List<string>();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var modelResponse = JsonSerializer.Deserialize<OpenAIModelListResponse>(responseContent, JsonOptions);
                return modelResponse?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize models response from upstream API");
                return new List<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models from upstream API");
            return new List<string>();
        }
    }
}
