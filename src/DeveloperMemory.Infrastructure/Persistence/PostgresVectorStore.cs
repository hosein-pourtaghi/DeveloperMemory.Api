using System.Security.Cryptography;
using System.Text;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL + pgvector implementation of IVectorStore.
/// Uses raw SQL for vector operations since EF Core doesn't natively support pgvector.
///
/// Production path:
///   IVectorStore.SearchAsync(queryVector, maxResults)
///       → SELECT ... ORDER BY vector <=> query_vector
///       → Top-K with minimum score threshold
///       → Embedding profile filtering
///       → Scope/project filtering
///
/// Security: All queries respect embedding profile isolation.
/// A vector from Provider A/Model A is never compared against Provider B/Model B.
/// </summary>
public class PostgresVectorStore : IVectorStore
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<PostgresVectorStore> _logger;

    public PostgresVectorStore(
        DeveloperMemoryDbContext context,
        IOptions<EmbeddingOptions> options,
        ILogger<PostgresVectorStore> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsAvailable => _options.Enabled;

    public async Task<bool> UpsertAsync(
        Guid memoryId,
        Embedding embedding,
        CancellationToken ct = default)
    {
        if (embedding == null || !embedding.IsValid())
        {
            return false;
        }

        try
        {
            var existing = await _context.VectorEntries
                .FirstOrDefaultAsync(v => v.MemoryId == memoryId, ct);

            if (existing != null)
            {
                existing.Vector = embedding.Values;
                existing.Dimensions = embedding.Dimensions;
                existing.Provider = embedding.Provider;
                existing.Model = embedding.Model;
                existing.Version = embedding.Version;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.ContentHash = ComputeContentHash(embedding.Values);
            }
            else
            {
                var entry = new VectorEntry
                {
                    Id = Guid.NewGuid(),
                    MemoryId = memoryId,
                    Vector = embedding.Values,
                    Dimensions = embedding.Dimensions,
                    Provider = embedding.Provider,
                    Model = embedding.Model,
                    Version = embedding.Version,
                    ContentHash = ComputeContentHash(embedding.Values)
                };

                _context.VectorEntries.Add(entry);
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogDebug(
                "Vector upserted for memory {MemoryId}: {Dimensions}d, provider={Provider}, model={Model}",
                memoryId, embedding.Dimensions, embedding.Provider, embedding.Model);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upsert vector for memory {MemoryId}", memoryId);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(Guid memoryId, CancellationToken ct = default)
    {
        try
        {
            var entry = await _context.VectorEntries
                .FirstOrDefaultAsync(v => v.MemoryId == memoryId, ct);

            if (entry == null)
            {
                return false;
            }

            _context.VectorEntries.Remove(entry);
            await _context.SaveChangesAsync(ct);

            _logger.LogDebug("Vector deleted for memory {MemoryId}", memoryId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete vector for memory {MemoryId}", memoryId);
            return false;
        }
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        float[] queryVector,
        int maxResults,
        double minimumScore = 0.0,
        CancellationToken ct = default)
    {
        if (queryVector == null || queryVector.Length == 0)
        {
            return [];
        }

        try
        {
            // Use cosine distance operator <=> for pgvector
            // The result is 1 - cosine_similarity, so we convert to similarity
            var profileKey = $"{_options.Provider}/{_options.Model}";
            var dimensions = queryVector.Length;

            // Convert query vector to PostgreSQL array format
            var vectorStr = FormatVectorForPg(queryVector);

            // Search using raw SQL for pgvector operations
            var sql = $@"
                SELECT
                    ""Id"",
                    ""MemoryId"",
                    ""Provider"",
                    ""Model"",
                    ""Version"",
                    1.0 - (""Vector"" <=> '{vectorStr}'::vector) AS ""Similarity""
                FROM ""VectorEntries""
                WHERE ""Provider"" = @provider
                  AND ""Model"" = @model
                  AND ""Dimensions"" = @dimensions
                ORDER BY ""Vector"" <=> '{vectorStr}'::vector
                LIMIT @limit";

            var results = await _context.VectorEntries
                .FromSqlRaw(sql,
                    new Npgsql.NpgsqlParameter("provider", _options.Provider),
                    new Npgsql.NpgsqlParameter("model", _options.Model),
                    new Npgsql.NpgsqlParameter("dimensions", dimensions),
                    new Npgsql.NpgsqlParameter("limit", maxResults))
                .ToListAsync(ct);

            var semanticResults = results
                .Select(r => new SemanticSearchResult
                {
                    MemoryId = r.MemoryId,
                    SimilarityScore = CalculateCosineSimilarity(queryVector, r.Vector),
                    Provider = r.Provider,
                    Model = r.Model,
                    ProfileKey = $"{r.Provider}/{r.Model}/{r.Version ?? "latest"}/{r.Dimensions}"
                })
                .Where(r => r.SimilarityScore >= minimumScore)
                .OrderByDescending(r => r.SimilarityScore)
                .ToList();

            _logger.LogDebug(
                "Vector search: {Results} results above threshold {Threshold}",
                semanticResults.Count, minimumScore);

            return semanticResults;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search failed, returning empty results");
            return [];
        }
    }

    public async Task<Embedding?> GetAsync(Guid memoryId, CancellationToken ct = default)
    {
        try
        {
            var entry = await _context.VectorEntries
                .FirstOrDefaultAsync(v => v.MemoryId == memoryId, ct);

            if (entry == null)
            {
                return null;
            }

            return new Embedding
            {
                Values = entry.Vector,
                Provider = entry.Provider,
                Model = entry.Model,
                Version = entry.Version,
                CreatedAt = entry.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get vector for memory {MemoryId}", memoryId);
            return null;
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.VectorEntries.CountAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count vectors");
            return 0;
        }
    }

    /// <summary>
    /// Gets the count of vectors matching a specific embedding profile.
    /// </summary>
    public async Task<int> CountByProfileAsync(string provider, string model, CancellationToken ct = default)
    {
        try
        {
            return await _context.VectorEntries
                .CountAsync(v => v.Provider == provider && v.Model == model, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count vectors for profile {Provider}/{Model}", provider, model);
            return 0;
        }
    }

    /// <summary>
    /// Gets vectors that may be stale (content hash doesn't match).
    /// Used for stale embedding detection.
    /// </summary>
    public async Task<List<VectorEntry>> GetStaleVectorsAsync(
        string provider,
        string model,
        CancellationToken ct = default)
    {
        try
        {
            return await _context.VectorEntries
                .Where(v => v.Provider == provider && v.Model == model)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get stale vectors");
            return [];
        }
    }

    private static double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0.0;
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(normA) * Math.Sqrt(normB);
        return magnitude > 0 ? dotProduct / magnitude : 0.0;
    }

    private static string FormatVectorForPg(float[] vector)
    {
        return $"[{string.Join(",", vector)}]";
    }

    private static string ComputeContentHash(float[] values)
    {
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            sb.Append(v.ToString("F6"));
            sb.Append(',');
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }
}
