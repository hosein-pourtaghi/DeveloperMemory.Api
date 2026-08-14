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

    public async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appSettings.FreeLlmApi.BaseUrl))
        {
            throw new InvalidOperationException("FreeLlmApi base URL is not configured");
        }

        // Build OpenAI-compatible chat completion request
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-3.5-turbo", // Default model, can be overridden
            Messages = new List<Message>
            {
                new Message
                {
                    Role = "user",
                    Content = prompt
                }
            },
            Temperature = 0.7,
            MaxTokens = 150,
            Stream = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(_appSettings.FreeLlmApi.BaseUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error from FreeLlm API: {ErrorContent}", errorContent);
            throw new HttpRequestException($"Error from FreeLlm API: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        try
        {
            var responseData = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseContent);
            return responseData?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize FreeLlm API response");
            throw;
        }
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appSettings.FreeLlmApi.BaseUrl))
        {
            return new List<string>();
        }

        try
        {
            // Construct the models endpoint URL from the base URL
            // BaseUrl is like "http://localhost:3001/v1", so models endpoint is "http://localhost:3001/v1/models"
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
                var modelResponse = JsonSerializer.Deserialize<OpenAIModelListResponse>(responseContent);
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