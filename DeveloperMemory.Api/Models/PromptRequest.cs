namespace DeveloperMemory.Api.Models;

public class PromptRequest
{
    public string? Query { get; set; }
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
    public string? ProfileId { get; set; }
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Optional model override for this request.
    /// Supports FreeLLM routing modes:
    ///   "auto"       — Router picks the best available model (default if null)
    ///   "auto:fast"  — Router picks the fastest available model
    ///   "auto:smart" — Router picks the most capable available model
    ///   "fusion"     — Multiple models answer in parallel, a judge synthesizes one answer
    ///   "gpt-4"      — Any explicit model ID from the FreeLLM catalog
    /// When null, uses the configured DefaultModel from appsettings.json.
    /// </summary>
    public string? Model { get; set; }
}

public class CreateDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Project { get; set; }
    public List<string>? Tags { get; set; }
}
