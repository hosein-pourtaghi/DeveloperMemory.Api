namespace DeveloperMemory.Application.DTOs;

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
