using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.DTOs;

namespace DeveloperMemory.Api.Abstractions;

/// <summary>
/// Combined result from all retrieval sources for a given query.
/// Used by the gateway controller to assemble prompt context.
/// </summary>
public sealed class MemoryRetrievalResult
{
    /// <summary>
    /// Persistent memory entries matching the query, ordered by importance then recency.
    /// </summary>
    public List<MemoryDto> Memories { get; init; } = [];

    /// <summary>
    /// Knowledge documents matching the query, ordered by relevance score.
    /// </summary>
    public List<SearchResult> KnowledgeResults { get; init; } = [];
}
