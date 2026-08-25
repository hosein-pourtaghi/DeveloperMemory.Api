namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents an embedding vector with provider metadata.
/// Treated as provider output — not a domain invariant.
/// </summary>
public class Embedding
{
    /// <summary>The raw embedding values.</summary>
    public float[] Values { get; set; } = [];

    /// <summary>Number of dimensions in the vector.</summary>
    public int Dimensions => Values.Length;

    /// <summary>The embedding provider name (e.g., "openai", "local").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The embedding model name (e.g., "text-embedding-3-small").</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Version/revision of the embedding model, if available.</summary>
    public string? Version { get; set; }

    /// <summary>When this embedding was generated.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates that this embedding has non-empty, finite values.
    /// </summary>
    public bool IsValid()
    {
        if (Values.Length == 0) return false;
        foreach (var v in Values)
        {
            if (!float.IsFinite(v)) return false;
        }
        return true;
    }
}

/// <summary>
/// Identifies the embedding space/profile.
/// Used to prevent comparing vectors from incompatible models.
/// </summary>
public class EmbeddingProfile
{
    /// <summary>Provider name.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Model name.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Optional version.</summary>
    public string? Version { get; set; }

    /// <summary>Expected vector dimensions.</summary>
    public int Dimensions { get; set; }

    /// <summary>Creates a key that identifies this embedding space.</summary>
    public string GetProfileKey() => $"{Provider}/{Model}/{Version ?? "latest"}/{Dimensions}";

    public bool Equals(EmbeddingProfile? other)
    {
        if (other is null) return false;
        return GetProfileKey() == other.GetProfileKey();
    }
}

/// <summary>
/// Result of embedding generation.
/// </summary>
public class EmbeddingResult
{
    public Embedding? Embedding { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double GenerationDurationMs { get; set; }
}
