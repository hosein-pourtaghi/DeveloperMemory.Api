using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

/// <summary>
/// Prompt profile repository with versioning and concurrency support.
/// Implements IPromptProfileProvider for backward compatibility.
/// </summary>
public class PromptProfileRepository : IPromptProfileProvider
{
    private readonly DeveloperMemoryDbContext _context;
    private readonly ILogger<PromptProfileRepository> _logger;

    public PromptProfileRepository(
        DeveloperMemoryDbContext context,
        ILogger<PromptProfileRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PromptProfile?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.PromptProfiles
            .FirstOrDefaultAsync(p => p.Name == name, ct);
    }

    public async Task<PromptProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.PromptProfiles
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<PromptProfile>> GetEnabledProfilesAsync(CancellationToken ct = default)
    {
        return await _context.PromptProfiles
            .Where(p => p.Enabled)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<PromptProfile> GetDefaultProfileAsync(CancellationToken ct = default)
    {
        var defaultProfile = await _context.PromptProfiles
            .FirstOrDefaultAsync(p => p.Name == "DefaultDeveloper", ct);

        if (defaultProfile == null)
        {
            defaultProfile = await _context.PromptProfiles
                .OrderBy(p => p.Name)
                .FirstOrDefaultAsync(ct);
        }

        if (defaultProfile == null)
        {
            // Create default if none exists
            defaultProfile = CreateDefaultProfile();
            _context.PromptProfiles.Add(defaultProfile);
            await _context.SaveChangesAsync(ct);
        }

        return defaultProfile;
    }

    public async Task<PromptProfile> CreateAsync(PromptProfile profile, CancellationToken ct = default)
    {
        profile.Id = Guid.NewGuid();
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.Version = 1;

        _context.PromptProfiles.Add(profile);

        // Create initial version
        var version = new PromptProfileVersion
        {
            Id = Guid.NewGuid(),
            PromptProfileId = profile.Id,
            Version = 1,
            ConfigurationJson = profile.ConfigurationJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        _context.PromptProfileVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Prompt profile created: {Name} (v{Version})", profile.Name, profile.Version);
        return profile;
    }

    public async Task<PromptProfile?> UpdateAsync(Guid id, PromptProfile profile, CancellationToken ct = default)
    {
        var existing = await _context.PromptProfiles.FindAsync([id], ct);
        if (existing == null) return null;

        // Check concurrency
        if (existing.Version != profile.Version)
        {
            throw new DbUpdateConcurrencyException("Profile has been modified by another process");
        }

        existing.Name = profile.Name;
        existing.Description = profile.Description;
        existing.ConfigurationJson = profile.ConfigurationJson;
        existing.Enabled = profile.Enabled;
        existing.Version++;
        existing.UpdatedAt = DateTime.UtcNow;

        // Create new version
        var newVersion = new PromptProfileVersion
        {
            Id = Guid.NewGuid(),
            PromptProfileId = id,
            Version = existing.Version,
            ConfigurationJson = profile.ConfigurationJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        // Deactivate previous versions
        var previousVersions = await _context.PromptProfileVersions
            .Where(v => v.PromptProfileId == id && v.IsActive)
            .ToListAsync(ct);

        foreach (var prev in previousVersions)
        {
            prev.IsActive = false;
        }

        _context.PromptProfileVersions.Add(newVersion);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Prompt profile updated: {Name} (v{Version})", existing.Name, existing.Version);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _context.PromptProfiles.FindAsync([id], ct);
        if (profile == null) return false;

        // Protect system profiles
        if (profile.Name.StartsWith("Default") || profile.Name == "Debugging")
        {
            _logger.LogWarning("Cannot delete system profile: {Name}", profile.Name);
            return false;
        }

        _context.PromptProfiles.Remove(profile);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Prompt profile deleted: {Name}", profile.Name);
        return true;
    }

    /// <summary>
    /// Rolls back to a specific version by creating a new version with that configuration.
    /// </summary>
    public async Task<PromptProfile?> RollbackAsync(Guid profileId, int targetVersion, CancellationToken ct = default)
    {
        var profile = await _context.PromptProfiles.FindAsync([profileId], ct);
        if (profile == null) return null;

        var targetVersionEntity = await _context.PromptProfileVersions
            .FirstOrDefaultAsync(v => v.PromptProfileId == profileId && v.Version == targetVersion, ct);

        if (targetVersionEntity == null) return null;

        // Create new version with target configuration
        profile.ConfigurationJson = targetVersionEntity.ConfigurationJson;
        profile.Version++;
        profile.UpdatedAt = DateTime.UtcNow;

        var newVersion = new PromptProfileVersion
        {
            Id = Guid.NewGuid(),
            PromptProfileId = profileId,
            Version = profile.Version,
            ConfigurationJson = targetVersionEntity.ConfigurationJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system",
            ChangeDescription = $"Rollback to version {targetVersion}"
        };

        _context.PromptProfileVersions.Add(newVersion);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Profile {Name} rolled back to v{Target} (now v{New})",
            profile.Name, targetVersion, profile.Version);

        return profile;
    }

    /// <summary>
    /// Gets all versions for a profile.
    /// </summary>
    public async Task<IReadOnlyList<PromptProfileVersion>> GetVersionsAsync(
        Guid profileId, CancellationToken ct = default)
    {
        return await _context.PromptProfileVersions
            .Where(v => v.PromptProfileId == profileId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);
    }

    private static PromptProfile CreateDefaultProfile()
    {
        var profile = new PromptProfile
        {
            Id = Guid.NewGuid(),
            Name = "DefaultDeveloper",
            Description = "Standard developer prompt intelligence",
            Version = 1,
            Enabled = true
        };

        profile.SetConfiguration(new PromptProfileConfiguration
        {
            TokenBudget = 4000,
            IntentPolicy = new IntentPolicyConfig { UseLlmAnalysis = false },
            MemoryPolicy = new MemoryPolicyConfig { IncludeMemory = true },
            ContextPolicy = new ContextPolicyConfig { IncludeProjectContext = true },
            OptimizationPolicy = new OptimizationPolicyConfig { Mode = "Auto" }
        });

        return profile;
    }
}
