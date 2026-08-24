using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

public class UpdateMemoryRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public MemoryState? State { get; set; }
    public DataClassification? Classification { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double? Importance { get; set; }
}
