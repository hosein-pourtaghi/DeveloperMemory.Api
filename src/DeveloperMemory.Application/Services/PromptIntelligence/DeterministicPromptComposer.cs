using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using System.Text;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Deterministic, template-based prompt composer.
/// Assembles organized context into a structured prompt for downstream consumption.
/// 
/// The composer is provider-neutral — it does not generate OpenAI, Anthropic,
/// or vendor-specific request objects. It produces plain structured text.
/// 
/// Section order:
///   1. Project Context
///   2. Workspace Context
///   3. Applicable Rules and Constraints
///   4. Relevant Memory
///   5. Original Request
/// </summary>
public class DeterministicPromptComposer : IPromptComposer
{
    public PromptCompositionResult Compose(
        PromptAnalysis analysis,
        List<PromptConstraint> constraints,
        List<ContextSection> sections,
        string originalRequest,
        string? profileContext = null,
        string? knowledgeContext = null)
    {
        var instructions = new StringBuilder();

        // Header
        instructions.AppendLine("--- DeveloperMemory Intelligence Context ---");
        instructions.AppendLine();

        // Intent summary (for downstream models to understand context)
        instructions.AppendLine($"[Task Analysis] Intent: {analysis.Intent} | Type: {analysis.TaskType}");
        if (!string.IsNullOrWhiteSpace(analysis.UserGoal))
        {
            instructions.AppendLine($"Goal: {analysis.UserGoal}");
        }
        instructions.AppendLine();

        // Context sections in order
        foreach (var section in sections.OrderBy(s => s.Order))
        {
            instructions.AppendLine($"## {section.Heading}");
            instructions.AppendLine();

            foreach (var item in section.Items)
            {
                if (!string.IsNullOrEmpty(item.Label))
                {
                    instructions.AppendLine($"({item.Label})");
                }
                instructions.AppendLine(item.Content);
                instructions.AppendLine();
            }
        }

        // Technical context if present
        if (analysis.TechnicalContext.Count > 0)
        {
            instructions.AppendLine("## Technical Context");
            instructions.AppendLine(string.Join(", ", analysis.TechnicalContext));
            instructions.AppendLine();
        }

        // Profile context (if provided by the caller)
        if (!string.IsNullOrWhiteSpace(profileContext))
        {
            instructions.AppendLine(profileContext);
            instructions.AppendLine();
        }

        // Knowledge context (if provided by the caller)
        if (!string.IsNullOrWhiteSpace(knowledgeContext))
        {
            instructions.AppendLine(knowledgeContext);
            instructions.AppendLine();
        }

        instructions.AppendLine("--- End Intelligence Context ---");
        instructions.AppendLine();

        // The composed prompt keeps the original request prominent
        var composedPrompt = instructions.ToString() +
            Environment.NewLine +
            "User Request: " + originalRequest;

        return new PromptCompositionResult
        {
            Instructions = instructions.ToString(),
            ComposedPrompt = composedPrompt
        };
    }
}
