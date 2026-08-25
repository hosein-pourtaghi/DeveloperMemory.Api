using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Analyzes an incoming user request and produces a structured PromptAnalysis.
/// The initial implementation is deterministic (keyword-based).
/// Replaceable with LLM-assisted analysis in the future.
/// </summary>
public interface IPromptAnalyzer
{
    /// <summary>
    /// Analyzes the incoming request and returns structured intent, task type,
    /// keywords, and constraints.
    /// </summary>
    PromptAnalysis Analyze(string request, PromptContext? context = null);
}
