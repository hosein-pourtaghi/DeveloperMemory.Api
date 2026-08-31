namespace DeveloperMemory.Api.Infrastructure.Security;

public enum SecurityEventType
{
    AuthenticationSuccess,
    AuthenticationFailure,
    InvalidApiKeyAttempt,
    ExpiredApiKeyAttempt,
    RevokedApiKeyAttempt,
    KeyCreated,
    KeyRotated,
    KeyRevoked,
    AuthorizationFailure,
    RateLimitRejected,
    OwnershipViolationAttempt
}

public enum SecurityEventOutcome
{
    Success,
    Failure,
    Denied
}

public class SecurityAuditEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public SecurityEventType EventType { get; init; }
    public SecurityEventOutcome Outcome { get; init; }
    public string? OwnerId { get; init; }
    public string? KeyId { get; init; }
    public string? CorrelationId { get; init; }
    public string? SourceIp { get; init; }
    public string? FailureReason { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
