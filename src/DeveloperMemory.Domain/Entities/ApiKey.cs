using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Domain.Entities;

/// <summary>
/// Persistent API key with full lifecycle management.
/// Raw secrets are never stored — only salted SHA-256 hashes.
/// </summary>
public class ApiKey : BaseEntity
{
    /// <summary>Human-readable name for this key.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hashed representation of the raw API key (salted SHA-256). Never the raw value.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>First 8 characters of the raw key, for display/identification only.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>The owner/user this key belongs to (server-controlled, from authentication).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Optional display name for the key owner.</summary>
    public string? OwnerDisplayName { get; set; }

    /// <summary>Scopes granted to this key.</summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>When the key was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the key expires (null = no expiration).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the key was revoked (null = not revoked).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Reason for revocation.</summary>
    public string? RevokedReason { get; set; }

    /// <summary>When the key was last successfully used.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>How many times the key has been used.</summary>
    public int UsageCount { get; set; }

    /// <summary>Pointer to the replacement key after rotation.</summary>
    public Guid? ReplacedByKeyId { get; set; }

    // ── Lifecycle methods ──

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsExpired && !IsRevoked;

    public void Revoke(string? reason = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reason ?? "Revoked by user";
    }

    public void RecordUsage()
    {
        LastUsedAt = DateTime.UtcNow;
        UsageCount++;
    }

    public void SetReplacement(Guid newKeyId, int overlapDays)
    {
        ReplacedByKeyId = newKeyId;
        ExpiresAt = DateTime.UtcNow.AddDays(overlapDays);
    }
}

/// <summary>
/// Allowed values for API key lifecycle state.
/// </summary>
public enum ApiKeyState
{
    Active,
    Expired,
    Revoked
}
