using DeveloperMemory.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Services;

public class FreeLlmApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _appSettings;

    public FreeLlmApiClient(HttpClient httpClient, IOptions<AppSettings> appSettings)
    {
        _httpClient = httpClient;
        _appSettings = appSettings.Value;

        if (!string.IsNullOrEmpty(_appSettings.FreeLlmApi.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _appSettings.FreeLlmApi.ApiKey);
        }
    }

    public async Task<string> SendPromptAsync(string prompt)
    {
        if (string.IsNullOrEmpty(_appSettings.FreeLlmApi.BaseUrl))
        {
            throw new InvalidOperationException("FreeLlmApi base URL is not configured");
        }

        var request = new
        {
            prompt = prompt,
            max_tokens = 150,
            temperature = 0.7,
            top_p = 1,
            stream = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(_appSettings.FreeLlmApi.BaseUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Error from FreeLlm API: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<dynamic>(responseContent);

        return responseData.choices[0].text;
    }
}