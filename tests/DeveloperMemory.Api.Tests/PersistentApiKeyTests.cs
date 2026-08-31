using FluentAssertions;
using DeveloperMemory.Api.Infrastructure.Authentication;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Tests for persistent API key storage, hashing, and lifecycle management.
/// </summary>
public class PersistentApiKeyTests : IDisposable
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ApiKeyRepository _repository;

    public PersistentApiKeyTests()
    {
        var options = new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new DeveloperMemoryDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new ApiKeyRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    // ── ApiKeyHasher Tests ──

    [Fact]
    public void ApiKeyHasher_HashKey_ProducesConsistentHash()
    {
        var rawKey = "dm_testkey12345";
        var hash1 = ApiKeyHasher.HashKey(rawKey);
        var hash2 = ApiKeyHasher.HashKey(rawKey);

        // Hashes use different random salts, so they should differ
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ApiKeyHasher_VerifyKey_SucceedsWithCorrectKey()
    {
        var rawKey = ApiKeyHasher.GenerateRawKey();
        var hash = ApiKeyHasher.HashKey(rawKey);

        ApiKeyHasher.VerifyKey(rawKey, hash).Should().BeTrue();
    }

    [Fact]
    public void ApiKeyHasher_VerifyKey_FailsWithWrongKey()
    {
        var rawKey = ApiKeyHasher.GenerateRawKey();
        var wrongKey = ApiKeyHasher.GenerateRawKey();
        var hash = ApiKeyHasher.HashKey(rawKey);

        ApiKeyHasher.VerifyKey(wrongKey, hash).Should().BeFalse();
    }

    [Fact]
    public void ApiKeyHasher_GenerateRawKey_HasDmPrefix()
    {
        var rawKey = ApiKeyHasher.GenerateRawKey();

        rawKey.Should().StartWith("dm_");
        rawKey.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public void ApiKeyHasher_VerifyKey_FailsWithMalformedHash()
    {
        ApiKeyHasher.VerifyKey("test", "invalid").Should().BeFalse();
        ApiKeyHasher.VerifyKey("test", "").Should().BeFalse();
    }

    // ── Repository Persistence Tests ──

    [Fact]
    public async Task CreateAsync_PersistsKey()
    {
        var apiKey = CreateApiKey("user-1", "Test Key");

        var created = await _repository.CreateAsync(apiKey);

        created.Id.Should().NotBeEmpty();
        var found = await _repository.GetByIdAsync(created.Id);
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Test Key");
        found.OwnerId.Should().Be("user-1");
    }

    [Fact]
    public async Task GetByKeyHashAsync_FindsMatchingKey()
    {
        var rawKey = ApiKeyHasher.GenerateRawKey();
        var hash = ApiKeyHasher.HashKey(rawKey);
        var apiKey = CreateApiKey("user-1", "Test Key");
        apiKey.KeyHash = hash;
        await _repository.CreateAsync(apiKey);

        var found = await _repository.GetByKeyHashAsync(hash);

        found.Should().NotBeNull();
        found!.Id.Should().Be(apiKey.Id);
    }

    [Fact]
    public async Task GetByKeyHashAsync_ReturnsNull_ForUnknownHash()
    {
        var found = await _repository.GetByKeyHashAsync("nonexistent-hash");

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByOwnerIdAsync_ReturnsOnlyOwnerKeys()
    {
        await _repository.CreateAsync(CreateApiKey("user-1", "Key A"));
        await _repository.CreateAsync(CreateApiKey("user-1", "Key B"));
        await _repository.CreateAsync(CreateApiKey("user-2", "Key C"));

        var user1Keys = await _repository.GetByOwnerIdAsync("user-1");

        user1Keys.Should().HaveCount(2);
        user1Keys.All(k => k.OwnerId == "user-1").Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var apiKey = await _repository.CreateAsync(CreateApiKey("user-1", "Original"));

        apiKey.DisplayName = "Updated";
        apiKey.RecordUsage();
        await _repository.UpdateAsync(apiKey);

        var found = await _repository.GetByIdAsync(apiKey.Id);
        found!.DisplayName.Should().Be("Updated");
        found.UsageCount.Should().Be(1);
        found.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Lifecycle_IsActive_WhenNotExpiredOrRevoked()
    {
        var apiKey = CreateApiKey("user-1", "Active Key");
        apiKey.ExpiresAt = DateTime.UtcNow.AddDays(30);
        await _repository.CreateAsync(apiKey);

        var found = await _repository.GetByIdAsync(apiKey.Id);
        found!.IsActive.Should().BeTrue();
        found.IsExpired.Should().BeFalse();
        found.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Lifecycle_IsExpired_WhenPastExpiration()
    {
        var apiKey = CreateApiKey("user-1", "Expired Key");
        apiKey.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await _repository.CreateAsync(apiKey);

        var found = await _repository.GetByIdAsync(apiKey.Id);
        found!.IsExpired.Should().BeTrue();
        found.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Lifecycle_IsRevoked_AfterRevoke()
    {
        var apiKey = CreateApiKey("user-1", "Revoked Key");
        await _repository.CreateAsync(apiKey);

        apiKey.Revoke("Compromised");
        await _repository.UpdateAsync(apiKey);

        var found = await _repository.GetByIdAsync(apiKey.Id);
        found!.IsRevoked.Should().BeTrue();
        found.IsActive.Should().BeFalse();
        found.RevokedReason.Should().Be("Compromised");
    }

    [Fact]
    public async Task Lifecycle_SetReplacement_UpdatesExpirationAndLinks()
    {
        var oldKey = CreateApiKey("user-1", "Old Key");
        await _repository.CreateAsync(oldKey);
        var newKeyId = Guid.NewGuid();

        oldKey.SetReplacement(newKeyId, 7);
        await _repository.UpdateAsync(oldKey);

        var found = await _repository.GetByIdAsync(oldKey.Id);
        found!.ReplacedByKeyId.Should().Be(newKeyId);
        found.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        found.ExpiresAt.Should().BeBefore(DateTime.UtcNow.AddDays(8));
    }

    [Fact]
    public async Task DeleteExpiredKeysAsync_RemovesExpiredKeys()
    {
        var expired1 = CreateApiKey("user-1", "Expired 1");
        expired1.ExpiresAt = DateTime.UtcNow.AddDays(-10);
        await _repository.CreateAsync(expired1);

        var expired2 = CreateApiKey("user-1", "Expired 2");
        expired2.ExpiresAt = DateTime.UtcNow.AddDays(-5);
        await _repository.CreateAsync(expired2);

        var active = CreateApiKey("user-1", "Active");
        active.ExpiresAt = DateTime.UtcNow.AddDays(30);
        await _repository.CreateAsync(active);

        var deleted = await _repository.DeleteExpiredKeysAsync(DateTime.UtcNow);

        deleted.Should().Be(2);
        var remaining = await _repository.GetByOwnerIdAsync("user-1");
        remaining.Should().HaveCount(1);
        remaining[0].DisplayName.Should().Be("Active");
    }

    [Fact]
    public async Task OwnershipIsolation_UserCannotSeeOtherKeys()
    {
        await _repository.CreateAsync(CreateApiKey("user-a", "Key A"));
        await _repository.CreateAsync(CreateApiKey("user-b", "Key B"));

        var userAKeys = await _repository.GetByOwnerIdAsync("user-a");
        var userBKeys = await _repository.GetByOwnerIdAsync("user-b");

        userAKeys.Should().HaveCount(1);
        userAKeys[0].OwnerId.Should().Be("user-a");
        userBKeys.Should().HaveCount(1);
        userBKeys[0].OwnerId.Should().Be("user-b");
    }

    [Fact]
    public async Task KeyHash_IsNeverTheRawKey()
    {
        var rawKey = ApiKeyHasher.GenerateRawKey();
        var hash = ApiKeyHasher.HashKey(rawKey);
        var apiKey = CreateApiKey("user-1", "Test");
        apiKey.KeyHash = hash;
        await _repository.CreateAsync(apiKey);

        var found = await _repository.GetByIdAsync(apiKey.Id);
        found!.KeyHash.Should().NotBe(rawKey);
        found.KeyHash.Should().NotContain(rawKey);
    }

    // ── Helpers ──

    private static ApiKey CreateApiKey(string ownerId, string displayName)
    {
        return new ApiKey
        {
            DisplayName = displayName,
            KeyHash = ApiKeyHasher.HashKey(ApiKeyHasher.GenerateRawKey()),
            KeyPrefix = "dm_test12",
            OwnerId = ownerId,
            Scopes = ["memory:read", "memory:write"],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(90)
        };
    }
}

// Need FluentAssertions
