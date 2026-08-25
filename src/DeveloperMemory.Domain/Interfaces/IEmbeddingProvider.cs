using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Provider-independent abstraction for generating text embeddings.
/// Implementations may use OpenAI, local models, or any compatible provider.
/// The application layer must not reference specific providers.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Provider name for observability and diagnostics.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this provider is currently configured and available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// The embedding profile identifying this provider's vector space.
    /// </summary>
    EmbeddingProfile Profile { get; }

    /// <summary>
    /// Generates an embedding for the given text.
    /// Returns null embedding with error message on failure.
    /// Never throws for operational failures — returns failed result.
    /// </summary>
    Task<EmbeddingResult> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for multiple texts in a single request.
    /// More efficient than calling GenerateAsync multiple times.
    /// </summary>
    Task<IReadOnlyList<EmbeddingResult>> GenerateBatchAsync(
        IReadOnlyCollection<string> texts,
        CancellationToken cancellationToken = default);
}
