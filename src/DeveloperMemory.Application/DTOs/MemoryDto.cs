using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

public class MemoryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryScope Scope { get; set; }
    public MemoryState State { get; set; }
    public MemoryType MemoryType { get; set; }
    public DataClassification Classification { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? Source { get; set; }
    public List<string> Tags { get; set; } = [];
    public Guid? SupersededById { get; set; }
    public Guid? SupersedesId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double Importance { get; set; }
    public double Confidence { get; set; }
    public int Version { get; set; }
}
