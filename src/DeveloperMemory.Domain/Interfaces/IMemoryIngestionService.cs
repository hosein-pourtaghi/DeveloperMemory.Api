using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Service for ingesting new memory into the system.
/// Handles normalization, duplicate detection, conflict detection,
/// and lifecycle decisions. Returns structured results explaining
/// what happened.
/// </summary>
public interface IMemoryIngestionService
{
    /// <summary>
    /// Ingests a memory candidate into the system.
    /// Returns a structured result describing the outcome.
    /// </summary>
    Task<MemoryIngestionResult> IngestAsync(
        MemoryIngestionRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Request to ingest a memory into the system.
/// </summary>
public class MemoryIngestionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryScope Scope { get; set; } = MemoryScope.Global;
    public MemoryType MemoryType { get; set; } = MemoryType.Other;
    public DataClassification Classification { get; set; } = DataClassification.Internal;
    public Guid? ProjectId { get; set; }
    public string? WorkspaceId { get; set; }
    public string? UserId { get; set; }
    public string? Source { get; set; }
    public List<string>? Tags { get; set; }
    public double Importance { get; set; } = 0.5;
    public double Confidence { get; set; } = 1.0;
    public DateTime? ExpiresAt { get; set; }
    public string? MetadataJson { get; set; }
}

/// <summary>
/// Result of a memory ingestion operation.
/// </summary>
public class MemoryIngestionResult
{
    public MemoryIngestionOutcome Outcome { get; set; }
    public MemoryEntry? Memory { get; set; }
    public MemoryEntry? RelatedMemory { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool ConflictDetected { get; set; }
    public bool DuplicateDetected { get; set; }
    public bool WasPersisted { get; set; }
}

/// <summary>
/// Possible outcomes of a memory ingestion operation.
/// </summary>
public enum MemoryIngestionOutcome
{
    Created,
    Updated,
    Merged,
    SupersededExisting,
    IgnoredDuplicate,
    Rejected,
    RequiresReview
}
