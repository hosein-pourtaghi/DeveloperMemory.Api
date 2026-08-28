namespace DeveloperMemory.Api.Infrastructure.Security;

/// <summary>
/// Structured security audit trail for authentication, key lifecycle, and authorization events.
/// Does not store raw secrets or sensitive authentication material.
/// </summary>
public interface ISecurityAuditService
{
    void RecordEvent(SecurityAuditEvent evt);
    IReadOnlyList<SecurityAuditEvent> GetRecentEvents(int count = 100);
    IReadOnlyList<SecurityAuditEvent> GetEventsByType(SecurityEventType type, int count = 50);
}
