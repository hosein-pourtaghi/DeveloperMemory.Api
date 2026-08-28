using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Application use case for safely reading prompt processing history.
/// </summary>
public sealed class PromptProcessingHistoryService : IPromptProcessingHistoryService
{
    private const int MaximumResults = 100;
    private readonly IPromptProcessingRecordRepository _repository;

    public PromptProcessingHistoryService(IPromptProcessingRecordRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PromptProcessingRecord>> GetRecentAsync(
        string ownerId,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        return QueryAsync(ownerId, maxResults: maxResults, ct: ct);
    }

    public async Task<IReadOnlyList<PromptProcessingRecord>> QueryAsync(
        string ownerId,
        Guid? profileId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? optimizationMode = null,
        string? validationStatus = null,
        bool? fallbackUsed = null,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return [];

        return await _repository.QueryAsync(
            ownerId,
            profileId,
            from,
            to,
            optimizationMode,
            validationStatus,
            fallbackUsed,
            Math.Clamp(maxResults, 1, MaximumResults),
            ct);
    }

    public async Task<PromptProcessingRecord?> GetByIdAsync(
        Guid id,
        string ownerId,
        CancellationToken ct = default)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(ownerId))
            return null;

        return await _repository.GetByIdAsync(id, ownerId, ct);
    }
}
