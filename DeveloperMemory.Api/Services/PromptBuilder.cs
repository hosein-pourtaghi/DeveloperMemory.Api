using DeveloperMemory.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeveloperMemory.Api.Services;

public class PromptBuilder
{
    public string BuildPrompt(PromptRequest request, List<DeveloperProfile> profiles, List<SearchResult> searchResults)
    {
        var profile = profiles.FirstOrDefault(p => p.Id.ToString() == request.ProfileId);
        var systemPrompt = request.SystemPrompt ?? "You are a helpful assistant.";
        var userQuery = request.Query ?? string.Empty;

        var prompt = $"{systemPrompt}\n\n";

        if (profile != null)
        {
            prompt += $"Developer Profile:\n";
            prompt += $"Name: {profile.Name}\n";
            prompt += $"Role: {profile.Role}\n";
            prompt += $"Skills: {string.Join(", ", profile.Skills)}\n";
            prompt += $"Experience: {profile.Experience}\n";
            prompt += $"Bio: {profile.Bio}\n\n";
        }

        if (searchResults.Any())
        {
            prompt += "Relevant Knowledge:\n";
            foreach (var result in searchResults)
            {
                prompt += $"- {result.Title} (Score: {result.Score:F2})\n";
                prompt += $"  {result.Content.Substring(0, Math.Min(200, result.Content.Length))}...\n";
            }
            prompt += "\n";
        }

        prompt += $"User Query: {userQuery}\n";
        return prompt;
    }
}
