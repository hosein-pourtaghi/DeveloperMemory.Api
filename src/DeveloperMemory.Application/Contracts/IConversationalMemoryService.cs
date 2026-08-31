using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Orchestrates conversational memory ingestion: detects durable information in
/// user messages, extracts structured candidates, and persists them using the
/// existing memory infrastructure. All failures are non-fatal.
/// </summary>
public interface IConversationalMemoryService
{
    /// <summary>
    /// Attempts to detect and persist memories from a conversational message.
    /// This is a fire-and-forget enrichment — failures must not block the chat pipeline.
    /// </summary>
    /// <param name="message">The user message to analyze.</param>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="projectId">Optional explicit project context.</param>
    /// <param name="workspaceId">Optional explicit workspace context.</param>
    /// <param name="tags">Optional explicit tags.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the ingestion attempt.</returns>
    Task<ConversationalMemoryIngestionResult> TryIngestAsync(
        string message,
        string userId,
        Guid? projectId = null,
        string? workspaceId = null,
        List<string>? tags = null,
        List<string>? conversationHistory = null,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a conversational memory ingestion attempt.
/// </summary>
public class ConversationalMemoryIngestionResult
{
    /// <summary>Whether durable information was detected in the message.</summary>
    public bool Detected { get; set; }

    /// <summary>Whether a memory was successfully persisted.</summary>
    public bool Persisted { get; set; }

    /// <summary>Number of memories created.</summary>
    public int CreatedCount { get; set; }

    /// <summary>Number of duplicates detected (not persisted).</summary>
    public int DuplicateCount { get; set; }

    /// <summary>Number of conflicts detected and resolved.</summary>
    public int SupersededCount { get; set; }

    /// <summary>Whether the ingestion failed (non-fatal).</summary>
    public bool Failed { get; set; }

    /// <summary>Failure reason if applicable.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Warnings generated during ingestion.</summary>
    public List<string> Warnings { get; set; } = [];
}
