using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DeveloperMemory.Infrastructure.Configuration;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// LLM-based intent analyzer.
/// Uses an OpenAI-compatible LLM to analyze user intent with higher accuracy.
///
/// Falls back gracefully when LLM is unavailable.
/// Always returns a valid result — never throws for operational failures.
/// </summary>
public class LlmIntentAnalyzer : IIntentAnalyzer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MemoryIntelligenceOptions _options;
    private readonly ILogger<LlmIntentAnalyzer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LlmIntentAnalyzer(
        IHttpClientFactory httpClientFactory,
        IOptions<MemoryIntelligenceOptions> options,
        ILogger<LlmIntentAnalyzer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IntentAnalysisResult> AnalyzeAsync(
        string input,
        PromptContext? context = null,
        CancellationToken ct = default)
    {
        if (!_options.IsAvailable || string.IsNullOrWhiteSpace(input))
        {
            return CreateFallbackResult(input);
        }

        try
        {
            var request = CreateAnalysisRequest(input, context);
            var response = await CallLlmAsync(request, ct);

            if (response == null)
            {
                return CreateFallbackResult(input);
            }

            return ParseResponse(response, input);
        }
        catch (OperationCanceledException)
        {
            throw; // Always propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM intent analysis failed");
            return CreateFallbackResult(input);
        }
    }

    private object CreateAnalysisRequest(string input, PromptContext? context)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(input, context);

        return new
        {
            model = _options.ExtractionModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1,
            max_tokens = 1000,
            response_format = new { type = "json_object" }
        };
    }

    private static string BuildSystemPrompt()
    {
        return """
        You are an intent analysis assistant. Analyze user requests and classify their intent.

        Return JSON with this structure:
        {
          "intent": "Coding|Debugging|Architecture|Documentation|Research|Explanation|Refactoring|Planning|General",
          "task_type": "Coding|Debugging|Architecture|Documentation|Research|Explanation|Refactoring|Planning|General|Performance|Security|Testing",
          "technical_domain": "Database|API|Architecture|Testing|DevOps|Security|General",
          "complexity": "Simple|Medium|Complex|Expert",
          "risk_level": "Low|Normal|Elevated|High",
          "requires_memory": true,
          "requires_project_context": true,
          "keywords": ["keyword1", "keyword2"],
          "explicit_constraints": ["constraint1"],
          "confidence": 0.85
        }

        RULES:
        1. Classify based on the actual content, not assumptions.
        2. Be conservative with risk assessment.
        3. Identify explicit constraints mentioned in quotes.
        4. Return structured JSON only.
        """;
    }

    private static string BuildUserPrompt(string input, PromptContext? context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze this user request:");
        sb.AppendLine();
        sb.AppendLine($"Request: {Truncate(input, 2000)}");

        if (context != null)
        {
            if (!string.IsNullOrEmpty(context.UserId))
            {
                sb.AppendLine($"User: {context.UserId}");
            }
            if (context.ProjectId.HasValue)
            {
                sb.AppendLine($"Project: {context.ProjectId}");
            }
        }

        return sb.ToString();
    }

    private async Task<string?> CallLlmAsync(object request, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("MemoryExtraction");
        var endpoint = $"{_options.ExtractionProvider}/chat/completions";

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.ExtractionTimeoutSeconds));

        var response = await client.PostAsync(endpoint, content, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    private IntentAnalysisResult ParseResponse(string responseJson, string input)
    {
        try
        {
            var response = JsonSerializer.Deserialize<LlmIntentResponse>(responseJson, JsonOptions);
            if (response == null)
            {
                return CreateFallbackResult(input);
            }

            // Validate and parse intent
            IntentType intent;
            if (!Enum.TryParse<IntentType>(response.Intent, true, out intent))
            {
                intent = IntentType.General;
            }

            TaskType taskType;
            if (!Enum.TryParse<TaskType>(response.TaskType, true, out taskType))
            {
                taskType = TaskType.General;
            }

            var confidence = Math.Clamp(response.Confidence, 0.0, 1.0);

            return new IntentAnalysisResult
            {
                OriginalInput = input,
                Intent = intent,
                TaskType = taskType,
                TechnicalDomain = response.TechnicalDomain ?? "General",
                Complexity = ParseEnum<ComplexityLevel>(response.Complexity, ComplexityLevel.Medium),
                RiskLevel = ParseEnum<RiskLevel>(response.RiskLevel, RiskLevel.Normal),
                Keywords = response.Keywords ?? [],
                ExplicitConstraints = response.ExplicitConstraints ?? [],
                RequiresProjectContext = response.RequiresProjectContext,
                IsMemoryInstruction = intent == IntentType.General && input.ToLowerInvariant().Contains("remember"),
                GoalSummary = $"LLM analysis ({confidence:P0}): {input.Length > 80 ? input[..80] + "..." : input}",
                IsSimpleQuery = intent == IntentType.General && input.Length < 100
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM intent response");
            return CreateFallbackResult(input);
        }
    }

    private static T ParseEnum<T>(string? value, T defaultValue) where T : struct
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return Enum.TryParse<T>(value, true, out var result) ? result : defaultValue;
    }

    private static IntentAnalysisResult CreateFallbackResult(string input)
    {
        return new IntentAnalysisResult
        {
            OriginalInput = input ?? string.Empty,
            Intent = IntentType.General,
            TaskType = TaskType.General,
            IsSimpleQuery = string.IsNullOrWhiteSpace(input) || input.Length < 100
        };
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}

// ── LLM response DTOs ──

internal class LlmIntentResponse
{
    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("task_type")]
    public string? TaskType { get; set; }

    [JsonPropertyName("technical_domain")]
    public string? TechnicalDomain { get; set; }

    [JsonPropertyName("complexity")]
    public string? Complexity { get; set; }

    [JsonPropertyName("risk_level")]
    public string? RiskLevel { get; set; }

    [JsonPropertyName("requires_memory")]
    public bool RequiresMemory { get; set; } = true;

    [JsonPropertyName("requires_project_context")]
    public bool RequiresProjectContext { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("explicit_constraints")]
    public List<string>? ExplicitConstraints { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.5;
}
