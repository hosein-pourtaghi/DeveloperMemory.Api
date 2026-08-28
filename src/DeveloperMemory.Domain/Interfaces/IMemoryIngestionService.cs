using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Interfaces;

public interface IMemoryIngestionService
{
    Task<MemoryIngestionResult> IngestAsync(
        MemoryIngestionRequest request,
        string ownerId,
        CancellationToken ct = default);
}

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
