namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// Indicates the processing status of a PromptPackage.
/// Used for observability, diagnostics, and gateway behavior decisions.
/// </summary>
public enum PromptIntelligenceStatus
{
    /// <summary>
    /// All pipeline stages completed successfully.
    /// The package contains full analysis, constraints, context, and optimization.
    /// </summary>
    Full,

    /// <summary>
    /// One or more non-critical pipeline stages failed or were skipped.
    /// The package contains valid, safe content from successfully completed stages.
    /// The original request and explicit constraints are always preserved.
    /// </summary>
    Degraded,

    /// <summary>
    /// A non-recoverable failure occurred. The package may contain minimal content
    /// but should not be used for prompt construction. Callers should handle this
    /// as an error condition.
    /// </summary>
    Failed
}

/// <summary>
/// Identifies which pipeline stage failed or was skipped.
/// Used for diagnostics and observability.
/// </summary>
public enum PromptIntelligenceStage
{
    /// <summary>Prompt analysis stage.</summary>
    Analysis,

    /// <summary>Memory retrieval stage.</summary>
    Retrieval,

    /// <summary>Constraint resolution stage.</summary>
    ConstraintResolution,

    /// <summary>Memory context assembly stage.</summary>
    ContextAssembly,

    /// <summary>Prompt composition stage.</summary>
    Composition,

    /// <summary>Prompt optimization stage.</summary>
    Optimization
}
