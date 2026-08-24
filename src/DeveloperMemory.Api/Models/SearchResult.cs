namespace DeveloperMemory.Api.Models;

public class SearchResult
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public double Score { get; set; }
    public string FilePath { get; set; } = string.Empty;
}