namespace DeveloperMemory.Api.Infrastructure.Authentication;

public class ApiKeySettings
{
    public bool DevelopmentBypass { get; set; }
    public string DevelopmentOwnerId { get; set; } = "development-owner";
    public string DevelopmentOwnerDisplayName { get; set; } = "Local Development Owner";
    public List<ApiKeyEntry> ApiKeys { get; set; } = [];
    public int DefaultExpirationDays { get; set; } = 90;
    public int RotationOverlapDays { get; set; } = 7;
}

public class ApiKeyEntry
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public List<string> Scopes { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? ReplacedByKeyId { get; set; }
}
