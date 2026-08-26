using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.DTOs;

namespace DeveloperMemory.Api.Abstractions;

/// <summary>
/// Provider-independent boundary for retrieving relevant memory and knowledge context
/// for a given request query. Implementations may combine multiple retrieval sources
/// (persistent memory, knowledge documents, future vector search, etc.) behind this abstraction.
/// </summary>
public interface IMemoryRetriever
{
    /// <summary>
    /// Retrieves relevant context for a query from all available retrieval sources.
    /// </summary>
    /// <param name="query">The search query derived from the user's request.</param>
    /// <param name="project">Optional project filter for knowledge documents.</param>
    /// <param name="tags">Optional tag filter for knowledge documents.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing matching memories and knowledge documents.</returns>
    Task<MemoryRetrievalResult> RetrieveContextAsync(
        string query,
        string? project = null,
        List<string>? tags = null,
        CancellationToken ct = default);
}
