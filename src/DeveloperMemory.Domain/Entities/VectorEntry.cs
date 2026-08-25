using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents a stored vector in the vector database.
/// Separated from MemoryEntry to avoid coupling the domain to vector infrastructure.
/// </summary>
public class VectorEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The memory this vector is associated with.</summary>
    public Guid MemoryId { get; set; }

    /// <summary>The embedding provider name (e.g., "openai", "local").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The embedding model name (e.g., "text-embedding-3-small").</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Optional version/revision of the embedding model.</summary>
    public string? Version { get; set; }

    /// <summary>Number of dimensions in the vector.</summary>
    public int Dimensions { get; set; }

    /// <summary>
    /// The embedding vector stored as a float array.
    /// In PostgreSQL + pgvector, this maps to a vector column.
    /// The framework handles serialization/deserialization.
    /// </summary>
    public float[] Vector { get; set; } = [];

    /// <summary>When this vector was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this vector was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hash of the text content used to generate this embedding.
    /// Used for staleness detection.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;
}
