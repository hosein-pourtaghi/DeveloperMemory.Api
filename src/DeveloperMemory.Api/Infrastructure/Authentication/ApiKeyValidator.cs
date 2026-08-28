namespace DeveloperMemory.Api.Infrastructure.Authentication;

/// <summary>
/// Extracted API key validation logic — testable independently of the authentication handler.
/// </summary>
public class ApiKeyValidator
{
    private readonly ApiKeySettings _settings;

    public ApiKeyValidator(ApiKeySettings settings)
    {
        _settings = settings;
    }

    public ApiKeyValidationResult Validate(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return ApiKeyValidationResult.NoResult;
        }

        var matchedKey = _settings.ApiKeys.FirstOrDefault(k =>
            string.Equals(k.Key, apiKey, StringComparison.Ordinal));

        if (matchedKey == null)
        {
            return new ApiKeyValidationResult
            {
                IsValid = false,
                FailureReason = "Invalid API key."
            };
        }

        // Revoked key
        if (matchedKey.RevokedAt.HasValue)
        {
            return new ApiKeyValidationResult
            {
                IsValid = false,
                FailureReason = $"API key has been revoked{(matchedKey.RevokedReason != null ? $" ({matchedKey.RevokedReason})" : string.Empty)}.",
                KeyEntry = matchedKey
            };
        }

        // Expired key
        if (matchedKey.ExpiresAt.HasValue && matchedKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            return new ApiKeyValidationResult
            {
                IsValid = false,
                FailureReason = "API key has expired.",
                KeyEntry = matchedKey
            };
        }

        // Update last used timestamp
        matchedKey.LastUsedAt = DateTime.UtcNow;

        return new ApiKeyValidationResult
        {
            IsValid = true,
            KeyEntry = matchedKey
        };
    }
}

public class ApiKeyValidationResult
{
    public static readonly ApiKeyValidationResult NoResult = new() { IsNoResult = true };

    public bool IsNoResult { get; init; }
    public bool IsValid { get; init; }
    public string? FailureReason { get; init; }
    public ApiKeyEntry? KeyEntry { get; init; }
}
