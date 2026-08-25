using System.Net.Http.Json;
using System.Text.Json;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// OpenAI-compatible LLM quality evaluator.
/// Evaluates prompt quality using an external LLM behind a provider-independent abstraction.
/// LLM output is treated as untrusted data and validated before use.
/// </summary>
public class LlmPromptQualityEvaluator : ILlmPromptQualityEvaluator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmPromptQualityEvaluator> _logger;

    public LlmPromptQualityEvaluator(
        IHttpClientFactory httpClientFactory,
        ILogger<LlmPromptQualityEvaluator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsAvailable => true;
    public string EvaluatorName => "llm";
    public string EvaluatorVersion => "1.0";

    public async Task<PromptQualityScore> EvaluateAsync(
        string originalPrompt,
        string optimizedPrompt,
        IntentAnalysisResult? intent = null,
        int tokenBudget = 4000,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient("LlmEvaluation");
            var request = BuildEvaluationRequest(originalPrompt, optimizedPrompt, intent, tokenBudget);

            var response = await client.PostAsJsonAsync("chat/completions", request, ct);
            response.EnsureSuccessStatusCode();

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(ct);
            if (completion?.Choices == null || completion.Choices.Count == 0)
            {
                throw new InvalidOperationException("No response from LLM");
            }

            var content = completion.Choices[0].Message?.Content ?? string.Empty;
            var evaluation = ParseEvaluationResponse(content);

            // Validate the LLM output
            evaluation = ValidateAndClampScores(evaluation);

            sw.Stop();
            _logger.LogDebug("LLM quality evaluation completed in {Duration}ms", sw.ElapsedMilliseconds);

            return evaluation;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "LLM quality evaluation failed after {Duration}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static object BuildEvaluationRequest(
        string originalPrompt,
        string optimizedPrompt,
        IntentAnalysisResult? intent,
        int tokenBudget)
    {
        var constraintInfo = intent?.ExplicitConstraints.Count > 0
            ? $"Explicit constraints: {string.Join(", ", intent.ExplicitConstraints)}"
            : "No explicit constraints.";

        var systemPrompt = @"You are a prompt quality evaluator. Analyze the optimized prompt and return a JSON evaluation.

Score each dimension from 0.0 to 1.0:
- intentPreservation: How well the original intent is preserved
- constraintPreservation: How well explicit constraints are preserved
- contextRelevance: How relevant the context is to the task
- structure: How well-structured the prompt is
- tokenEfficiency: How efficiently tokens are used (within budget)
- security: Whether security boundaries are intact

Return ONLY valid JSON with this exact structure:
{
  ""intentPreservation"": 0.92,
  ""constraintPreservation"": 0.95,
  ""contextRelevance"": 0.84,
  ""structure"": 0.90,
  ""tokenEfficiency"": 0.88,
  ""security"": 0.98,
  ""issues"": [],
  ""recommendations"": []
}

Do NOT include chain-of-thought reasoning. Return only structured evaluation metadata.";

        var userPrompt = $@"Original prompt:
{Truncate(originalPrompt, 2000)}

Optimized prompt:
{Truncate(optimizedPrompt, 4000)}

Token budget: {tokenBudget}
{constraintInfo}

Evaluate the optimized prompt quality.";

        return new
        {
            model = "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.0,
            max_tokens = 500,
            response_format = new { type = "json_object" }
        };
    }

    private static PromptQualityScore ParseEvaluationResponse(string content)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(content);

            return new PromptQualityScore
            {
                IntentPreservation = json.GetProperty("intentPreservation").GetDouble(),
                ConstraintPreservation = json.GetProperty("constraintPreservation").GetDouble(),
                ContextRelevance = json.GetProperty("contextRelevance").GetDouble(),
                Structure = json.GetProperty("structure").GetDouble(),
                TokenEfficiency = json.GetProperty("tokenEfficiency").GetDouble(),
                SecurityValidation = json.GetProperty("security").GetDouble(),
                Evaluator = "llm",
                EvaluatorVersion = "1.0",
                Issues = json.TryGetProperty("issues", out var issues)
                    ? issues.EnumerateArray().Select(i => i.GetString() ?? string.Empty).ToList()
                    : [],
                Recommendations = json.TryGetProperty("recommendations", out var recs)
                    ? recs.EnumerateArray().Select(r => r.GetString() ?? string.Empty).ToList()
                    : []
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse LLM evaluation response: {ex.Message}");
        }
    }

    private static PromptQualityScore ValidateAndClampScores(PromptQualityScore score)
    {
        // Clamp all scores to [0, 1]
        score.IntentPreservation = Math.Clamp(score.IntentPreservation, 0.0, 1.0);
        score.ConstraintPreservation = Math.Clamp(score.ConstraintPreservation, 0.0, 1.0);
        score.ContextRelevance = Math.Clamp(score.ContextRelevance, 0.0, 1.0);
        score.Structure = Math.Clamp(score.Structure, 0.0, 1.0);
        score.TokenEfficiency = Math.Clamp(score.TokenEfficiency, 0.0, 1.0);
        score.SecurityValidation = Math.Clamp(score.SecurityValidation, 0.0, 1.0);

        score.ComputeOverall();
        return score;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    // Internal DTOs for OpenAI-compatible response
    private class ChatCompletionResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        public Message? Message { get; set; }
    }

    private class Message
    {
        public string? Content { get; set; }
    }
}
