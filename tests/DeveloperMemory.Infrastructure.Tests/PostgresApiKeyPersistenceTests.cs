using Xunit;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// PostgreSQL persistence tests for the API key lifecycle.
/// Every test writes through one context and reads back through a NEW context,
/// proving key state survives context recreation.
/// </summary>
public class PostgresApiKeyPersistenceTests : PostgresTestBase
{
    private const string OwnerA = "pg-key-owner-a";
    private const string OwnerB = "pg-key-owner-b";

    public PostgresApiKeyPersistenceTests(PostgresDbFixture fixture) : base(fixture) { }

    private static string HashRawKey(string rawKey)
    {
        // Same algorithm as ApiKeyHasher (HMAC-SHA256 with random salt).
        // Reimplemented here to avoid a dependency on the Api project from
        // the Infrastructure test project; the format is "saltHex:hashHex".
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
        var salted = new byte[salt.Length + keyBytes.Length];
        System.Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
        System.Buffer.BlockCopy(keyBytes, 0, salted, salt.Length, keyBytes.Length);
        var hash = System.Security.Cryptography.SHA256.HashData(salted);
        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    private static ApiKey CreateKey(
        string rawKey,
        string ownerId = OwnerA,
        string displayName = "PG Test Key",
        DateTime? expiresAt = null)
    {
        return new ApiKey
        {
            DisplayName = displayName,
            KeyHash = HashRawKey(rawKey),
            KeyPrefix = rawKey[..Math.Min(11, rawKey.Length)],
            OwnerId = ownerId,
            Scopes = ["memory:read", "memory:write"],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    [Fact]
    public async Task CreateAndAuthenticate_SurvivesContextRecreation()
    {
        const string rawKey = "dm_test_persist_key_123";
        Guid keyId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var key = await repo.CreateAsync(CreateKey(rawKey));
            keyId = key.Id;
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);

            // Lookup by prefix (as the auth handler does), then verify hash
            var found = await repo.GetByKeyPrefixAsync(rawKey[..Math.Min(11, rawKey.Length)]);
            Assert.NotNull(found);
            Assert.Equal(keyId, found!.Id);
            Assert.Equal(OwnerA, found.OwnerId);

            // Verify the raw key against the stored salted hash
            var parts = found.KeyHash.Split(':');
            Assert.Equal(2, parts.Length);
            var salt = Convert.FromHexString(parts[0]);
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
            var salted = new byte[salt.Length + keyBytes.Length];
            System.Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
            System.Buffer.BlockCopy(keyBytes, 0, salted, salt.Length, keyBytes.Length);
            var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(salted));
            Assert.Equal(parts[1], actualHash);

            // Raw secret must never be stored
            Assert.DoesNotContain(rawKey, found.KeyHash);
            Assert.DoesNotContain(rawKey, found.KeyPrefix + "x");
        }
    }

    [Fact]
    public async Task Revocation_SurvivesContextRecreation()
    {
        const string rawKey = "dm_test_revoke_key_456";
        Guid keyId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var key = await repo.CreateAsync(CreateKey(rawKey));
            keyId = key.Id;

            key.Revoke("compromised");
            await repo.UpdateAsync(key);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var found = await repo.GetByIdAsync(keyId);
            Assert.NotNull(found);
            Assert.True(found!.IsRevoked);
            Assert.Equal("compromised", found.RevokedReason);
            Assert.False(found.IsActive);
        }
    }

    [Fact]
    public async Task Rotation_SurvivesContextRecreation()
    {
        const string oldRawKey = "dm_test_rotate_old_789";
        Guid oldKeyId;
        Guid newKeyId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var oldKey = await repo.CreateAsync(CreateKey(oldRawKey));
            oldKeyId = oldKey.Id;

            // Rotate: create replacement, mark old key with overlap
            var newKey = await repo.CreateAsync(CreateKey("dm_test_rotate_new_000", displayName: "PG Test Key (rotated)"));
            newKeyId = newKey.Id;
            oldKey.SetReplacement(newKeyId, overlapDays: 7);
            await repo.UpdateAsync(oldKey);
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var oldKey = await repo.GetByIdAsync(oldKeyId);
            var newKey = await repo.GetByIdAsync(newKeyId);

            Assert.NotNull(oldKey);
            Assert.NotNull(newKey);
            Assert.Equal(newKeyId, oldKey!.ReplacedByKeyId);
            Assert.NotNull(oldKey.ExpiresAt);
            Assert.True(oldKey.ExpiresAt > DateTime.UtcNow.AddDays(6)); // overlap window ~7 days
            Assert.Equal(OwnerA, newKey!.OwnerId);
            Assert.False(newKey.IsRevoked);
        }
    }

    [Fact]
    public async Task Expiration_IsDetected_AfterContextRecreation()
    {
        const string rawKey = "dm_test_expire_key_111";
        Guid keyId;
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var key = await repo.CreateAsync(CreateKey(rawKey, expiresAt: DateTime.UtcNow.AddHours(-1)));
            keyId = key.Id;
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var found = await repo.GetByIdAsync(keyId);
            Assert.NotNull(found);
            Assert.True(found!.IsExpired);
            Assert.False(found.IsActive);

            // Expired keys are discoverable for cleanup
            var expired = await repo.GetExpiredKeysAsync(DateTime.UtcNow);
            Assert.Contains(expired, k => k.Id == keyId);
        }
    }

    [Fact]
    public async Task OwnershipIsolation_SurvivesContextRecreation()
    {
        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            await repo.CreateAsync(CreateKey("dm_test_owner_a_key_222", ownerId: OwnerA));
            await repo.CreateAsync(CreateKey("dm_test_owner_b_key_333", ownerId: OwnerB));
        }

        await using (var ctx = Fixture.CreateContext())
        {
            var repo = new ApiKeyRepository(ctx);
            var keysA = await repo.GetByOwnerIdAsync(OwnerA);
            var keysB = await repo.GetByOwnerIdAsync(OwnerB);

            Assert.Single(keysA);
            Assert.Single(keysB);
            Assert.All(keysA, k => Assert.Equal(OwnerA, k.OwnerId));
            Assert.All(keysB, k => Assert.Equal(OwnerB, k.OwnerId));
        }
    }
}
