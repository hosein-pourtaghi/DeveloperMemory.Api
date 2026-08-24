using System.Security.Cryptography;
using System.Text;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// Generates deterministic GUIDs from file paths so that document and profile IDs
/// remain stable across application restarts and reindexes.
/// </summary>
public static class StableIdHelper
{
    /// <summary>
    /// Generates a stable GUID from a file path by hashing the normalized path string.
    /// The same file path always produces the same GUID.
    /// </summary>
    public static Guid GenerateFromFilePath(string filePath)
    {
        // Normalize the path to ensure consistency across platforms
        var normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/').ToLowerInvariant();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));

        // Use the first 16 bytes of the SHA-256 hash to create a GUID
        var guidBytes = new byte[16];
        Array.Copy(hashBytes, guidBytes, 16);

        return new Guid(guidBytes);
    }
}
