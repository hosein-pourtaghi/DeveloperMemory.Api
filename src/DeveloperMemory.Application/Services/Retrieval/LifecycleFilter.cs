using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Filters memories based on their lifecycle state.
/// Active memories are returned normally; expired/superseded/archived/deleted
/// memories are excluded from active context.
/// </summary>
public static class LifecycleFilter
{
    /// <summary>
    /// The set of memory states that are eligible for active retrieval.
    /// </summary>
    private static readonly HashSet<MemoryState> ActiveStates =
    [
        MemoryState.Active,
        MemoryState.Updated
    ];

    /// <summary>
    /// Filters memories by lifecycle state.
    /// Only Active and Updated memories are considered eligible.
    /// Deleted, Expired, Archived, and Superseded memories are excluded.
    /// </summary>
    /// <param name="eligibleMemories">Memories that passed privacy filtering.</param>
    /// <returns>Memories with valid lifecycle state.</returns>
    public static List<(MemoryEntry Memory, string EligibilityReason)> FilterByLifecycle(
        List<(MemoryEntry Memory, string EligibilityReason)> eligibleMemories)
    {
        var results = new List<(MemoryEntry Memory, string EligibilityReason)>();

        foreach (var (memory, reason) in eligibleMemories)
        {
            if (!ActiveStates.Contains(memory.State))
            {
                // Skip non-active memories
                continue;
            }

            // Check expiration (even if state is Active, if ExpiresAt has passed, skip)
            if (memory.IsExpired)
            {
                continue;
            }

            results.Add((memory, reason));
        }

        return results;
    }

    /// <summary>
    /// Determines if a memory state is eligible for active retrieval.
    /// </summary>
    public static bool IsEligible(MemoryEntry memory)
    {
        return ActiveStates.Contains(memory.State) && !memory.IsExpired;
    }
}
