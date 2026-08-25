using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services.Retrieval;

/// <summary>
/// Deterministic approximate token/character budgeting implementation.
/// Uses the same ~4 chars/token heuristic as the existing TokenEstimator.
/// 
/// The budgeting mechanism never bypasses privacy boundaries — it operates
/// only on already-filtered and ranked eligible memories.
/// </summary>
public class CharacterContextBudgeter : IContextBudgeter
{
    private const double CharsPerToken = 4.0;

    /// <summary>
    /// Selects the highest-value memories that fit within the token budget.
    /// Memories are assumed to be pre-ranked by relevance (highest first).
    /// </summary>
    public Task<List<RetrievedMemory>> SelectWithinBudgetAsync(
        List<RetrievedMemory> rankedMemories,
        int tokenBudget,
        CancellationToken ct = default)
    {
        if (tokenBudget <= 0 || rankedMemories.Count == 0)
        {
            return Task.FromResult(new List<RetrievedMemory>());
        }

        var selected = new List<RetrievedMemory>();
        var remainingBudget = tokenBudget;

        foreach (var memory in rankedMemories)
        {
            ct.ThrowIfCancellationRequested();

            var estimatedTokens = memory.EstimatedTokens > 0
                ? memory.EstimatedTokens
                : EstimateTokens(memory);

            if (estimatedTokens <= remainingBudget)
            {
                memory.EstimatedTokens = estimatedTokens;
                selected.Add(memory);
                remainingBudget -= estimatedTokens;
            }
        }

        return Task.FromResult(selected);
    }

    private static int EstimateTokens(RetrievedMemory memory)
    {
        var textLength = memory.Title.Length + memory.Content.Length;
        return (int)Math.Ceiling(textLength / CharsPerToken);
    }
}
