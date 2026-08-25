using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-independent abstraction for prompt profiles.
/// Profiles configure how the Prompt Intelligence Engine behaves.
/// </summary>
public interface IPromptProfileProvider
{
    /// <summary>
    /// Gets a profile by name.
    /// Returns null if not found.
    /// </summary>
    Task<PromptProfile?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Gets a profile by ID.
    /// Returns null if not found.
    /// </summary>
    Task<PromptProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all enabled profiles.
    /// </summary>
    Task<IReadOnlyList<PromptProfile>> GetEnabledProfilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the default profile.
    /// </summary>
    Task<PromptProfile> GetDefaultProfileAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new profile.
    /// </summary>
    Task<PromptProfile> CreateAsync(PromptProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing profile.
    /// </summary>
    Task<PromptProfile?> UpdateAsync(Guid id, PromptProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Deletes a profile.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
