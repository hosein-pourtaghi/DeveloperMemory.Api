using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Api.Infrastructure.Security;

/// <summary>
/// In-memory implementation of the security audit trail.
/// Events are kept in a bounded circular buffer. For production, replace with persistent storage.
/// Does not store raw API keys, Authorization headers, passwords, or tokens.
/// </summary>
public class InMemorySecurityAuditService : ISecurityAuditService
{
    private readonly ConcurrentBag<SecurityAuditEvent> _events = new();
    private readonly ILogger<InMemorySecurityAuditService> _logger;
    private const int MaxEvents = 10_000;

    public InMemorySecurityAuditService(ILogger<InMemorySecurityAuditService> logger)
    {
        _logger = logger;
    }

    public void RecordEvent(SecurityAuditEvent evt)
    {
        _events.Add(evt);

        // Log security-significant events
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

    public IReadOnlyList<SecurityAuditEvent> GetRecentEvents(int count = 100)
    {
        return _events
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToList();
    }

    public IReadOnlyList<SecurityAuditEvent> GetEventsByType(SecurityEventType type, int count = 50)
    {
        return _events
            .Where(e => e.EventType == type)
            .OrderByDescending(e => e.OccurredAt)
            .Take(count)
            .ToList();
    }
}
