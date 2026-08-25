using DeveloperMemory.Domain.Entities;

namespace DeveloperMemory.Domain.Interfaces;

/// <summary>
/// Abstraction for selecting the best memories within a specified token budget.
/// The implementation must never bypass privacy boundaries — budgeting
/// operates only on already-filtered eligible memories.
/// </summary>
public interface IContextBudgeter
{
    /// <summary>
    /// Selects the highest-value memories that fit within the token budget.
    /// </summary>
    /// <param name="rankedMemories">Memories ordered by relevance (highest first).</param>
    /// <param name="tokenBudget">Maximum approximate tokens to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected memories that fit within the budget.</returns>
    Task<List<RetrievedMemory>> SelectWithinBudgetAsync(
        List<RetrievedMemory> rankedMemories,
        int tokenBudget,
        CancellationToken ct = default);
}
