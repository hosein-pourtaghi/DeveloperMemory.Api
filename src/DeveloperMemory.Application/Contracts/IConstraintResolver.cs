using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Resolves and deduplicates constraints from multiple sources
/// (current request, project rules, user preferences, memory).
/// Enforces deterministic precedence: higher-precedence constraints override
/// lower-precedence ones when they conflict.
/// </summary>
public interface IConstraintResolver
{
    /// <summary>
    /// Resolves constraints from the analysis, prompt context, and project context.
    /// Returns a deduplicated, precedence-ordered list of constraints.
    /// </summary>
    List<PromptConstraint> Resolve(
        PromptAnalysis analysis,
        PromptContext? context,
        List<string>? projectRules = null);
}
