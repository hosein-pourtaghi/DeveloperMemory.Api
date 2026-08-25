namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Tracks the state of a memory's embedding, separate from memory lifecycle.
/// A memory can be Active while its embedding is Failed.
/// </summary>
public enum EmbeddingStatus
{
    /// <summary>Embedding generation is pending/not yet attempted.</summary>
    Pending,

    /// <summary>Embedding is ready for semantic search.</summary>
    Ready,

    /// <summary>Embedding generation failed. Retry possible.</summary>
    Failed,

    /// <summary>Embedding exists but the model has changed. Should be regenerated.</summary>
    Stale,

    /// <summary>No embedding provider is configured or available.</summary>
    Unavailable
}
