using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL/InMemory implementation of API key persistence.
/// Uses salted SHA-256 hashes — raw secrets are never stored.
/// </summary>
public class ApiKeyRepository : IApiKeyRepository
{
    private readonly DeveloperMemoryDbContext _context;

    public ApiKeyRepository(DeveloperMemoryDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(ct);
        return apiKey;
    }

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<List<ApiKey>> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .Where(k => k.OwnerId == ownerId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Update(apiKey);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ApiKey?> GetByKeyPrefixAsync(string keyPrefix, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix, ct);
    }

    public async Task<List<ApiKey>> GetExpiredKeysAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt.Value < cutoffUtc)
            .ToListAsync(ct);
    }

    public async Task<int> DeleteExpiredKeysAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var expired = await _context.ApiKeys
            .Where(k => k.ExpiresAt.HasValue && k.ExpiresAt.Value < cutoffUtc)
            .ToListAsync(ct);

        if (expired.Count == 0) return 0;

        _context.ApiKeys.RemoveRange(expired);
        return await _context.SaveChangesAsync(ct);
    }
}
