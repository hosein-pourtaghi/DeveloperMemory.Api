namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Optimizes a composed prompt for clarity, efficiency, and quality.
/// The first implementation is deterministic (removes duplicates, normalizes whitespace,
/// enforces structure). Replaceable with LLM-assisted optimization in the future.
/// </summary>
public interface IPromptOptimizer
{
    /// <summary>
    /// Optimizes the prompt text. Returns the optimized prompt.
    /// Must not change the semantic meaning of the content.
    /// </summary>
    string Optimize(string prompt);
}
