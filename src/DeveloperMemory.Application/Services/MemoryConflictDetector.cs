using System.Text.RegularExpressions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic conflict detector for memory entries.
/// Uses content similarity, type matching, and lifecycle rules.
/// Conservative: when uncertain, reports PotentialConflict rather than forcing a decision.
/// </summary>
public class MemoryConflictDetector : IMemoryConflictDetector
{
    public IReadOnlyList<MemoryConflict> DetectConflicts(
        MemoryEntry newMemory,
        IReadOnlyList<MemoryEntry> existingMemories)
    {
        var conflicts = new List<MemoryConflict>();

        foreach (var existing in existingMemories)
        {
            // Compare only with retrievable lifecycle states and ignore expired entries.
            if (existing.State is not (MemoryState.Active or MemoryState.Updated) || existing.IsExpired)
                continue;

            // Only compare within the same scope
            if (existing.Scope != newMemory.Scope) continue;

            // Compare content
            var conflict = CompareContent(newMemory, existing);
            if (conflict != null)
            {
                conflicts.Add(conflict);
            }
        }

        return conflicts.OrderByDescending(c => c.Confidence).ToList();
    }

    private static MemoryConflict? CompareContent(MemoryEntry newMemory, MemoryEntry existing)
    {
        // ── Exact duplicate ──
        if (string.Equals(
            newMemory.Content.Trim(),
            existing.Content.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            return new MemoryConflict
            {
                ExistingMemory = existing,
                ConflictType = MemoryConflictType.ExactDuplicate,
                Explanation = "Identical content",
                ShouldSupersede = false,
                Confidence = 1.0
            };
        }

        // ── Normalized duplicate ──
        var newNormalized = newMemory.NormalizedContent ?? Normalize(newMemory.Content);
        var existingNormalized = existing.NormalizedContent ?? Normalize(existing.Content);

        if (string.Equals(newNormalized, existingNormalized, StringComparison.OrdinalIgnoreCase))
        {
            return new MemoryConflict
            {
                ExistingMemory = existing,
                ConflictType = MemoryConflictType.NormalizedDuplicate,
                Explanation = "Same content after normalization",
                ShouldSupersede = false,
                Confidence = 0.95
            };
        }

        // ── High similarity check ──
        var similarity = ComputeSimilarity(newNormalized, existingNormalized);
        if (similarity > 0.85)
        {
            // Very similar content — likely an update
            return new MemoryConflict
            {
                ExistingMemory = existing,
                ConflictType = MemoryConflictType.UpdatedVersion,
                Explanation = $"High content similarity ({similarity:P0})",
                ShouldSupersede = true,
                Confidence = similarity
            };
        }

        // ── Contradiction check for same memory type ──
        if (newMemory.MemoryType == existing.MemoryType &&
            DetectContradiction(newMemory.Content, existing.Content))
        {
            return new MemoryConflict
            {
                ExistingMemory = existing,
                ConflictType = MemoryConflictType.Contradiction,
                Explanation = "Conflicting statements detected",
                ShouldSupersede = true, // Prefer newer information
                Confidence = 0.7
            };
        }

        return null;
    }

    private static bool DetectContradiction(string newContent, string existingContent)
    {
        var newLower = newContent.ToLowerInvariant();
        var existingLower = existingContent.ToLowerInvariant();

        // Simple negation pattern: "use X" vs "don't use X" / "avoid X" / "no X"
        var negations = new[] { "don't use ", "do not use ", "avoid ", "no ", "never ", "not " };

        foreach (var negation in negations)
        {
            if (newLower.Contains(negation) && !existingLower.Contains(negation))
            {
                // Check if the non-negated part matches
                var stripped = newLower.Replace(negation, "").Trim();
                if (ComputeSimilarity(stripped, existingLower) > 0.7)
                    return true;
            }

            if (existingLower.Contains(negation) && !newLower.Contains(negation))
            {
                var stripped = existingLower.Replace(negation, "").Trim();
                if (ComputeSimilarity(newLower, stripped) > 0.7)
                    return true;
            }
        }

        return false;
    }

    private static string Normalize(string content)
    {
        var text = content.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static double ComputeSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (wordsA.Length == 0 || wordsB.Length == 0) return 0.0;

        var setA = new HashSet<string>(wordsA);
        var setB = new HashSet<string>(wordsB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }
}
