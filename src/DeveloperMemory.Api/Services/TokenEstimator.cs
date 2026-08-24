using DeveloperMemory.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// Estimates token counts for OpenAI requests and responses.
/// Uses a simple heuristic: ~4 characters per token (close to actual GPT tokenizer averages).
/// This is for logging/comparison purposes, not billing-accurate.
/// </summary>
public static class TokenEstimator
{
    private const double CharsPerToken = 4.0;

    /// <summary>
    /// Estimates tokens in a plain text string.
    /// </summary>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    /// <summary>
    /// Estimates tokens in a full OpenAI request body (the JSON that gets sent).
    /// </summary>
    public static int EstimateRequestTokens(OpenAIChatCompletionRequest request)
    {
        if (request?.Messages == null || request.Messages.Count == 0) return 0;

        int total = 0;
        foreach (var msg in request.Messages)
        {
            // Each message has ~4 tokens overhead for role/formatting
            total += 4;
            total += EstimateTokens(msg.Content ?? "");
            if (msg.ToolCalls != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    total += EstimateTokens(tc.Function?.Name ?? "");
                    total += EstimateTokens(tc.Function?.Arguments ?? "");
                }
            }
        }

        // Overhead for model, temperature, and other request parameters
        total += 3;

        return total;
    }

    /// <summary>
    /// Estimates tokens in an OpenAI response.
    /// </summary>
    public static int EstimateResponseTokens(OpenAIChatCompletionResponse response)
    {
        if (response?.Choices == null) return 0;

        int total = 0;
        foreach (var choice in response.Choices)
        {
            if (choice.Message?.Content != null)
                total += EstimateTokens(choice.Message.Content);
        }
        return total;
    }
}
