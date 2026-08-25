namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Abstraction for prompt history retention and cleanup.
/// Provider-independent; does not mandate a background job framework.
/// </summary>
public interface IPromptHistoryRetentionService
{
    /// <summary>
    /// Removes processing records older than the specified retention period.
    /// </summary>
    Task<int> CleanupExpiredRecordsAsync(
        TimeSpan retentionPeriod,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of records eligible for cleanup.
    /// </summary>
    Task<int> GetExpiredRecordCountAsync(
        TimeSpan retentionPeriod,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the total count of processing records.
    /// </summary>
    Task<int> GetTotalRecordCountAsync(
        CancellationToken ct = default);
}
