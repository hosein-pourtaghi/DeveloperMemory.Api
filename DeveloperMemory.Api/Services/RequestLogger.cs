using DeveloperMemory.Api.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// Logs detailed request/response metrics to a file for token comparison and debugging.
/// Creates one log file per day in the configured request log folder.
/// </summary>
public class RequestLogger
{
    private readonly ILogger<RequestLogger> _logger;
    private readonly string _logFolder;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public RequestLogger(ILogger<RequestLogger> logger, IConfiguration configuration)
    {
        _logger = logger;
        _logFolder = configuration.GetValue<string>("AppSettings:Paths:RequestLogFolder") ?? "./logs/requests";
    }

    /// <summary>
    /// Logs a full request/response cycle with token counts and model selection.
    /// </summary>
    public async Task LogRequestAsync(
        string phase, // "INCOMING", "ENRICHED", "SENT"
        OpenAIChatCompletionRequest request,
        string? selectedModel = null,
        int? incomingTokens = null,
        int? enrichedTokens = null,
        int? responseTokens = null,
        long? providerTokens = null,
        double? latencyMs = null,
        bool isStreaming = false)
    {
        var entry = new StringBuilder();
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");

        entry.AppendLine($"[{timestamp}] {phase} | model={request.Model ?? "null"} | stream={isStreaming} | messages={request.Messages.Count}");

        if (selectedModel != null)
            entry.AppendLine($"  selected_model: {selectedModel}");

        if (incomingTokens.HasValue)
            entry.AppendLine($"  incoming_tokens: ~{incomingTokens}");

        if (enrichedTokens.HasValue)
            entry.AppendLine($"  enriched_tokens: ~{enrichedTokens}");

        if (responseTokens.HasValue)
            entry.AppendLine($"  response_tokens: ~{responseTokens}");

        if (providerTokens.HasValue)
            entry.AppendLine($"  provider_tokens: {providerTokens}");

        if (latencyMs.HasValue)
            entry.AppendLine($"  latency_ms: {latencyMs:F0}");

        // Log each message's token estimate
        if (request.Messages.Count > 0)
        {
            entry.AppendLine("  messages:");
            for (int i = 0; i < request.Messages.Count; i++)
            {
                var msg = request.Messages[i];
                var contentPreview = msg.Content?.Length > 100 ? msg.Content[..100] + "..." : msg.Content;
                var tokens = TokenEstimator.EstimateTokens(msg.Content ?? "");
                entry.AppendLine($"    [{i}] role={msg.Role} tokens=~{tokens} content={contentPreview}");
            }
        }

        // Write to console via Serilog
        _logger.LogWarning("TokenTracker: {LogEntry}", entry.ToString().TrimEnd());

        // Write to daily log file
        await WriteToFileAsync(entry.ToString());
    }

    private async Task WriteToFileAsync(string content)
    {
        try
        {
            Directory.CreateDirectory(_logFolder);
            var fileName = $"requests-{DateTime.UtcNow:yyyy-MM-dd}.log";
            var filePath = Path.Combine(_logFolder, fileName);
            await File.AppendAllTextAsync(filePath, content + "\n");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write request log to file");
        }
    }
}
