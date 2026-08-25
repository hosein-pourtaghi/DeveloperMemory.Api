using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// In-memory audit trail for testing and in-memory mode.
/// </summary>
public class InMemoryPromptAudit : IPromptIntelligenceAudit
{
    private readonly List<PromptAuditEvent> _events = [];
    private readonly object _lock = new();

    public Task RecordEventAsync(PromptAuditEvent auditEvent, CancellationToken ct = default)
    {
        auditEvent.Id = auditEvent.Id == Guid.Empty ? Guid.NewGuid() : auditEvent.Id;
        auditEvent.CreatedAt = DateTime.UtcNow;

        lock (_lock)
        {
            _events.Add(auditEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PromptAuditEvent>> GetEventsByCorrelationAsync(
        string correlationId,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptAuditEvent> result = _events
                .Where(e => e.CorrelationId == correlationId)
                .OrderBy(e => e.CreatedAt)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<PromptAuditEvent>> GetRecentEventsAsync(
        int count = 100,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<PromptAuditEvent> result = _events
                .OrderByDescending(e => e.CreatedAt)
                .Take(count)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
