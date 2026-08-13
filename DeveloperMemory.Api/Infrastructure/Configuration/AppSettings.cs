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
}

public class PathSettings
{
    public string KnowledgeFolder { get; set; } = string.Empty;
    public string ProfilesFolder { get; set; } = string.Empty;
}