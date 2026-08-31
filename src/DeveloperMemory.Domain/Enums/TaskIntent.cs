namespace DeveloperMemory.Domain.Enums;

/// <summary>
/// The high-level intent of the task being performed.
/// Used by the Agent Context Intelligence layer to determine context relevance.
/// </summary>
public enum TaskIntent
{
    /// <summary>Task involves writing, modifying, or generating code.</summary>
    Implement,

    /// <summary>Task involves diagnosing and fixing issues.</summary>
    Debug,

    /// <summary>Task involves system design, architecture decisions, or structural planning.</summary>
    Architecture,

    /// <summary>Task involves memory capture or knowledge extraction.</summary>
    MemoryCapture,

    /// <summary>Task involves querying or retrieving existing context/memories.</summary>
    Query
}
