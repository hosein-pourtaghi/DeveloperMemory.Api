using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

public class CreateMemoryRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryScope Scope { get; set; } = MemoryScope.Global;
    public DataClassification Classification { get; set; } = DataClassification.Internal;
    public Guid? ProjectId { get; set; }
    public string? WorkspaceId { get; set; }
    public string? UserId { get; set; }
    public string? Source { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double Importance { get; set; } = 0.5;
}
