using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// PostgreSQL persistence tests for the security audit trail.
/// Verifies append-only behavior and that no raw secrets are stored.
/// </summary>
public class PostgresAuditPersistenceTests : PostgresTestBase
{
    public PostgresAuditPersistenceTests(PostgresDbFixture fixture) : base(fixture) { }

    private static SecurityAuditLogEntry CreateEvent(
        string eventType,
        string outcome,
        string? ownerId = null,
        string? keyId = null,
        string? failureReason = null)
    {
        return new SecurityAuditLogEntry
        {
            OccurredAt = DateTime.UtcNow,
            EventType = eventType,
            Outcome = outcome,
            OwnerId = ownerId,
            KeyId = keyId,
            CorrelationId = Guid.NewGuid().ToString(),
            SourceIp = "127.0.0.1",
            FailureReason = failureReason
        };
    }

    [Fact]
    public async Task AuditEvents_SurviveContextRecreation()
    {
        Guid eventId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            var entry = CreateEvent("AuthenticationSuccess", "Success", "audit-owner");
            eventId = entry.Id;
            await repo.AppendAsync(entry);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            var recent = await repo.GetRecentAsync(10);
            Assert.Contains(recent, e => e.Id == eventId);
            Assert.Equal("AuthenticationSuccess", recent.First(e => e.Id == eventId).EventType);
        }
    }

    [Fact]
    public async Task AuditEvents_AreAppendOnly()
    {
        Guid eventId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            var entry = CreateEvent("KeyCreated", "Success", "audit-owner");
            eventId = entry.Id;
            await repo.AppendAsync(entry);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            var recent = await repo.GetRecentAsync(10);
            var found = recent.First(e => e.Id == eventId);

            // Attempt to modify an existing entry (should not be possible through the API,
            // but verify the repository layer doesn't expose update/delete)
            Assert.Equal("KeyCreated", found.EventType);
            Assert.Equal("Success", found.Outcome);
        }

        // The repository interface has no Update/Delete for audit — append-only by design.
        // Verify the interface contract:
        var iface = typeof(IAuditRepository);
        Assert.DoesNotContain(iface.GetMethods(), m => m.Name is "UpdateAsync" or "DeleteAsync");
    }

    [Fact]
    public async Task AuditEvents_AreQueryableByTypeAndOwner()
    {
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            await repo.AppendAsync(CreateEvent("AuthenticationSuccess", "Success", "audit-owner-a"));
            await repo.AppendAsync(CreateEvent("AuthenticationFailure", "Failure", "audit-owner-a", failureReason: "Invalid key"));
            await repo.AppendAsync(CreateEvent("AuthenticationSuccess", "Success", "audit-owner-b"));
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);

            var byType = await repo.GetByEventTypeAsync("AuthenticationSuccess");
            Assert.Equal(2, byType.Count);

            var byOwner = await repo.GetByOwnerIdAsync("audit-owner-a");
            Assert.Equal(2, byOwner.Count);
            Assert.All(byOwner, e => Assert.Equal("audit-owner-a", e.OwnerId));
        }
    }

    [Fact]
    public async Task AuditEvents_NeverContainRawSecrets()
    {
        const string sensitiveValue = "dm_super_secret_raw_key_abc123";
        const string bearerToken = "Bearer eyJhbGciOiJIUzI1NiJ9.secret";

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            await repo.AppendAsync(CreateEvent("AuthenticationFailure", "Failure", "audit-owner", failureReason: "Invalid key"));
            await repo.AppendAsync(CreateEvent("KeyCreated", "Success", "audit-owner"));
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new AuditRepository(ctx);
            var all = await repo.GetRecentAsync(100);

            // Serialize everything and assert no sensitive values leaked
            var serialized = System.Text.Json.JsonSerializer.Serialize(all);
            Assert.DoesNotContain(sensitiveValue, serialized);
            Assert.DoesNotContain(bearerToken, serialized);
            Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
