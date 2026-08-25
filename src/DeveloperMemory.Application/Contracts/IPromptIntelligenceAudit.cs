using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Audit trail for Prompt Intelligence operations.
/// Records significant events without sensitive content.
/// </summary>
public interface IPromptIntelligenceAudit
{
    /// <summary>
    /// Records an audit event.
    /// </summary>
    Task RecordEventAsync(PromptAuditEvent auditEvent, CancellationToken ct = default);

    /// <summary>
    /// Gets audit events for a correlation ID.
    /// </summary>
    Task<IReadOnlyList<PromptAuditEvent>> GetEventsByCorrelationAsync(
        string correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets recent audit events.
    /// </summary>
    Task<IReadOnlyList<PromptAuditEvent>> GetRecentEventsAsync(
        int count = 100,
        CancellationToken ct = default);
}
