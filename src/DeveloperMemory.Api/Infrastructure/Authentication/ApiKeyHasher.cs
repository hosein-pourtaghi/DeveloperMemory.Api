using System.Security.Cryptography;
using System.Text;

namespace DeveloperMemory.Api.Infrastructure.Authentication;

/// <summary>
/// Secure API key hashing using HMAC-SHA256 with random salt.
/// Raw keys are never stored — only salted hashes.
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>Hash a raw API key with a random salt. Returns "salt:hash" format.</summary>
    public static string HashKey(string rawKey)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var saltHex = Convert.ToHexString(salt);
        var hash = ComputeHash(rawKey, salt);
        return $"{saltHex}:{hash}";
    }

    /// <summary>Verify a raw key against a stored salted hash.</summary>
    public static bool VerifyKey(string rawKey, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromHexString(parts[0]);
        var expectedHash = parts[1];
        var actualHash = ComputeHash(rawKey, salt);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedHash));
    }

    private static string ComputeHash(string rawKey, byte[] salt)
    {
        var keyBytes = Encoding.UTF8.GetBytes(rawKey);
        var saltedKey = new byte[salt.Length + keyBytes.Length];
        Buffer.BlockCopy(salt, 0, saltedKey, 0, salt.Length);
        Buffer.BlockCopy(keyBytes, 0, saltedKey, salt.Length, keyBytes.Length);

        var hashBytes = SHA256.HashData(saltedKey);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>Generate a new secure API key with the "dm_" prefix.</summary>
    public static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"dm_{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}
