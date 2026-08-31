using DeveloperMemory.Domain.Entities;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// LLM-based prompt optimizer.
/// Uses an OpenAI-compatible LLM to improve prompt quality.
///
/// Rules:
/// - Preserve user intent
/// - Preserve explicit requirements
/// - Preserve architectural constraints
/// - Preserve safety constraints
/// - Remove unnecessary repetition
/// - Improve structure and clarity
/// - Stay within token budget
/// </summary>
public class LlmPromptOptimizer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MemoryIntelligenceOptions _options;
    private readonly ILogger<LlmPromptOptimizer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LlmPromptOptimizer(
        IHttpClientFactory httpClientFactory,
        IOptions<MemoryIntelligenceOptions> options,
        ILogger<LlmPromptOptimizer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Whether LLM optimization is available.
    /// </summary>
    public bool IsAvailable => _options.IsAvailable;

    /// <summary>
    /// Optimizes a prompt using LLM.
    /// </summary>
    public async Task<LlmOptimizationResult> OptimizeAsync(
        string originalPrompt,
        IntentAnalysisResult? intent = null,
        int tokenBudget = 4000,
        CancellationToken ct = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(originalPrompt))
        {
            return new LlmOptimizationResult
            {
                OptimizedPrompt = originalPrompt,
                Success = false,
                Reason = "LLM optimization not available"
            };
        }

        try
        {
            var request = CreateOptimizationRequest(originalPrompt, intent, tokenBudget);
            var response = await CallLlmAsync(request, ct);

            if (response == null)
            {
                return new LlmOptimizationResult
                {
                    OptimizedPrompt = originalPrompt,
                    Success = false,
                    Reason = "No response from LLM"
                };
            }

            return ParseResponse(response, originalPrompt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM prompt optimization failed");
            return new LlmOptimizationResult
            {
                OptimizedPrompt = originalPrompt,
                Success = false,
                Reason = $"Optimization failed: {ex.Message}"
            };
        }
    }

    private object CreateOptimizationRequest(string prompt, IntentAnalysisResult? intent, int tokenBudget)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(prompt, intent, tokenBudget);

        return new
        {
            model = _options.ExtractionModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.3,
            max_tokens = 4000
        };
    }

    private static string BuildSystemPrompt()
    {
        return """
        You are a prompt optimization assistant. Improve prompts for AI coding assistants.

        RULES:
        1. Preserve ALL user intent and requirements.
        2. Preserve ALL explicit constraints.
        3. Preserve ALL architectural decisions.
        4. Preserve ALL security constraints.
        5. Remove unnecessary repetition.
        6. Improve structure and clarity.
        7. Keep the prompt concise but complete.
        8. Do NOT invent new requirements.
        9. Do NOT change technical decisions.
        10. Do NOT remove safety constraints.

        Return the optimized prompt as plain text (not JSON).
        """;
    }

    private static string BuildUserPrompt(string prompt, IntentAnalysisResult? intent, int tokenBudget)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Optimize this prompt for an AI coding assistant:");
        sb.AppendLine();
        sb.AppendLine("ORIGINAL PROMPT:");
        sb.AppendLine("---");
        sb.AppendLine(Truncate(prompt, 3000));
        sb.AppendLine("---");

        if (intent != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Intent: {intent.Intent}");
            sb.AppendLine($"Task: {intent.TaskType}");
            sb.AppendLine($"Domain: {intent.TechnicalDomain}");
            if (intent.ExplicitConstraints.Count > 0)
            {
                sb.AppendLine($"Constraints: {string.Join(", ", intent.ExplicitConstraints)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Token budget: approximately {tokenBudget} tokens (~{tokenBudget * 4} characters)");
        sb.AppendLine();
        sb.AppendLine("Return the optimized prompt only:");

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

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        try
        {
            var completion = JsonSerializer.Deserialize<LlmCompletionResponse>(responseJson, JsonOptions);
            return completion?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch
        {
            return responseJson; // Return raw response if parsing fails
        }
    }

    private LlmOptimizationResult ParseResponse(string response, string originalPrompt)
    {
        var optimized = response.Trim();

        // Basic validation
        if (string.IsNullOrWhiteSpace(optimized))
        {
            return new LlmOptimizationResult
            {
                OptimizedPrompt = originalPrompt,
                Success = false,
                Reason = "Empty optimization response"
            };
        }

        // Check if the optimized prompt is significantly different
        var similarity = CalculateSimilarity(originalPrompt, optimized);
        if (similarity < 0.3)
        {
            _logger.LogWarning(
                "Optimized prompt too different from original (similarity={Similarity:F2}); using original",
                similarity);
            return new LlmOptimizationResult
            {
                OptimizedPrompt = originalPrompt,
                Success = false,
                Reason = "Optimized prompt deviates too much from original"
            };
        }

        return new LlmOptimizationResult
        {
            OptimizedPrompt = optimized,
            Success = true,
            OriginalLength = originalPrompt.Length,
            OptimizedLength = optimized.Length,
            Reason = "LLM optimization applied"
        };
    }

    private static double CalculateSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var setA = new HashSet<string>(wordsA);
        var setB = new HashSet<string>(wordsB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}

/// <summary>
/// Result of LLM prompt optimization.
/// </summary>
public class LlmOptimizationResult
{
    /// <summary>The optimized prompt.</summary>
    public string OptimizedPrompt { get; set; } = string.Empty;

    /// <summary>Whether optimization succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Original character count.</summary>
    public int OriginalLength { get; set; }

    /// <summary>Optimized character count.</summary>
    public int OptimizedLength { get; set; }

    /// <summary>Reason for the result.</summary>
    public string Reason { get; set; } = string.Empty;
}

// ── LLM response DTOs ──

internal class LlmCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<LlmChoice>? Choices { get; set; }
}

internal class LlmChoice
{
    [JsonPropertyName("message")]
    public LlmMessage? Message { get; set; }
}

internal class LlmMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
