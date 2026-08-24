namespace DeveloperMemory.Api.Models;

public class CreateDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
}
