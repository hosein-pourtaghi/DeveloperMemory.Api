namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Represents a prompt intelligence profile.
/// Configures how the Prompt Intelligence Engine behaves for different scenarios.
/// </summary>
public class PromptProfile
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Profile name (e.g., "DefaultDeveloper", "Debugging").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Profile description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Profile version for tracking changes.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Whether this profile is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>JSON-serialized profile configuration.</summary>
    public string ConfigurationJson { get; set; } = "{}";

    /// <summary>When this profile was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this profile was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the profile configuration.
    /// </summary>
    public PromptProfileConfiguration GetConfiguration()
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<PromptProfileConfiguration>(ConfigurationJson)
                   ?? new PromptProfileConfiguration();
        }
        catch
        {
            return new PromptProfileConfiguration();
        }
    }

    /// <summary>
    /// Sets the profile configuration.
    /// </summary>
    public void SetConfiguration(PromptProfileConfiguration config)
    {
        ConfigurationJson = System.Text.Json.JsonSerializer.Serialize(config);
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Configuration for a prompt profile.
/// </summary>
public class PromptProfileConfiguration
{
    /// <summary>Intent analysis policy.</summary>
    public IntentPolicyConfig IntentPolicy { get; set; } = new();

    /// <summary>Memory retrieval policy.</summary>
    public MemoryPolicyConfig MemoryPolicy { get; set; } = new();

    /// <summary>Context selection policy.</summary>
    public ContextPolicyConfig ContextPolicy { get; set; } = new();

    /// <summary>Optimization policy.</summary>
    public OptimizationPolicyConfig OptimizationPolicy { get; set; } = new();

    /// <summary>Token budget for this profile.</summary>
    public int TokenBudget { get; set; } = 4000;

    /// <summary>Required model capabilities.</summary>
    public List<string> ModelRequirements { get; set; } = [];
}

/// <summary>
/// Intent analysis policy configuration.
/// </summary>
public class IntentPolicyConfig
{
    /// <summary>Whether to use LLM for intent analysis.</summary>
    public bool UseLlmAnalysis { get; set; }

    /// <summary>Minimum LLM confidence to override deterministic.</summary>
    public double MinLlmConfidence { get; set; } = 0.85;
}

/// <summary>
/// Memory retrieval policy configuration.
/// </summary>
public class MemoryPolicyConfig
{
    /// <summary>Whether to include memory context.</summary>
    public bool IncludeMemory { get; set; } = true;

    /// <summary>Maximum memories to retrieve.</summary>
    public int MaxMemories { get; set; } = 10;

    /// <summary>Memory types to prioritize.</summary>
    public List<string> PrioritizeTypes { get; set; } = [];

    /// <summary>Whether to include low-confidence memories.</summary>
    public bool IncludeLowConfidence { get; set; }
}

/// <summary>
/// Context selection policy configuration.
/// </summary>
public class ContextPolicyConfig
{
    /// <summary>Whether to include project context.</summary>
    public bool IncludeProjectContext { get; set; } = true;

    /// <summary>Whether to include architecture rules.</summary>
    public bool IncludeArchitectureRules { get; set; } = true;

    /// <summary>Whether to include coding conventions.</summary>
    public bool IncludeCodingConventions { get; set; } = true;

    /// <summary>Context priority weights.</summary>
    public Dictionary<string, double> PriorityWeights { get; set; } = [];
}

/// <summary>
/// Optimization policy configuration.
/// </summary>
public class OptimizationPolicyConfig
{
    /// <summary>Optimization mode: Deterministic, LLM, Auto, Disabled.</summary>
    public string Mode { get; set; } = "Auto";

    /// <summary>Whether to validate LLM output.</summary>
    public bool ValidateOutput { get; set; } = true;

    /// <summary>Maximum optimization attempts.</summary>
    public int MaxAttempts { get; set; } = 1;
}
