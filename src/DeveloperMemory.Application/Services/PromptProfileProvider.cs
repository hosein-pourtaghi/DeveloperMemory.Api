using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Prompt profile provider using the existing persistence infrastructure.
/// Manages prompt intelligence profiles with versioning.
/// </summary>
public class PromptProfileProvider : IPromptProfileProvider
{
    private readonly IMemoryRepository _memoryRepository; // Reuse existing repository pattern
    private readonly ILogger<PromptProfileProvider> _logger;

    // In-memory cache for profiles (profiles change infrequently)
    private readonly Dictionary<Guid, PromptProfile> _cache = new();
    private readonly Dictionary<string, PromptProfile> _nameCache = new(StringComparer.OrdinalIgnoreCase);

    public PromptProfileProvider(
        ILogger<PromptProfileProvider> logger)
    {
        _logger = logger;
        InitializeDefaultProfiles();
    }

    public Task<PromptProfile?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        _nameCache.TryGetValue(name, out var profile);
        return Task.FromResult(profile);
    }

    public Task<PromptProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _cache.TryGetValue(id, out var profile);
        return Task.FromResult(profile);
    }

    public Task<IReadOnlyList<PromptProfile>> GetEnabledProfilesAsync(CancellationToken ct = default)
    {
        var profiles = _cache.Values
            .Where(p => p.Enabled)
            .OrderBy(p => p.Name)
            .ToList();
        return Task.FromResult<IReadOnlyList<PromptProfile>>(profiles);
    }

    public Task<PromptProfile> GetDefaultProfileAsync(CancellationToken ct = default)
    {
        var defaultProfile = _cache.Values.FirstOrDefault(p => p.Name == "DefaultDeveloper")
                              ?? _cache.Values.First();
        return Task.FromResult(defaultProfile);
    }

    public Task<PromptProfile> CreateAsync(PromptProfile profile, CancellationToken ct = default)
    {
        profile.Id = Guid.NewGuid();
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.Version = 1;

        _cache[profile.Id] = profile;
        _nameCache[profile.Name] = profile;

        _logger.LogInformation("Prompt profile created: {Name} (v{Version})", profile.Name, profile.Version);
        return Task.FromResult(profile);
    }

    public Task<PromptProfile?> UpdateAsync(Guid id, PromptProfile profile, CancellationToken ct = default)
    {
        if (!_cache.ContainsKey(id))
        {
            return Task.FromResult<PromptProfile?>(null);
        }

        var existing = _cache[id];
        existing.Name = profile.Name;
        existing.Description = profile.Description;
        existing.ConfigurationJson = profile.ConfigurationJson;
        existing.Enabled = profile.Enabled;
        existing.Version++;
        existing.UpdatedAt = DateTime.UtcNow;

        _nameCache[existing.Name] = existing;

        _logger.LogInformation("Prompt profile updated: {Name} (v{Version})", existing.Name, existing.Version);
        return Task.FromResult<PromptProfile?>(existing);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(id, out var profile))
        {
            return Task.FromResult(false);
        }

        _cache.Remove(id);
        _nameCache.Remove(profile.Name);

        _logger.LogInformation("Prompt profile deleted: {Name}", profile.Name);
        return Task.FromResult(true);
    }

    private void InitializeDefaultProfiles()
    {
        var defaults = new[]
        {
            CreateProfile("DefaultDeveloper", "Standard developer prompt intelligence", 4000,
                intent: false, memory: true, projectContext: true, optimization: "Auto"),
            CreateProfile("Debugging", "Optimized for debugging and error resolution", 6000,
                intent: true, memory: true, projectContext: true, optimization: "Deterministic"),
            CreateProfile("ArchitectureReview", "Architecture design and review", 8000,
                intent: true, memory: true, projectContext: true, optimization: "Auto"),
            CreateProfile("CodeGeneration", "Code implementation tasks", 4000,
                intent: false, memory: true, projectContext: true, optimization: "Auto"),
            CreateProfile("Documentation", "Documentation generation", 3000,
                intent: false, memory: false, projectContext: true, optimization: "Deterministic"),
        };

        foreach (var profile in defaults)
        {
            _cache[profile.Id] = profile;
            _nameCache[profile.Name] = profile;
        }
    }

    private static PromptProfile CreateProfile(
        string name, string description, int tokenBudget,
        bool intent, bool memory, bool projectContext, string optimization)
    {
        var profile = new PromptProfile
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Version = 1,
            Enabled = true
        };

        profile.SetConfiguration(new PromptProfileConfiguration
        {
            TokenBudget = tokenBudget,
            IntentPolicy = new IntentPolicyConfig { UseLlmAnalysis = intent },
            MemoryPolicy = new MemoryPolicyConfig { IncludeMemory = memory },
            ContextPolicy = new ContextPolicyConfig { IncludeProjectContext = projectContext },
            OptimizationPolicy = new OptimizationPolicyConfig { Mode = optimization }
        });

        return profile;
    }
}
