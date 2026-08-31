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
/// Pipeline: Request → Analysis → Memory Ingestion → Constraints → Memory Retrieval
///          → Deduplication → Organization → Composition → Optimization → PromptPackage
/// </summary>
public interface IPromptIntelligenceEngine
{
    /// <summary>
    /// Processes a raw request through the full intelligence pipeline and produces
    /// a complete PromptPackage ready for downstream consumption.
    ///
    /// The optional profileContext and knowledgeContext parameters allow the caller
    /// to provide pre-formatted additional context (developer profiles, knowledge
    /// documents) that the engine includes in the composed prompt alongside its
    /// own intelligence-derived context.
    ///
    /// The optional tags parameter provides client-supplied tags for memory scope
    /// inference. When absent, the engine uses available conversational context.
    /// </summary>
    Task<PromptPackage> ProcessAsync(
        string userRequest,
        string userId,
        Guid? projectId = null,
        string? workspaceId = null,
        int contextTokenBudget = 4000,
        string? profileContext = null,
        string? knowledgeContext = null,
        List<string>? tags = null,
        List<string>? conversationHistory = null,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a request with a pre-built PromptContext (for callers that
    /// have already run retrieval).
    /// </summary>
    PromptPackage ProcessWithContext(
        string userRequest,
        PromptContext context);
}
