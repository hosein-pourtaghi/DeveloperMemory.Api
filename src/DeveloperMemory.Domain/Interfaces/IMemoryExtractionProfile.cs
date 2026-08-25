namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Identifies how a memory was extracted.
/// Analogous to EmbeddingProfile but for extraction processing.
/// </summary>
public class ExtractionProfile
{
    /// <summary>The extraction provider name (e.g., "openai", "deterministic").</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The model used for extraction.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Version of the extraction prompt/schema.</summary>
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>Version of the extraction output schema.</summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>Creates a key that identifies this extraction profile.</summary>
    public string GetProfileKey() => $"{Provider}/{Model}/{PromptVersion}/{SchemaVersion}";
}
