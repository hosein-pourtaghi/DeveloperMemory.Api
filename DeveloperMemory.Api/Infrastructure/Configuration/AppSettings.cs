namespace DeveloperMemory.Api.Infrastructure.Configuration;

public class AppSettings
{
    public FreeLlmApiSettings FreeLlmApi { get; set; } = new();
    public PathSettings Paths { get; set; } = new();
}

public class FreeLlmApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default model routing mode for FreeLLM API requests.
    /// Supported values:
    ///   "auto"       — Router picks the best available model
    ///   "auto:fast"  — Router picks the fastest available model
    ///   "auto:smart" — Router picks the most capable available model
    ///   "fusion"     — Multiple models answer in parallel, a judge synthesizes one answer
    ///   "gpt-4"      — Any explicit model ID from the FreeLLM catalog
    /// Can be overridden per-request via the Model property on PromptRequest.
    /// </summary>
    public string DefaultModel { get; set; } = "auto";
}

public class PathSettings
{
    public string KnowledgeFolder { get; set; } = string.Empty;
    public string ProfilesFolder { get; set; } = string.Empty;
}
