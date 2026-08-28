using System.Security.Claims;
using System.Text.Encodings.Web;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DeveloperMemory.Api.Infrastructure.Authentication;

/// <summary>
/// Authenticates API keys from either:
///   1. PostgreSQL database (primary, via IApiKeyRepository)
///   2. Configuration (fallback, for development bootstrap)
///
/// Lifecycle checks (expiration, revocation) are enforced before authentication succeeds.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "Authorization";
    private const string BearerPrefix = "Bearer ";

    private readonly ApiKeySettings _apiKeySettings;
    private readonly IApiKeyRepository? _apiKeyRepository;
    private readonly ISecurityAuditService _auditService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeySettings> apiKeySettings,
        ISecurityAuditService auditService,
        IServiceProvider serviceProvider)
        : base(options, logger, encoder)
    {
        _apiKeySettings = apiKeySettings.Value;
        _auditService = auditService;

        // Optional: resolve IApiKeyRepository from DI (may not be registered in unit tests)
        _apiKeyRepository = serviceProvider.GetService(typeof(IApiKeyRepository)) as IApiKeyRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(ApiKeyHeaderName))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string rawKey;
        if (authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            rawKey = authHeader[BearerPrefix.Length..].Trim();
        }
        else
        {
            rawKey = authHeader.Trim();
        }

        if (string.IsNullOrEmpty(rawKey))
        {
            return AuthenticateResult.NoResult();
        }

        // ── Try database-backed key first ──
        // Lookup by prefix (first ~11 chars of raw key), then verify with salted hash
        if (_apiKeyRepository != null && rawKey.Length >= 8)
        {
            try
            {
                var prefix = rawKey[..Math.Min(11, rawKey.Length)];
                var candidate = await _apiKeyRepository.GetByKeyPrefixAsync(prefix);

                if (candidate != null && ApiKeyHasher.VerifyKey(rawKey, candidate.KeyHash))
                {
                    return ValidateDatabaseKey(candidate);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Database key lookup failed, falling back to config");
            }
        }

        // ── Fallback: configuration-based keys (development bootstrap) ──
        return ValidateConfigKey(rawKey);
    }

    private AuthenticateResult ValidateDatabaseKey(DeveloperMemory.Domain.Entities.ApiKey dbKey)
    {
        var sourceIp = Context.Connection.RemoteIpAddress?.ToString();

        // Revoked key
        if (dbKey.IsRevoked)
        {
            _auditService.RecordEvent(new SecurityAuditEvent
            {
                EventType = SecurityEventType.RevokedApiKeyAttempt,
                Outcome = SecurityEventOutcome.Failure,
                OwnerId = dbKey.OwnerId,
                KeyId = dbKey.Id.ToString(),
                SourceIp = sourceIp,
                FailureReason = $"Revoked{(dbKey.RevokedReason != null ? $" ({dbKey.RevokedReason})" : string.Empty)}"
            });
            return AuthenticateResult.Fail($"API key has been revoked{(dbKey.RevokedReason != null ? $" ({dbKey.RevokedReason})" : string.Empty)}.");
        }

        // Expired key
        if (dbKey.IsExpired)
        {
            _auditService.RecordEvent(new SecurityAuditEvent
            {
                EventType = SecurityEventType.ExpiredApiKeyAttempt,
                Outcome = SecurityEventOutcome.Failure,
                OwnerId = dbKey.OwnerId,
                KeyId = dbKey.Id.ToString(),
                SourceIp = sourceIp,
                FailureReason = "Key expired"
            });
            return AuthenticateResult.Fail("API key has expired.");
        }

        // Valid key — record usage
        dbKey.RecordUsage();
        _ = _apiKeyRepository!.UpdateAsync(dbKey);

        _auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationSuccess,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = dbKey.OwnerId,
            KeyId = dbKey.Id.ToString(),
            SourceIp = sourceIp
        });

        return CreateSuccessResult(dbKey.OwnerId, dbKey.OwnerDisplayName ?? dbKey.OwnerId, dbKey.Id.ToString());
    }

    private AuthenticateResult ValidateConfigKey(string rawKey)
    {
        var matchedKey = _apiKeySettings.ApiKeys.FirstOrDefault(k =>
            string.Equals(k.Key, rawKey, StringComparison.Ordinal));

        if (matchedKey == null)
        {
            _auditService.RecordEvent(new SecurityAuditEvent
            {
                EventType = SecurityEventType.InvalidApiKeyAttempt,
                Outcome = SecurityEventOutcome.Failure,
                SourceIp = Context.Connection.RemoteIpAddress?.ToString(),
                FailureReason = "Unknown key"
            });
            return AuthenticateResult.Fail("Invalid API key.");
        }

        // Revoked
        if (matchedKey.RevokedAt.HasValue)
        {
            _auditService.RecordEvent(new SecurityAuditEvent
            {
                EventType = SecurityEventType.RevokedApiKeyAttempt,
                Outcome = SecurityEventOutcome.Failure,
                OwnerId = matchedKey.UserId,
                KeyId = matchedKey.Id,
                SourceIp = Context.Connection.RemoteIpAddress?.ToString(),
                FailureReason = "Revoked"
            });
            return AuthenticateResult.Fail($"API key has been revoked{(matchedKey.RevokedReason != null ? $" ({matchedKey.RevokedReason})" : string.Empty)}.");
        }

        // Expired
        if (matchedKey.ExpiresAt.HasValue && matchedKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            _auditService.RecordEvent(new SecurityAuditEvent
            {
                EventType = SecurityEventType.ExpiredApiKeyAttempt,
                Outcome = SecurityEventOutcome.Failure,
                OwnerId = matchedKey.UserId,
                KeyId = matchedKey.Id,
                SourceIp = Context.Connection.RemoteIpAddress?.ToString(),
                FailureReason = "Key expired"
            });
            return AuthenticateResult.Fail("API key has expired.");
        }

        // Update last used
        matchedKey.LastUsedAt = DateTime.UtcNow;

        _auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.AuthenticationSuccess,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = matchedKey.UserId,
            KeyId = matchedKey.Id,
            SourceIp = Context.Connection.RemoteIpAddress?.ToString()
        });

        return CreateSuccessResult(matchedKey.UserId, matchedKey.DisplayName ?? matchedKey.UserId, matchedKey.Id);
    }

    private static AuthenticateResult CreateSuccessResult(string userId, string displayName, string keyId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, displayName),
            new Claim("api_key_id", keyId),
            new Claim("api_key", "true")
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "ApiKey");

        return AuthenticateResult.Success(ticket);
    }
}
