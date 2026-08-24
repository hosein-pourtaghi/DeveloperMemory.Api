using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeveloperMemory.Api.Services;

public class PromptBuilder
{
    private const int MaxKnowledgeContextLength = 2000;
    private const int MaxMemoryEntries = 10;

    /// <summary>
    /// Enriches the OpenAI request messages with developer profile, relevant knowledge,
    /// and persistent memory entries while preserving the original conversation history.
    /// The original messages are never modified or removed.
    ///
    /// Instruction precedence (highest to lowest):
    ///   1. Client's existing system message (preserved and appended to, not replaced)
    ///   2. Persistent memory context (appended to system message)
    ///   3. DeveloperMemory profile context (appended to system message)
    ///   4. Retrieved knowledge context (appended to system message)
    ///   5. User messages (preserved as-is)
    /// </summary>
    public OpenAIChatCompletionRequest BuildEnrichedRequest(
        OpenAIChatCompletionRequest request,
        List<DeveloperProfile> profiles,
        List<SearchResult> searchResults,
        List<MemoryDto>? memories = null)
    {
        var hasProfileContext = profiles.Count > 0;
        var hasKnowledgeContext = searchResults.Count > 0;
        var hasMemoryContext = memories?.Count > 0;

        if (!hasProfileContext && !hasKnowledgeContext && !hasMemoryContext)
        {
            return request;
        }

        // Build the DeveloperMemory context block
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("--- DeveloperMemory Context ---");
        contextBuilder.AppendLine();

        if (hasMemoryContext)
        {
            contextBuilder.AppendLine(BuildMemoryContext(memories!));
        }

        if (hasProfileContext)
        {
            contextBuilder.AppendLine(BuildProfileContext(profiles));
        }

        if (hasKnowledgeContext)
        {
            contextBuilder.AppendLine(BuildKnowledgeContext(searchResults));
        }

        contextBuilder.AppendLine("--- End DeveloperMemory Context ---");
        contextBuilder.AppendLine();

        var contextBlock = contextBuilder.ToString();

        // Create a new message list preserving the original conversation
        var enrichedMessages = new List<Message>();

        bool contextInjected = false;

        foreach (var message in request.Messages)
        {
            if (message.Role == "system" && !contextInjected)
            {
                // Append DeveloperMemory context to the existing system message
                enrichedMessages.Add(new Message
                {
                    Role = "system",
                    Content = message.Content + contextBlock,
                    ExtensionData = message.ExtensionData
                });
                contextInjected = true;
            }
            else
            {
                // Preserve all other messages as-is
                enrichedMessages.Add(new Message
                {
                    Role = message.Role,
                    Content = message.Content,
                    ToolCalls = message.ToolCalls,
                    ToolCallId = message.ToolCallId,
                    Name = message.Name,
                    ExtensionData = message.ExtensionData
                });
            }
        }

        // If there was no system message, inject one at the beginning
        if (!contextInjected)
        {
            enrichedMessages.Insert(0, new Message
            {
                Role = "system",
                Content = $"You are a helpful assistant.{contextBlock}"
            });
        }

        // Build the enriched request, copying all original fields
        var enrichedRequest = new OpenAIChatCompletionRequest
        {
            Model = request.Model,
            Messages = enrichedMessages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            N = request.N,
            Stream = request.Stream,
            Stop = request.Stop,
            MaxTokens = request.MaxTokens,
            MaxCompletionTokens = request.MaxCompletionTokens,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            User = request.User,
            StreamOptions = request.StreamOptions,
            ExtensionData = request.ExtensionData
        };

        return enrichedRequest;
    }

    private string BuildProfileContext(List<DeveloperProfile> profiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Developer Profile]");

        foreach (var profile in profiles)
        {
            sb.AppendLine($"Name: {profile.Name}");
            sb.AppendLine($"Role: {profile.Role}");
            if (profile.Skills.Count > 0)
                sb.AppendLine($"Skills: {string.Join(", ", profile.Skills)}");
            if (!string.IsNullOrWhiteSpace(profile.Experience))
                sb.AppendLine($"Experience: {profile.Experience}");
            if (!string.IsNullOrWhiteSpace(profile.Bio))
                sb.AppendLine($"Bio: {Truncate(profile.Bio, 500)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildKnowledgeContext(List<SearchResult> searchResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Relevant Knowledge]");

        var included = 0;
        foreach (var result in searchResults)
        {
            if (included >= 5) break; // Limit to top 5 results to avoid bloating the prompt

            var contentPreview = Truncate(result.Content, 500);
            sb.AppendLine($"## {result.Title} (relevance: {result.Score:F2})");
            sb.AppendLine(contentPreview);
            sb.AppendLine();
            included++;
        }

        if (searchResults.Count > 5)
        {
            sb.AppendLine($"({searchResults.Count - 5} additional results omitted for brevity)");
        }

        return sb.ToString();
    }

    private string BuildMemoryContext(List<MemoryDto> memories)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Persistent Memory]");
        sb.AppendLine("These are facts, preferences, and decisions from your persistent memory. Use them as authoritative context.");
        sb.AppendLine();

        var included = 0;
        foreach (var memory in memories)
        {
            if (included >= MaxMemoryEntries) break;

            var contentPreview = Truncate(memory.Content, 300);
            sb.AppendLine($"## {memory.Title}");
            sb.AppendLine($"Scope: {memory.Scope} | State: {memory.State} | Importance: {memory.Importance:F1}");
            if (memory.Tags.Count > 0)
                sb.AppendLine($"Tags: {string.Join(", ", memory.Tags)}");
            sb.AppendLine(contentPreview);
            sb.AppendLine();
            included++;
        }

        if (memories.Count > MaxMemoryEntries)
        {
            sb.AppendLine($"({memories.Count - MaxMemoryEntries} additional memories omitted for brevity)");
        }

        return sb.ToString();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    // ── Legacy method kept for backward compatibility with ProxyController ──

    /// <summary>
    /// Legacy: Builds a single enriched prompt string from a PromptRequest.
    /// Used by the legacy /api/Proxy endpoint. Prefer <see cref="BuildEnrichedRequest"/> for new code.
    /// </summary>
    public string BuildPrompt(PromptRequest request, List<DeveloperProfile> profiles, List<SearchResult> searchResults)
    {
        var profile = profiles.FirstOrDefault(p => p.Id.ToString() == request.ProfileId);
        var systemPrompt = request.SystemPrompt ?? "You are a helpful assistant.";
        var userQuery = request.Query ?? string.Empty;

        var prompt = $"{systemPrompt}\n\n";

        if (profile != null)
        {
            prompt += "Developer Profile:\n";
            prompt += $"Name: {profile.Name}\n";
            prompt += $"Role: {profile.Role}\n";
            prompt += $"Skills: {string.Join(", ", profile.Skills)}\n";
            prompt += $"Experience: {profile.Experience}\n";
            prompt += $"Bio: {profile.Bio}\n\n";
        }

        if (searchResults.Count > 0)
        {
            prompt += "Relevant Knowledge:\n";
            foreach (var result in searchResults)
            {
                prompt += $"- {result.Title} (Score: {result.Score:F2})\n";
                prompt += $"  {Truncate(result.Content, 200)}\n";
            }
            prompt += "\n";
        }

        prompt += $"User Query: {userQuery}\n";
        return prompt;
    }
}
