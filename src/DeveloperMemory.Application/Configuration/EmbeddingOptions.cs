namespace DeveloperMemory.Application.Configuration;

/// <summary>
/// Configuration for embedding providers.
/// Supports OpenAI-compatible and future providers.
/// </summary>
public class EmbeddingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Embedding";

    /// <summary>Whether semantic/embedding functionality is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Provider name for diagnostics (e.g., "openai", "local").</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>Base URL for the embedding API endpoint.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Embedding model name (e.g., "text-embedding-3-small").</summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>API key for authentication (if required by provider).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Expected embedding dimensions. If 0, determined from provider response.</summary>
    public int Dimensions { get; set; }

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum number of texts per batch request.</summary>
    public int MaxBatchSize { get; set; } = 2048;

    /// <summary>Whether the embedding cache is enabled.</summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>Cache expiration in minutes.</summary>
    public int CacheExpirationMinutes { get; set; } = 1440; // 24 hours

    /// <summary>Rate limit: maximum concurrent requests to the provider.</summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>Rate limit: maximum requests per minute.</summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>Whether the provider requires authentication via API key.</summary>
    public bool RequiresApiKey => !string.IsNullOrEmpty(ApiKey);
}
