namespace DeveloperMemory.Api.Infrastructure.Configuration;

public class AppSettings
{
    public FreeLlmApiSettings FreeLlmApi { get; set; } = new();
    public PathSettings Paths { get; set; } = new();
    public ModelSelectionSettings ModelSelection { get; set; } = new();
}

public class FreeLlmApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default model used when auto-selection is disabled or mode is unrecognized.
    /// </summary>
    public string DefaultModel { get; set; } = "auto";

    /// <summary>
    /// Maximum time allowed for an upstream request, in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

public class ModelSelectionSettings
{
    /// <summary>
    /// When true, the gateway selects PlanModel or BuildModel based on detected mode
    /// only when the client did not provide a model.
    /// An explicit client model always takes precedence.
    /// </summary>
    public bool AutoSelectModel { get; set; } = true;

    /// <summary>
    /// Model to use when the request is detected as a planning/reasoning task.
    /// </summary>
    public string PlanModel { get; set; } = "auto:smart";

    /// <summary>
    /// Model to use when the request is detected as an implementation/build task.
    /// </summary>
    public string BuildModel { get; set; } = "auto:fast";
}

public class PathSettings
{
    public string KnowledgeFolder { get; set; } = string.Empty;
    public string ProfilesFolder { get; set; } = string.Empty;
    public string RequestLogFolder { get; set; } = "./logs/requests";
}
