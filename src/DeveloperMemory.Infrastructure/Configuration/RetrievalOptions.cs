using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Infrastructure.Configuration;

/// <summary>
/// Configuration for selecting the retrieval provider used by
/// <see cref="DeveloperMemory.Application.Services.MemoryRetrievalService"/>.
///
/// Selection maps the configured mode onto the EXISTING providers:
///   Keyword (default) → KeywordRetrievalProvider
///   Semantic          → SemanticRetrievalProvider (embedding + vector store)
///   Hybrid            → HybridRetrievalProvider  (keyword + semantic, merged)
///   Auto              → HybridRetrievalProvider (which falls back to
///                        lexical-only results when the semantic leg is
///                        unavailable — its documented behavior)
///
/// The default is Keyword so existing deployments never start consuming
/// embedding/vector resources unexpectedly.
/// </summary>
public class RetrievalOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Retrieval";

    /// <summary>
    /// Retrieval mode: "Keyword" (or "Lexical"), "Semantic", "Hybrid", or "Auto".
    /// Case-insensitive. Unknown/empty values fall back to Keyword.
    /// </summary>
    public string Mode { get; set; } = "Keyword";

    /// <summary>
    /// The configured mode parsed into the domain <see cref="RetrievalMode"/> enum.
    /// Accepts both "Keyword" and "Lexical" for the keyword mode.
    /// Fail-safe: any unrecognized value resolves to Lexical (keyword-only).
    /// </summary>
    public RetrievalMode ResolvedMode => Mode?.Trim().ToLowerInvariant() switch
    {
        "keyword" or "lexical" => RetrievalMode.Lexical,
        "semantic" => RetrievalMode.Semantic,
        "hybrid" => RetrievalMode.Hybrid,
        "auto" => RetrievalMode.Auto,
        _ => RetrievalMode.Lexical
    };
}
