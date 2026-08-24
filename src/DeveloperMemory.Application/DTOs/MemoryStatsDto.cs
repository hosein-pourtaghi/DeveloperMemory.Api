using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.DTOs;

public class MemoryStatsDto
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int ExpiredCount { get; set; }
    public int SupersededCount { get; set; }
    public int ArchivedCount { get; set; }
    public int GlobalCount { get; set; }
    public int ProjectCount { get; set; }
    public int WorkspaceCount { get; set; }
    public int PrivateCount { get; set; }
    public Dictionary<MemoryScope, int> ByScope { get; set; } = [];
    public Dictionary<MemoryState, int> ByState { get; set; } = [];
}
