namespace DeveloperMemory.Application.Configuration;

/// <summary>
/// Configuration for the V2-3 agent registry.
///
/// Agents may be defined in the "Agents" configuration section. When the
/// section is empty, the registry falls back to its built-in default agent
/// ("assistant"), so the system works without configuration and remains
/// extensible through configuration alone.
///
/// Example:
///   "Agents": {
///     "Agents": [
///       { "AgentId": "writer", "Name": "Writer", "SystemInstructions": "...", "Enabled": true }
///     ]
///   }
/// </summary>
public class AgentRegistryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Agents";

    /// <summary>Configured agent definitions.</summary>
    public List<AgentDefinitionOptions> Agents { get; set; } = [];
}

/// <summary>
/// One configured agent definition.
/// Mirrors the <c>Agent</c> contract with binding-friendly (string) types.
/// </summary>
public class AgentDefinitionOptions
{
    /// <summary>Stable agent identifier.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Human-readable name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>System/behavior instructions.</summary>
    public string SystemInstructions { get; set; } = string.Empty;

    /// <summary>Whether the agent can execute.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Optional AgentType classification hint (parsed case-insensitively).</summary>
    public string? AgentType { get; set; }

    /// <summary>Optional metadata.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}