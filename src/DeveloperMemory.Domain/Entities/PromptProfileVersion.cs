namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Immutable version of a prompt profile configuration.
/// Every configuration change creates a new version.
/// Historical versions are never overwritten.
/// </summary>
public class PromptProfileVersion
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>The profile this version belongs to.</summary>
    public Guid PromptProfileId { get; set; }

    /// <summary>Version number (1, 2, 3, ...).</summary>
    public int Version { get; set; }

    /// <summary>JSON-serialized profile configuration at this version.</summary>
    public string ConfigurationJson { get; set; } = "{}";

    /// <summary>Whether this is the currently active version.</summary>
    public bool IsActive { get; set; }

    /// <summary>When this version was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Who created this version (user/system identifier).</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>Optional description of the change.</summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// Gets the configuration for this version.
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
}
