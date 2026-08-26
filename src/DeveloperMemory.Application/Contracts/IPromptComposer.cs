using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Composes structured prompt text from organized context sections,
/// constraints, and the original request.
/// The first implementation is deterministic and template-based.
/// </summary>
public interface IPromptComposer
{
    /// <summary>
    /// Composes a structured prompt from the provided context.
    /// Returns the instructions text and the final composed prompt.
    /// </summary>
    PromptCompositionResult Compose(
        PromptAnalysis analysis,
        List<PromptConstraint> constraints,
        List<ContextSection> sections,
        string originalRequest,
        string? profileContext = null,
        string? knowledgeContext = null);
}

/// <summary>
/// Result of prompt composition.
/// </summary>
public class PromptCompositionResult
{
    /// <summary>
    /// The assembled instructions text (system-level context).
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// The final composed prompt including the original request.
    /// </summary>
    public string ComposedPrompt { get; set; } = string.Empty;
}
