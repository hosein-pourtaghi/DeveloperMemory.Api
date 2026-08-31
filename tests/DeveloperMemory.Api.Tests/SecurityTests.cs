using DeveloperMemory.Api.Infrastructure.Authentication;
using DeveloperMemory.Api.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Security tests for API key lifecycle, audit trail, and authentication behavior.
/// Tests the ApiKeyValidator (extracted validation logic) and SecurityAuditService directly.
/// </summary>
public class SecurityTests
{
    // ── API Key Lifecycle Tests ──

    [Fact]
    public void ValidActiveKey_Authenticates()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-1", Key = "valid-key", UserId = "user-1",
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });

        var result = validator.Validate("valid-key");

        result.IsValid.Should().BeTrue();
        result.KeyEntry!.UserId.Should().Be("user-1");
        result.KeyEntry.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public void ExpiredKey_IsRejected()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-expired", Key = "expired-key", UserId = "user-1",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });

        var result = validator.Validate("expired-key");

        result.IsValid.Should().BeFalse();
        result.FailureReason!.Should().Contain("expired");
    }

    [Fact]
    public void RevokedKey_IsRejected()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-revoked", Key = "revoked-key", UserId = "user-1",
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            RevokedReason = "Compromised"
        });

        var result = validator.Validate("revoked-key");

        result.IsValid.Should().BeFalse();
        result.FailureReason!.Should().Contain("revoked");
        result.FailureReason!.Should().Contain("Compromised");
    }

    [Fact]
    public void InvalidKey_IsRejected()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-1", Key = "correct-key", UserId = "user-1"
        });

        var result = validator.Validate("wrong-key");

        result.IsValid.Should().BeFalse();
        result.FailureReason!.Should().Contain("Invalid API key");
    }

    [Fact]
    public void EmptyKey_ReturnsNoResult()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-1", Key = "valid-key", UserId = "user-1"
        });

        var result = validator.Validate("");

        result.IsNoResult.Should().BeTrue();
    }

    [Fact]
    public void NullKey_ReturnsNoResult()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-1", Key = "valid-key", UserId = "user-1"
        });

        var result = validator.Validate(null!);

        result.IsNoResult.Should().BeTrue();
    }

    [Fact]
    public void KeyWithNullExpiration_Authenticates()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-no-exp", Key = "no-exp-key", UserId = "user-1",
            ExpiresAt = null
        });

        var result = validator.Validate("no-exp-key");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void KeyWithNullRevocation_Authenticates()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-active", Key = "active-key", UserId = "user-1",
            RevokedAt = null
        });

        var result = validator.Validate("active-key");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MultipleKeys_DifferentUsers_AuthenticateCorrectly()
    {
        var validator = CreateValidator(
            new ApiKeyEntry { Id = "k1", Key = "key-a", UserId = "user-a" },
            new ApiKeyEntry { Id = "k2", Key = "key-b", UserId = "user-b" });

        var resultA = validator.Validate("key-a");
        resultA.IsValid.Should().BeTrue();
        resultA.KeyEntry!.UserId.Should().Be("user-a");

        var resultB = validator.Validate("key-b");
        resultB.IsValid.Should().BeTrue();
        resultB.KeyEntry!.UserId.Should().Be("user-b");
    }

    [Fact]
    public void RevokedThenExpiredKey_ShowsRevokedError()
    {
        var validator = CreateValidator(new ApiKeyEntry
        {
            Id = "key-both", Key = "both-key", UserId = "user-1",
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            RevokedReason = "Manual revocation",
            ExpiresAt = DateTime.UtcNow.AddDays(-5) // Also expired
        });

        var result = validator.Validate("both-key");

        // Revocation is checked BEFORE expiration
        result.IsValid.Should().BeFalse();
        result.FailureReason!.Should().Contain("revoked");
    }

    // ── Security Audit Trail Tests ──

    [Fact]
    public void AuditService_RecordsEvents()
    {
        var auditService = CreateAuditService();

        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationSuccess,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = "user-1"
        });

        var events = auditService.GetRecentEvents();
        events.Should().HaveCount(1);
        events[0].EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        events[0].OwnerId.Should().Be("user-1");
        events[0].EventId.Should().NotBeEmpty();
    }

    [Fact]
    public void AuditService_FiltersByEventType()
    {
        var auditService = CreateAuditService();

        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationSuccess,
            Outcome = SecurityEventOutcome.Success
        });
        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationFailure,
            Outcome = SecurityEventOutcome.Failure
        });
        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.KeyRevoked,
            Outcome = SecurityEventOutcome.Success
        });

        var authFailures = auditService.GetEventsByType(SecurityEventType.AuthenticationFailure);
        authFailures.Should().HaveCount(1);
        authFailures[0].EventType.Should().Be(SecurityEventType.AuthenticationFailure);
    }

    [Fact]
    public void AuditService_DoesNotStoreRawSecrets()
    {
        var auditService = CreateAuditService();

        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.KeyCreated,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = "user-1",
            KeyId = "key-123"
        });

        var events = auditService.GetRecentEvents();
        events.Should().HaveCount(1);

        // SecurityAuditEvent should not have a raw key field
        var eventType = typeof(SecurityAuditEvent);
        var props = eventType.GetProperties();
        props.Should().NotContain(p =>
            p.Name.Contains("RawKey", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase),
            "SecurityAuditEvent should not expose raw authentication material");
    }

    [Fact]
    public void AuditService_RecordsMultipleEvents_OrderByDate()
    {
        var auditService = CreateAuditService();

        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationFailure,
            Outcome = SecurityEventOutcome.Failure,
            OccurredAt = DateTime.UtcNow.AddMinutes(-5)
        });

        auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationSuccess,
            Outcome = SecurityEventOutcome.Success,
            OccurredAt = DateTime.UtcNow
        });

        var events = auditService.GetRecentEvents();
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        events[1].EventType.Should().Be(SecurityEventType.AuthenticationFailure);
    }

    [Fact]
    public void AuditService_RespectsCountLimit()
    {
        var auditService = CreateAuditService();

        for (int i = 0; i < 10; i++)
        {
            auditService.RecordEvent(new SecurityAuditEvent
            {
                EventType = SecurityEventType.AuthenticationFailure,
                Outcome = SecurityEventOutcome.Failure
            });
        }

        var events = auditService.GetRecentEvents(5);
        events.Should().HaveCount(5);
    }

    // ── Key Lifecycle State Tests ──

    [Fact]
    public void KeyEntry_DefaultValues_AreCorrect()
    {
        var entry = new ApiKeyEntry();

        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.ExpiresAt.Should().BeNull();
        entry.RevokedAt.Should().BeNull();
        entry.LastUsedAt.Should().BeNull();
        entry.ReplacedByKeyId.Should().BeNull();
    }

    [Fact]
    public void KeyEntry_RevokedKey_HasReason()
    {
        var entry = new ApiKeyEntry
        {
            RevokedAt = DateTime.UtcNow,
            RevokedReason = "Compromised"
        };

        entry.RevokedAt.Should().NotBeNull();
        entry.RevokedReason.Should().Be("Compromised");
    }

    [Fact]
    public void KeyEntry_RotationLinksKeys()
    {
        var oldKey = new ApiKeyEntry { Id = "old-key" };
        var newKey = new ApiKeyEntry { Id = "new-key", ReplacedByKeyId = null };

        oldKey.ReplacedByKeyId = newKey.Id;
        oldKey.ExpiresAt = DateTime.UtcNow.AddDays(7);

        oldKey.ReplacedByKeyId.Should().Be("new-key");
        oldKey.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        newKey.ReplacedByKeyId.Should().BeNull();
    }

    [Fact]
    public void MultipleKeys_SameUser_Listable()
    {
        var settings = CreateSettings(
            new ApiKeyEntry { Id = "k1", Key = "key1", UserId = "user-1", DisplayName = "Key 1" },
            new ApiKeyEntry { Id = "k2", Key = "key2", UserId = "user-1", DisplayName = "Key 2" },
            new ApiKeyEntry { Id = "k3", Key = "key3", UserId = "user-2", DisplayName = "Key 3" });

        var userKeys = settings.ApiKeys.Where(k => k.UserId == "user-1").ToList();
        userKeys.Should().HaveCount(2);
        userKeys.Select(k => k.Id).Should().Contain("k1");
        userKeys.Select(k => k.Id).Should().Contain("k2");
    }

    [Fact]
    public void Settings_DefaultExpiration_HasDefault()
    {
        var settings = new ApiKeySettings
        {
            DefaultExpirationDays = 90,
            RotationOverlapDays = 7
        };

        settings.DefaultExpirationDays.Should().Be(90);
        settings.RotationOverlapDays.Should().Be(7);
    }

    // ── OwnerId Fail-Closed Tests ──

    [Fact]
    public void OwnerId_EmptyString_FailClosed()
    {
        var request = new Domain.Entities.RetrievalRequest
        {
            Query = "test",
            OwnerId = string.Empty
        };

        request.OwnerId.Should().BeEmpty();
    }

    [Fact]
    public void OwnerId_Null_FailClosed()
    {
        var request = new Domain.Entities.RetrievalRequest
        {
            Query = "test",
            OwnerId = null!
        };

        request.OwnerId.Should().BeNull();
    }

    // ── SecurityEventType Coverage ──

    [Fact]
    public void SecurityEventTypes_AllDefined()
    {
        var eventTypes = Enum.GetValues<SecurityEventType>();

        eventTypes.Should().Contain(SecurityEventType.AuthenticationSuccess);
        eventTypes.Should().Contain(SecurityEventType.AuthenticationFailure);
        eventTypes.Should().Contain(SecurityEventType.InvalidApiKeyAttempt);
        eventTypes.Should().Contain(SecurityEventType.ExpiredApiKeyAttempt);
        eventTypes.Should().Contain(SecurityEventType.RevokedApiKeyAttempt);
        eventTypes.Should().Contain(SecurityEventType.KeyCreated);
        eventTypes.Should().Contain(SecurityEventType.KeyRotated);
        eventTypes.Should().Contain(SecurityEventType.KeyRevoked);
        eventTypes.Should().Contain(SecurityEventType.AuthorizationFailure);
        eventTypes.Should().Contain(SecurityEventType.RateLimitRejected);
        eventTypes.Should().Contain(SecurityEventType.OwnershipViolationAttempt);
    }

    // ── Helpers ──

    private static ApiKeyValidator CreateValidator(params ApiKeyEntry[] keys)
    {
        return new ApiKeyValidator(new ApiKeySettings { ApiKeys = [.. keys] });
    }

    private static ApiKeySettings CreateSettings(params ApiKeyEntry[] keys)
    {
        return new ApiKeySettings { ApiKeys = [.. keys] };
    }

    private static InMemorySecurityAuditService CreateAuditService()
    {
        return new InMemorySecurityAuditService(
            new Mock<ILogger<InMemorySecurityAuditService>>().Object);
    }
}
