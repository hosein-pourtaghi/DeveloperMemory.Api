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

    Task<PromptProcessingRecord?> GetByIdAsync(
        Guid id,
        string ownerId,
        CancellationToken ct = default);
}
