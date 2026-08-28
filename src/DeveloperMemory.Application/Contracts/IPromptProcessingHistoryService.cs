using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provides ownership-aware access to prompt processing history.
/// </summary>
public interface IPromptProcessingHistoryService
{
    Task<IReadOnlyList<PromptProcessingRecord>> GetRecentAsync(
        string ownerId,
        int maxResults = 50,
        CancellationToken ct = default);

    Task<IReadOnlyList<PromptProcessingRecord>> QueryAsync(
        string ownerId,
        Guid? profileId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? optimizationMode = null,
        string? validationStatus = null,
        bool? fallbackUsed = null,
        int maxResults = 50,
        CancellationToken ct = default);

    Task<PromptProcessingRecord?> GetByIdAsync(
        Guid id,
        string ownerId,
        CancellationToken ct = default);
}
