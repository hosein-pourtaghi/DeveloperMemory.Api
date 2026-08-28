using FluentAssertions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// Tests for persistent security audit trail storage.
/// </summary>
public class AuditRepositoryTests : IDisposable
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly AuditRepository _repository;

    public AuditRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DeveloperMemoryDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new AuditRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task AppendAsync_PersistsEntry()
    {
        var entry = CreateEntry("AuthenticationSuccess", "Success", "user-1");

        await _repository.AppendAsync(entry);

        var results = await _repository.GetRecentAsync(1);
        results.Should().HaveCount(1);
        results[0].EventType.Should().Be("AuthenticationSuccess");
        results[0].OwnerId.Should().Be("user-1");
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsInDescendingOrder()
    {
        await _repository.AppendAsync(CreateEntry("Event1", "Success", null, DateTime.UtcNow.AddMinutes(-5)));
        await _repository.AppendAsync(CreateEntry("Event2", "Failure", null, DateTime.UtcNow.AddMinutes(-2)));
        await _repository.AppendAsync(CreateEntry("Event3", "Success", null, DateTime.UtcNow));

        var results = await _repository.GetRecentAsync(10);

        results.Should().HaveCount(3);
        results[0].EventType.Should().Be("Event3");
        results[1].EventType.Should().Be("Event2");
        results[2].EventType.Should().Be("Event1");
    }

    [Fact]
    public async Task GetRecentAsync_RespectsCountLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repository.AppendAsync(CreateEntry("Event", "Success"));
        }

        var results = await _repository.GetRecentAsync(3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByEventTypeAsync_FiltersCorrectly()
    {
        await _repository.AppendAsync(CreateEntry("AuthenticationSuccess", "Success"));
        await _repository.AppendAsync(CreateEntry("AuthenticationFailure", "Failure"));
        await _repository.AppendAsync(CreateEntry("KeyCreated", "Success"));
        await _repository.AppendAsync(CreateEntry("AuthenticationFailure", "Failure"));

        var authFailures = await _repository.GetByEventTypeAsync("AuthenticationFailure");

        authFailures.Should().HaveCount(2);
        authFailures.All(e => e.EventType == "AuthenticationFailure").Should().BeTrue();
    }

    [Fact]
    public async Task GetByOwnerIdAsync_FiltersCorrectly()
    {
        await _repository.AppendAsync(CreateEntry("Auth", "Success", "user-1"));
        await _repository.AppendAsync(CreateEntry("Auth", "Success", "user-2"));
        await _repository.AppendAsync(CreateEntry("Auth", "Success", "user-1"));

        var user1Events = await _repository.GetByOwnerIdAsync("user-1");

        user1Events.Should().HaveCount(2);
        user1Events.All(e => e.OwnerId == "user-1").Should().BeTrue();
    }

    [Fact]
    public async Task AppendAsync_DoesNotStoreRawSecrets()
    {
        var entry = CreateEntry("KeyCreated", "Success", "user-1");
        entry.FailureReason = "Should not contain raw keys";

        await _repository.AppendAsync(entry);

        var results = await _repository.GetRecentAsync(1);
        results[0].FailureReason.Should().NotContain("dm_"); // No raw API key prefix
        // SecurityAuditLogEntry should not have fields for raw secrets
        typeof(SecurityAuditLogEntry).GetProperties()
            .Should().NotContain(p =>
                p.Name.Contains("RawKey", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase),
            "Audit entity should not have sensitive authentication material fields");
    }

    [Fact]
    public async Task AppendAsync_PreservesMetadata()
    {
        var entry = CreateEntry("KeyRotated", "Success", "user-1");
        entry.MetadataJson = "{\"PreviousKeyId\":\"abc-123\"}";

        await _repository.AppendAsync(entry);

        var results = await _repository.GetRecentAsync(1);
        results[0].MetadataJson.Should().Contain("PreviousKeyId");
    }

    // ── Helpers ──

    private static SecurityAuditLogEntry CreateEntry(
        string eventType, string outcome, string? ownerId = null,
        DateTime? occurredAt = null)
    {
        return new SecurityAuditLogEntry
        {
            OccurredAt = occurredAt ?? DateTime.UtcNow,
            EventType = eventType,
            Outcome = outcome,
            OwnerId = ownerId,
            SourceIp = "127.0.0.1"
        };
    }
}
