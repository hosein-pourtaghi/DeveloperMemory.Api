using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of prompt intelligence audit trail.
/// Records significant events without sensitive content.
/// </summary>
public class PromptIntelligenceAudit : IPromptIntelligenceAudit
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<PromptIntelligenceAudit> _logger;

    public PromptIntelligenceAudit(
        DeveloperMemoryDbContext context,
        ILogger<PromptIntelligenceAudit> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordEventAsync(PromptAuditEvent auditEvent, CancellationToken ct = default)
    {
        auditEvent.Id = auditEvent.Id == Guid.Empty ? Guid.NewGuid() : auditEvent.Id;
        auditEvent.CreatedAt = DateTime.UtcNow;

        _context.PromptAuditEvents.Add(auditEvent);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Audit event recorded: {EventType} (CorrelationId: {CorrelationId})",
            auditEvent.EventType, auditEvent.CorrelationId);
    }

    public async Task<IReadOnlyList<PromptAuditEvent>> GetEventsByCorrelationAsync(
        string correlationId,
        CancellationToken ct = default)
    {
        return await _context.PromptAuditEvents
            .Where(e => e.CorrelationId == correlationId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PromptAuditEvent>> GetRecentEventsAsync(
        int count = 100,
        CancellationToken ct = default)
    {
        return await _context.PromptAuditEvents
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }
}
