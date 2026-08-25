using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.DTOs;

/// <summary>
/// The result of a memory retrieval operation.
/// </summary>
public class RetrievedMemoriesResult
{
    /// <summary>
    /// The retrieved memories, ordered by relevance (highest first).
    /// </summary>
    public List<RetrievedMemory> Memories { get; set; } = [];

    /// <summary>
    /// Metadata about the retrieval process.
    /// </summary>
    public RetrievalMetadata Metadata { get; set; } = new();
}
