using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

public interface IPromptProcessingRecordRepository
{
    Task<IReadOnlyList<PromptProcessingRecord>> GetRecentAsync(int count = 50, CancellationToken ct = default);
    Task<PromptProcessingRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
