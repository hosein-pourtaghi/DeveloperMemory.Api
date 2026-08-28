using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Application use case for safely reading prompt processing history.
/// </summary>
public sealed class PromptProcessingHistoryService : IPromptProcessingHistoryService
{
    private readonly IPromptProcessingRecordRepository _repository;

    public PromptProcessingHistoryService(        IPromptProcessingRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PromptProcessingRecord>> GetRecentAsync(
        string ownerId,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return [];

        var records = await _repository.GetRecentAsync(100, ct);
        return records
            .Where(record => string.Equals(record.UserId, ownerId, StringComparison.Ordinal))
            .Take(Math.Clamp(maxResults, 1, 100))
            .ToList();
    }

    public async Task<PromptProcessingRecord?> GetByIdAsync(
        Guid id,
        string ownerId,
        CancellationToken ct = default)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(ownerId))
            return null;

        var record = await _repository.GetByIdAsync(id, ct);
        return record != null && string.Equals(record.UserId, ownerId, StringComparison.Ordinal)
            ? record
            : null;
    }
}
