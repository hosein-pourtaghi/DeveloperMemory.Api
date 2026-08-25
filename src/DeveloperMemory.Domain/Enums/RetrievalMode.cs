namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Controls how memories are retrieved.
/// </summary>
public enum RetrievalMode
{
    /// <summary>
    /// Use hybrid retrieval when semantic provider is available,
    /// fall back to lexical-only otherwise.
    /// </summary>
    Auto,

    /// <summary>Keyword/lexical retrieval only.</summary>
    Lexical,

    /// <summary>Semantic/vector retrieval only.</summary>
    Semantic,

    /// <summary>Combine lexical and semantic retrieval.</summary>
    Hybrid
}
