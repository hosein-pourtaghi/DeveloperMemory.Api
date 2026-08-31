using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Provider-neutral intelligence package produced by the Prompt Intelligence Engine.
/// Contains the complete context, analysis, constraints, and structured prompt
/// ready for consumption by any downstream LLM provider, agent, or execution system.
/// 
/// This is NOT an OpenAI request, Anthropic request, or vendor-specific object.
/// It is an internal intelligence representation.
/// </summary>
public class PromptPackage
{
    /// <summary>
    /// The original user request that triggered this package.
    /// Always preserved exactly, even in degraded/failed states.
    /// </summary>
    public string OriginalRequest { get; set; } = string.Empty;

    /// <summary>
    /// Structured analysis of the request.
    /// </summary>
    public PromptAnalysis Analysis { get; set; } = new();

    /// <summary>
    /// Resolved and deduplicated constraints, ordered by precedence.
    /// Explicit constraints from the current request are always preserved
    /// when safely available, even if later stages failed.
    /// </summary>
    public List<PromptConstraint> Constraints { get; set; } = [];

    /// <summary>
    /// Organized context sections (rules, project context, memory, etc.).
    /// </summary>
    public List<ContextSection> ContextSections { get; set; } = [];

    /// <summary>
    /// The composed prompt instruction text (system message content).
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// The final optimized prompt text, ready for the downstream provider.
    /// When optimization fails, this falls back to the composed prompt.
    /// </summary>
    public string OptimizedPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Retrieval metadata from Phase 3 pipeline.
    /// </summary>
    public RetrievalMetadata RetrievalMetadata { get; set; } = new();

    /// <summary>
    /// Intelligence engine metadata for observability.
    /// </summary>
    public PromptIntelligenceMetadata Metadata { get; set; } = new();

    /// <summary>
    /// The project context identifier.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// The workspace context identifier.
    /// </summary>
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// The user context identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    // ── Reliability contract ──

    /// <summary>
    /// Processing status: Full, Degraded, or Failed.
    /// Callers should check this to determine whether the package is complete.
    /// </summary>
    public PromptIntelligenceStatus Status { get; set; } = PromptIntelligenceStatus.Full;

    /// <summary>
    /// The pipeline stage that failed, if Status is Degraded or Failed.
    /// Null when Status is Full.
    /// </summary>
    public PromptIntelligenceStage? FailedStage { get; set; }

    /// <summary>
    /// Human-readable warnings about degradation.
    /// Safe for logging. Does not contain sensitive memory content.
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Reasons for degradation (e.g., "retrieval_unavailable", "optimization_failed").
    /// Structured for programmatic consumption.
    /// </summary>
    public List<string> DegradationReasons { get; set; } = [];

    /// <summary>
    /// Whether the original request was preserved exactly.
    /// Should always be true. Included as a contract invariant check.
    /// </summary>
    public bool OriginalRequestPreserved { get; set; } = true;

    /// <summary>
    /// Whether explicit constraints from the current request are present.
    /// Should be true when any explicit constraints were available.
    /// </summary>
    public bool ConstraintsPreserved { get; set; } = true;
}

/// <summary>
/// Metadata about the Prompt Intelligence Engine's processing.
/// Used for observability, diagnostics, and debugging.
/// </summary>
public class PromptIntelligenceMetadata
{
    /// <summary>
    /// Duration of prompt analysis (ms).
    /// </summary>
    public double AnalysisDurationMs { get; set; }

    /// <summary>
    /// Duration of conversational memory ingestion (ms).
    /// Zero if ingestion was not triggered or was skipped.
    /// </summary>
    public double IngestionDurationMs { get; set; }

    /// <summary>
    /// Whether conversational memory ingestion detected durable information.
    /// </summary>
    public bool IngestionDetected { get; set; }

    /// <summary>
    /// Number of memories created by conversational ingestion.
    /// </summary>
    public int IngestionCreatedCount { get; set; }

    /// <summary>
    /// Number of duplicates detected during ingestion.
    /// </summary>
    public int IngestionDuplicateCount { get; set; }

    /// <summary>
    /// Number of supersessions performed during ingestion.
    /// </summary>
    public int IngestionSupersededCount { get; set; }

    /// <summary>
    /// Duration of constraint resolution (ms).
    /// </summary>
    public double ConstraintDurationMs { get; set; }

    /// <summary>
    /// Duration of memory context assembly (ms).
    /// </summary>
    public double ContextAssemblyDurationMs { get; set; }

    /// <summary>
    /// Duration of prompt composition (ms).
    /// </summary>
    public double CompositionDurationMs { get; set; }

    /// <summary>
    /// Duration of prompt optimization (ms).
    /// </summary>
    public double OptimizationDurationMs { get; set; }

    /// <summary>
    /// Total engine processing duration (ms).
    /// </summary>
    public double TotalDurationMs { get; set; }

    /// <summary>
    /// Number of candidate memories received from retrieval.
    /// </summary>
    public int CandidateMemoryCount { get; set; }

    /// <summary>
    /// Number of memories after deduplication and refinement.
    /// </summary>
    public int RefinedMemoryCount { get; set; }

    /// <summary>
    /// Number of duplicates detected and removed.
    /// </summary>
    public int DuplicatesRemoved { get; set; }

    /// <summary>
    /// Number of constraint conflicts detected.
    /// </summary>
    public int ConflictsDetected { get; set; }

    /// <summary>
    /// Number of resolved constraints.
    /// </summary>
    public int ConstraintsResolved { get; set; }

    /// <summary>
    /// Final prompt character count after optimization (or composition if optimization failed).
    /// </summary>
    public int FinalPromptLength { get; set; }

    /// <summary>
    /// Number of context sections created.
    /// </summary>
    public int ContextSectionCount { get; set; }

    /// <summary>
    /// Processing status from the engine.
    /// </summary>
    public PromptIntelligenceStatus Status { get; set; } = PromptIntelligenceStatus.Full;

    /// <summary>
    /// Pipeline stage that failed, if any.
    /// </summary>
    public PromptIntelligenceStage? FailedStage { get; set; }

    /// <summary>
    /// Warnings generated during processing.
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Degradation reasons for observability.
    /// </summary>
    public List<string> DegradationReasons { get; set; } = [];
}
