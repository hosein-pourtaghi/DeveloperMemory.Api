using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// The central Prompt Intelligence Engine.
/// 
/// Transforms a raw user request + context into a structured, provider-neutral
/// PromptPackage that any downstream LLM provider or agent can consume.
/// 
/// The engine does NOT execute requests. It prepares intelligence/context.
/// 
/// Pipeline: Request → Analysis → Constraints → Memory Retrieval → Refinement
///          → Deduplication → Organization → Composition → Optimization → PromptPackage
/// </summary>
public interface IPromptIntelligenceEngine
{
    /// <summary>
    /// Processes a raw request through the full intelligence pipeline and produces
    /// a complete PromptPackage ready for downstream consumption.
    /// </summary>
    Task<PromptPackage> ProcessAsync(
        string userRequest,
        string userId,
        Guid? projectId = null,
        string? workspaceId = null,
        int contextTokenBudget = 4000,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a request with a pre-built PromptContext (for callers that
    /// have already run retrieval).
    /// </summary>
    PromptPackage ProcessWithContext(
        string userRequest,
        PromptContext context);
}
