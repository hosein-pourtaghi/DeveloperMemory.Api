using System.Text;
using System.Text.RegularExpressions;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

public class MemoryEntry : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? NormalizedContent { get; set; }
    public MemoryScope Scope { get; set; } = MemoryScope.Global;
    public MemoryState State { get; set; } = MemoryState.Active;
    public MemoryType MemoryType { get; set; } = MemoryType.Other;
    public DataClassification Classification { get; set; } = DataClassification.Internal;
    public Guid? ProjectId { get; set; }
    public string? WorkspaceId { get; set; }
    public string? UserId { get; set; }
    /// <summary>
    /// Server-controlled owner identifier. Derived from the authenticated principal.
    /// All memory access is filtered by this field. Never set from client input.
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? TagsJson { get; set; }
    public Guid? SupersededById { get; set; }
    public Guid? SupersedesId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; } = 0;
    public DateTime? ExpiresAt { get; set; }
    public double Importance { get; set; } = 0.5;
    public double Confidence { get; set; } = 1.0;
    public int Version { get; set; } = 1;
    public string? MetadataJson { get; set; }

    public Project? Project { get; set; }
    public MemoryEntry? SupersededBy { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    public bool IsActive => State == MemoryState.Active;

    public List<string> Tags =>
        string.IsNullOrEmpty(TagsJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(TagsJson) ?? [];

    public void SetTags(List<string> tags)
    {
        TagsJson = System.Text.Json.JsonSerializer.Serialize(tags);
    }

    /// <summary>
    /// Compute normalized content for duplicate detection.
    /// Lowercases, strips punctuation, collapses whitespace.
    /// </summary>
    public string ComputeNormalizedContent()
    {
        var text = $"{Title} {Content}".ToLowerInvariant();
        // Remove punctuation except alphanumeric and spaces
        text = Regex.Replace(text, @"[^\w\s]", " ");
        // Collapse multiple whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    // ── Lifecycle transitions ──

    public void Supersede(Guid supersededById)
    {
        ValidateTransition(MemoryState.Superseded);
        State = MemoryState.Superseded;
        SupersededById = supersededById;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Expire()
    {
        ValidateTransition(MemoryState.Expired);
        State = MemoryState.Expired;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Archive()
    {
        ValidateTransition(MemoryState.Archived);
        State = MemoryState.Archived;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void SoftDelete()
    {
        ValidateTransition(MemoryState.Deleted);
        State = MemoryState.Deleted;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void MarkAccessed()
    {
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
    }

    /// <summary>
    /// Validates that the requested state transition is allowed.
    /// Throws InvalidOperationException for invalid transitions.
    /// </summary>
    private void ValidateTransition(MemoryState targetState)
    {
        if (!IsValidTransition(State, targetState))
        {
            throw new InvalidOperationException(
                $"Invalid memory state transition from {State} to {targetState}. " +
                $"Memory {Id} cannot perform this operation.");
        }
    }

    /// <summary>
    /// Defines the valid state transitions for the memory lifecycle.
    /// </summary>
    public static bool IsValidTransition(MemoryState from, MemoryState to)
    {
        return from switch
        {
            MemoryState.Active => to is MemoryState.Updated or MemoryState.Superseded
                or MemoryState.Expired or MemoryState.Archived or MemoryState.Deleted,
            MemoryState.Updated => to is MemoryState.Active or MemoryState.Superseded
                or MemoryState.Expired or MemoryState.Archived or MemoryState.Deleted,
            MemoryState.Archived => to is MemoryState.Active or MemoryState.Deleted,
            MemoryState.Superseded => false, // Terminal state
            MemoryState.Expired => false,     // Terminal state
            MemoryState.Deleted => false,     // Terminal state
            _ => false
        };
    }
}
