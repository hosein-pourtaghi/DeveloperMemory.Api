namespace DeveloperMemory.Api.Infrastructure.Configuration;

/// <summary>
/// Configuration for diagnostic log persistence.
/// Controls whether HTTP diagnostics are persisted to PostgreSQL.
/// Default: PersistToDatabase = false (disabled).
/// </summary>
public class DiagnosticsSettings
{
    public const string SectionName = "Diagnostics";

    /// <summary>
    /// Whether to persist diagnostic log entries to PostgreSQL.
    /// Default: false. Enable for debugging/investigation.
    /// </summary>
    public bool PersistToDatabase { get; set; } = false;
}
