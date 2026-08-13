namespace DeveloperMemory.Api.Models;

public class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}