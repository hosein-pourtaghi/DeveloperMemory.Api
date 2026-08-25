namespace DeveloperMemory.Infrastructure.Configuration;

/// <summary>
/// Configuration for LLM-assisted memory intelligence.
/// When disabled, deterministic extraction only is used.
/// </summary>
public class MemoryIntelligenceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MemoryIntelligence";

    /// <summary>Whether LLM-assisted extraction is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Extraction mode: Deterministic, LLM, or Auto.</summary>
    public string ExtractionMode { get; set; } = "Auto";

    /// <summary>LLM provider for memory extraction.</summary>
    public string ExtractionProvider { get; set; } = "openai";

    /// <summary>LLM model for memory extraction.</summary>
    public string ExtractionModel { get; set; } = "gpt-4o-mini";

    /// <summary>LLM provider for conflict detection.</summary>
    public string ConflictProvider { get; set; } = "openai";

    /// <summary>LLM model for conflict detection.</summary>
    public string ConflictModel { get; set; } = "gpt-4o-mini";

    /// <summary>Maximum candidates per extraction request.</summary>
    public int MaxCandidatesPerRequest { get; set; } = 10;

    /// <summary>Maximum conflict checks per ingestion.</summary>
    public int MaxConflictChecks { get; set; } = 5;

    /// <summary>Maximum LLM calls per request.</summary>
    public int MaxLLMCallsPerRequest { get; set; } = 2;

    /// <summary>Extraction timeout in seconds.</summary>
    public int ExtractionTimeoutSeconds { get; set; } = 30;

    /// <summary>Conflict detection timeout in seconds.</summary>
    public int ConflictTimeoutSeconds { get; set; } = 15;

    /// <summary>Minimum confidence threshold for auto-persist.</summary>
    public double MinConfidenceForAutoPersist { get; set; } = 0.7;

    /// <summary>Confidence below which requires human review.</summary>
    public double ReviewThreshold { get; set; } = 0.5;

    /// <summary>Prompt version for extraction schema tracking.</summary>
    public string PromptVersion { get; set; } = "1.0";

    /// <summary>Schema version for extraction output.</summary>
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>Whether the LLM extraction provider is available.</summary>
    public bool IsAvailable => Enabled &&
        !string.IsNullOrEmpty(ExtractionProvider) &&
        !string.IsNullOrEmpty(ExtractionModel);
}
