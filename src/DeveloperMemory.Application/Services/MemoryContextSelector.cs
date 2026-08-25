using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Selects the best memories for a prompt context within a token budget.
/// 
/// Selection pipeline:
///   1. Rank all candidates
///   2. Exclude expired, deleted, superseded
///   3. Deduplicate
///   4. Select highest-value memories within budget
///   5. Track skipped memories with reasons
/// 
/// The selection is deterministic and explainable.
/// </summary>
public class MemoryContextSelector
{
    private const double CharsPerToken = 4.0;

    /// <summary>
    /// Selects memories for the prompt context within the budget.
    /// </summary>
    public MemoryContextSelection Select(
        IReadOnlyList<MemoryEntry> candidates,
        string query,
        int budget,
        RankingContext? context = null)
    {
        var selection = new MemoryContextSelection { Budget = budget };

        if (candidates.Count == 0 || budget <= 0)
        {
            selection.BudgetExhausted = true;
            return selection;
        }

        // ── Step 1: Filter out ineligible memories ──
        var eligible = new List<MemoryEntry>();
        foreach (var memory in candidates)
        {
            if (memory.State == MemoryState.Deleted)
            {
                selection.Skipped.Add(new SkippedMemory
                {
                    Memory = memory,
                    SkipReason = "Deleted",
                    RelevanceScore = 0
                });
                continue;
            }

            if (memory.IsExpired)
            {
                selection.Skipped.Add(new SkippedMemory
                {
                    Memory = memory,
                    SkipReason = "Expired",
                    RelevanceScore = 0
                });
                continue;
            }

            if (memory.State == MemoryState.Superseded)
            {
                selection.Skipped.Add(new SkippedMemory
                {
                    Memory = memory,
                    SkipReason = "Superseded",
                    RelevanceScore = 0
                });
                continue;
            }

            if (memory.State == MemoryState.Archived)
            {
                selection.Skipped.Add(new SkippedMemory
                {
                    Memory = memory,
                    SkipReason = "Archived",
                    RelevanceScore = 0
                });
                continue;
            }

            eligible.Add(memory);
        }

        if (eligible.Count == 0)
        {
            selection.BudgetExhausted = true;
            return selection;
        }

        // ── Step 2: Rank eligible memories ──
        var ranker = new MemoryRanker();
        var ranked = ranker.Rank(eligible, query, context);

        // ── Step 3: Deduplicate by normalized content ──
        var deduplicated = DeduplicateByContent(ranked);

        // ── Step 4: Select within budget ──
        var remainingBudget = budget;

        foreach (var rankedMemory in deduplicated)
        {
            var estimatedTokens = EstimateTokens(rankedMemory.Memory);

            if (estimatedTokens <= remainingBudget)
            {
                selection.Selected.Add(new SelectedMemory
                {
                    Memory = rankedMemory.Memory,
                    RelevanceScore = rankedMemory.RelevanceScore,
                    SelectionReason = rankedMemory.SelectionReason,
                    EstimatedTokens = estimatedTokens
                });
                remainingBudget -= estimatedTokens;
                selection.EstimatedCost += estimatedTokens;
            }
            else
            {
                selection.Skipped.Add(new SkippedMemory
                {
                    Memory = rankedMemory.Memory,
                    SkipReason = $"Exceeds remaining budget ({estimatedTokens} > {remainingBudget})",
                    RelevanceScore = rankedMemory.RelevanceScore
                });
            }
        }

        selection.BudgetExhausted = remainingBudget <= 0;
        return selection;
    }

    private static List<RankedMemory> DeduplicateByContent(List<RankedMemory> ranked)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RankedMemory>();

        foreach (var item in ranked)
        {
            var normalized = item.Memory.NormalizedContent ??
                item.Memory.Content.ToLowerInvariant().Replace("  ", " ").Trim();

            if (seen.Add(normalized))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static int EstimateTokens(MemoryEntry memory)
    {
        var textLength = memory.Title.Length + memory.Content.Length;
        return (int)Math.Ceiling(textLength / CharsPerToken);
    }
}
