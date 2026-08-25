using System.Security.Cryptography;
using System.Text;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// In-memory embedding provider for testing and development.
/// Generates deterministic hash-based pseudo-embeddings.
/// NOT suitable for production semantic search — use only for testing.
/// 
/// The vectors are deterministic: same input always produces same output.
/// Similar inputs may not produce similar vectors (unlike real embeddings).
/// </summary>
public class InMemoryEmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimensions;
    private readonly ILogger<InMemoryEmbeddingProvider> _logger;

    public InMemoryEmbeddingProvider(int dimensions = 128, ILogger<InMemoryEmbeddingProvider>? logger = null)
    {
        _dimensions = dimensions;
        _logger = logger ?? new LoggerFactory().CreateLogger<InMemoryEmbeddingProvider>();
    }

    public string ProviderName => "in-memory";

    public bool IsAvailable => true;

    public EmbeddingProfile Profile => new()
    {
        Provider = "in-memory",
        Model = "hash-based",
        Dimensions = _dimensions
    };

    public Task<EmbeddingResult> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Text is required"
            });
        }

        try
        {
            var values = GenerateDeterministicVector(text);
            var embedding = new Embedding
            {
                Values = values,
                Provider = ProviderName,
                Model = "hash-based",
                CreatedAt = DateTime.UtcNow
            };

            return Task.FromResult(new EmbeddingResult
            {
                Embedding = embedding,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-memory embedding generation failed");
            return Task.FromResult(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    public Task<IReadOnlyList<EmbeddingResult>> GenerateBatchAsync(
        IReadOnlyCollection<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = texts.Select(t => GenerateAsync(t, cancellationToken).Result).ToList();
        return Task.FromResult<IReadOnlyList<EmbeddingResult>>(results);
    }

    private float[] GenerateDeterministicVector(string text)
    {
        var values = new float[_dimensions];

        // Use SHA256 to generate deterministic values
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        for (int i = 0; i < _dimensions; i++)
        {
            // Use hash bytes cyclically to fill dimensions
            var hashIndex = i % hash.Length;
            var nextIndex = (i + 1) % hash.Length;

            // Combine two bytes to create a value in [-1, 1]
            var raw = (hash[hashIndex] << 8) | hash[nextIndex];
            values[i] = (raw / 32768.0f) - 1.0f;
        }

        // Normalize to unit vector
        var magnitude = 0.0f;
        foreach (var v in values) magnitude += v * v;
        magnitude = MathF.Sqrt(magnitude);

        if (magnitude > 0)
        {
            for (int i = 0; i < values.Length; i++)
                values[i] /= magnitude;
        }

        return values;
    }
}
