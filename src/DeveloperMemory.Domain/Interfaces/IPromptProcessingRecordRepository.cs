using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

public interface IPromptProcessingRecordRepository
{
    Task<IReadOnlyList<PromptProcessingRecord>> GetRecentAsync(
        string ownerId,
        int count = 50,
        CancellationToken ct = default);

    Task<PromptProcessingRecord?> GetByIdAsync(
        Guid id,
        string ownerId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PromptProcessingRecord>> QueryAsync(
        string ownerId,
        Guid? profileId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? optimizationMode = null,
        string? validationStatus = null,
        bool? fallbackUsed = null,
        int maxResults = 100,
        CancellationToken ct = default);
}
