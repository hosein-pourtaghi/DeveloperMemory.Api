using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

public class MemoryEntry : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryScope Scope { get; set; } = MemoryScope.Global;
    public MemoryState State { get; set; } = MemoryState.Active;
    public DataClassification Classification { get; set; } = DataClassification.Internal;
    public Guid? ProjectId { get; set; }
    public string? Source { get; set; }
    public string? TagsJson { get; set; }
    public Guid? SupersededById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public double Importance { get; set; } = 0.5;
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

    public void Supersede(Guid supersededById)
    {
        State = MemoryState.Superseded;
        SupersededById = supersededById;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        State = MemoryState.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        State = MemoryState.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        State = MemoryState.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }
}
