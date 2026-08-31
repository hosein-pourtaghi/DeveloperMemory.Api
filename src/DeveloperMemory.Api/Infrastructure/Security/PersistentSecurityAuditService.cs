using System.Text.Json;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Api.Infrastructure.Security;

/// <summary>
/// Production security audit service that persists events to PostgreSQL.
/// Maps between the in-memory SecurityAuditEvent model and the persistent SecurityAuditLogEntry.
/// Does not store raw API keys, passwords, tokens, or other sensitive authentication material.
/// </summary>
public class PersistentSecurityAuditService : ISecurityAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<PersistentSecurityAuditService> _logger;

    public PersistentSecurityAuditService(
        IAuditRepository auditRepository,
        ILogger<PersistentSecurityAuditService> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public void RecordEvent(SecurityAuditEvent evt)
    {
        // Fire-and-forget: persist asynchronously. Failures are logged but don't block requests.
        _ = PersistEventAsync(evt);
    }

    public IReadOnlyList<SecurityAuditEvent> GetRecentEvents(int count = 100)
    {
        // Synchronous wrapper — blocks on the async call for interface compatibility
        return GetRecentEventsAsync(count).GetAwaiter().GetResult();
    }

    public IReadOnlyList<SecurityAuditEvent> GetEventsByType(SecurityEventType type, int count = 50)
    {
        return GetEventsByTypeAsync(type, count).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyList<SecurityAuditEvent>> GetRecentEventsAsync(int count = 100, CancellationToken ct = default)
    {
        var entries = await _auditRepository.GetRecentAsync(count, ct);
        return entries.Select(MapToEvent).ToList();
    }

    public async Task<IReadOnlyList<SecurityAuditEvent>> GetEventsByTypeAsync(SecurityEventType type, int count = 50, CancellationToken ct = default)
    {
        var entries = await _auditRepository.GetByEventTypeAsync(type.ToString(), count, ct);
        return entries.Select(MapToEvent).ToList();
    }

    private async Task PersistEventAsync(SecurityAuditEvent evt)
    {
        try
        {
            var entry = new SecurityAuditLogEntry
            {
                OccurredAt = evt.OccurredAt,
                EventType = evt.EventType.ToString(),
                Outcome = evt.Outcome.ToString(),
                OwnerId = evt.OwnerId,
                KeyId = evt.KeyId,
                CorrelationId = evt.CorrelationId,
                SourceIp = evt.SourceIp,
                FailureReason = evt.FailureReason,
                MetadataJson = evt.Metadata != null ? JsonSerializer.Serialize(evt.Metadata) : null
            };

            await _auditRepository.AppendAsync(entry);

            // Also log significant events
            var logLevel = evt.Outcome switch
            {
                SecurityEventOutcome.Failure => LogLevel.Warning,
                SecurityEventOutcome.Denied => LogLevel.Warning,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel,
                "[SecurityAudit] {EventType} | Outcome={Outcome} | OwnerId={OwnerId} | KeyId={KeyId} | Reason={FailureReason}",
                evt.EventType, evt.Outcome, evt.OwnerId ?? "n/a", evt.KeyId ?? "n/a", evt.FailureReason ?? "n/a");
        }
        catch (Exception ex)
        {
            // Never let audit failures affect request processing
            _logger.LogError(ex, "Failed to persist security audit event {EventType}", evt.EventType);
        }
    }

    private static SecurityAuditEvent MapToEvent(SecurityAuditLogEntry entry)
    {
        Dictionary<string, string>? metadata = null;
        if (!string.IsNullOrEmpty(entry.MetadataJson))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entry.MetadataJson);
            }
            catch
            {
                // Ignore deserialization failures for audit display
            }
        }

        return new SecurityAuditEvent
        {
            EventId = entry.Id,
            OccurredAt = entry.OccurredAt,
            EventType = Enum.TryParse<SecurityEventType>(entry.EventType, out var type) ? type : SecurityEventType.AuthenticationFailure,
            Outcome = Enum.TryParse<SecurityEventOutcome>(entry.Outcome, out var outcome) ? outcome : SecurityEventOutcome.Failure,
            OwnerId = entry.OwnerId,
            KeyId = entry.KeyId,
            CorrelationId = entry.CorrelationId,
            SourceIp = entry.SourceIp,
            FailureReason = entry.FailureReason,
            Metadata = metadata
        };
    }
}
