namespace DeveloperMemory.Api.Models;

public class DeveloperProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public string Experience { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}