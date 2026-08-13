namespace DeveloperMemory.Api.Models;

public class PromptRequest
{
    public string? Query { get; set; }
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
    public string? ProfileId { get; set; }
    public string? SystemPrompt { get; set; }
}