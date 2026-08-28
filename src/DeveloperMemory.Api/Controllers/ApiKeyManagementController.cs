using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using DeveloperMemory.Api.Infrastructure.Authentication;
using DeveloperMemory.Api.Infrastructure.Security;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// API key lifecycle management — create, rotate, revoke, and inspect keys.
/// All operations require authentication and enforce owner isolation.
/// Keys are persistently stored in PostgreSQL with salted SHA-256 hashes.
/// </summary>
[ApiController]
[Route("api/ApiKey")]
[Authorize]
public class ApiKeyManagementController : ControllerBase
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ISecurityAuditService _auditService;
    private readonly ApiKeySettings _apiKeySettings;
    private readonly ILogger<ApiKeyManagementController> _logger;

    public ApiKeyManagementController(
        IApiKeyRepository apiKeyRepository,
        ISecurityAuditService auditService,
        IOptions<ApiKeySettings> apiKeySettings,
        ILogger<ApiKeyManagementController> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _auditService = auditService;
        _apiKeySettings = apiKeySettings.Value;
        _logger = logger;
    }

    /// <summary>List all keys for the current user (without revealing secrets).</summary>
    [HttpGet]
    public async Task<IActionResult> ListKeys(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var keys = await _apiKeyRepository.GetByOwnerIdAsync(userId, ct);

        return Ok(keys.Select(k => new
        {
            Id = k.Id,
            k.DisplayName,
            KeyPrefix = k.KeyPrefix,
            k.Scopes,
            k.CreatedAt,
            k.ExpiresAt,
            k.RevokedAt,
            k.RevokedReason,
            k.LastUsedAt,
            k.UsageCount,
            ReplacedByKeyId = k.ReplacedByKeyId?.ToString(),
            // Never expose: KeyHash, raw secrets
        }));
    }

    /// <summary>Create a new API key. The raw key is only returned in this response.</summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateKey([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        var rawKey = ApiKeyHasher.GenerateRawKey();
        var keyHash = ApiKeyHasher.HashKey(rawKey);
        var expirationDays = _apiKeySettings.DefaultExpirationDays;

        var apiKey = new ApiKey
        {
            DisplayName = request.DisplayName ?? $"Key {DateTime.UtcNow:yyyyMMdd-HHmmss}",
            KeyHash = keyHash,
            KeyPrefix = rawKey[..Math.Min(11, rawKey.Length)],
            OwnerId = userId,
            Scopes = request.Scopes ?? [],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays)
        };

        await _apiKeyRepository.CreateAsync(apiKey, ct);

        _auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.KeyCreated,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = userId,
            KeyId = apiKey.Id.ToString(),
            SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("API key created for user {UserId}, keyId={KeyId}", userId, apiKey.Id);

        // Return the raw key — this is the ONLY time it is visible
        return Ok(new
        {
            apiKey.Id,
            apiKey.DisplayName,
            Key = rawKey, // Raw secret — shown once only
            apiKey.ExpiresAt,
            Warning = "Store this key securely. It will not be shown again."
        });
    }

    /// <summary>Rotate an existing key — issues a replacement and sets an overlap period on the old key.</summary>
    [HttpPost("rotate/{keyId}")]
    public async Task<IActionResult> RotateKey(string keyId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        if (!Guid.TryParse(keyId, out var keyGuid))
            return NotFound();

        var existingKey = await _apiKeyRepository.GetByIdAsync(keyGuid, ct);
        if (existingKey == null || existingKey.OwnerId != userId)
            return NotFound();

        // Create replacement key
        var rawKey = ApiKeyHasher.GenerateRawKey();
        var keyHash = ApiKeyHasher.HashKey(rawKey);
        var overlapDays = _apiKeySettings.RotationOverlapDays;

        var newApiKey = new ApiKey
        {
            DisplayName = $"{existingKey.DisplayName} (rotated)",
            KeyHash = keyHash,
            KeyPrefix = rawKey[..Math.Min(11, rawKey.Length)],
            OwnerId = userId,
            Scopes = [.. existingKey.Scopes],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_apiKeySettings.DefaultExpirationDays)
        };

        await _apiKeyRepository.CreateAsync(newApiKey, ct);

        // Mark old key with overlap expiration
        existingKey.SetReplacement(newApiKey.Id, overlapDays);
        await _apiKeyRepository.UpdateAsync(existingKey, ct);

        _auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.KeyRotated,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = userId,
            KeyId = newApiKey.Id.ToString(),
            SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Metadata = new Dictionary<string, string> { ["PreviousKeyId"] = keyGuid.ToString() }
        });

        _logger.LogInformation("API key rotated: {OldKeyId} -> {NewKeyId} for user {UserId}",
            keyGuid, newApiKey.Id, userId);

        return Ok(new
        {
            newApiKey.Id,
            newApiKey.DisplayName,
            Key = rawKey,
            newApiKey.ExpiresAt,
            OldKeyExpiresAt = existingKey.ExpiresAt,
            Warning = "Store this key securely. It will not be shown again."
        });
    }

    /// <summary>Revoke an API key immediately.</summary>
    [HttpPost("revoke/{keyId}")]
    public async Task<IActionResult> RevokeKey(string keyId, [FromBody] RevokeApiKeyRequest? request = null, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();

        if (!Guid.TryParse(keyId, out var keyGuid))
            return NotFound();

        var key = await _apiKeyRepository.GetByIdAsync(keyGuid, ct);
        if (key == null || key.OwnerId != userId)
            return NotFound();

        key.Revoke(request?.Reason);
        await _apiKeyRepository.UpdateAsync(key, ct);

        _auditService.RecordEvent(new SecurityAuditEvent
        {
            EventType = SecurityEventType.KeyRevoked,
            Outcome = SecurityEventOutcome.Success,
            OwnerId = userId,
            KeyId = keyGuid.ToString(),
            SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            FailureReason = key.RevokedReason
        });

        _logger.LogInformation("API key revoked: {KeyId} by user {UserId}, reason: {Reason}",
            keyGuid, userId, key.RevokedReason);

        return Ok(new { key.Id, key.RevokedAt, key.RevokedReason });
    }

    /// <summary>View security audit events for the current user.</summary>
    [HttpGet("audit")]
    public IActionResult GetAuditEvents([FromQuery] int count = 50)
    {
        var events = _auditService.GetRecentEvents(Math.Min(count, 200));
        return Ok(events);
    }
}

public class CreateApiKeyRequest
{
    public string? DisplayName { get; set; }
    public List<string>? Scopes { get; set; }
}

public class RevokeApiKeyRequest
{
    public string? Reason { get; set; }
}
