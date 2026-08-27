using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Centralized memory retrieval service.
/// Accepts a structured retrieval request and returns privacy-aware,
/// lifecycle-filtered, ranked, and budget-constrained memory context.
/// </summary>
public interface IMemoryRetrievalService
{
    /// <summary>
    /// Retrieves memories eligible for the given context, ranked by relevance
    /// and constrained by the context budget.
    /// </summary>
    Task<RetrievedMemoriesResult> RetrieveAsync(
        RetrievalRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Builds a complete PromptContext from the retrieval request.
    /// This is the primary entry point for the prompt enrichment pipeline.
    /// </summary>
    Task<PromptContext> BuildPromptContextAsync(
        RetrievalRequest request,
        CancellationToken ct = default);
}
