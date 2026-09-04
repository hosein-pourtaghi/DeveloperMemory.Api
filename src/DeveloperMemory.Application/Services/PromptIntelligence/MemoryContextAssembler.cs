using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Assembles organized context sections from retrieved memories.
/// Handles deduplication, contradiction detection, and logical grouping.
/// 
/// Does NOT perform retrieval or bypass privacy boundaries.
/// Operates only on already-filtered, privacy-compliant memories from PromptContext.
/// </summary>
public class MemoryContextAssembler : IMemoryContextAssembler
{
    public ContextAssemblyResult Assemble(
        PromptContext context,
        PromptAnalysis analysis,
        List<PromptConstraint> constraints)
    {
        var result = new ContextAssemblyResult();

        if (context.RetrievedMemories.Count == 0)
        {
            return result;
        }

        // Step 1: Deduplicate similar memories
        var deduplicated = DeduplicateMemories(context.RetrievedMemories, result);

        // Step 2: Detect contradictions
        result.Contradictions = DetectContradictions(deduplicated);

        // Step 3: Group into organized sections
        result.Sections = OrganizeIntoSections(deduplicated, analysis, constraints, context);

        return result;
    }

    /// <summary>
    /// Deduplicates memories with similar content.
    /// Conservative: when uncertain, preserves information.
    /// </summary>
    private static List<RetrievedMemory> DeduplicateMemories(
        List<RetrievedMemory> memories,
        ContextAssemblyResult result)
    {
        var kept = new List<RetrievedMemory>();
        var removed = new HashSet<Guid>();

        for (int i = 0; i < memories.Count; i++)
        {
            if (removed.Contains(memories[i].MemoryId))
                continue;

            kept.Add(memories[i]);

            // Compare with subsequent memories
            for (int j = i + 1; j < memories.Count; j++)
            {
                if (removed.Contains(memories[j].MemoryId))
                    continue;

                if (AreDuplicates(memories[i], memories[j]))
                {
                    // Keep the one with higher importance or more recent update
                    var keepHigher = memories[i].Importance >= memories[j].Importance &&
                                     memories[i].UpdatedAt >= memories[j].UpdatedAt;

                    if (keepHigher)
                    {
                        removed.Add(memories[j].MemoryId);
                        result.DuplicatesRemoved++;
                    }
                    else
                    {
                        removed.Add(memories[i].MemoryId);
                        result.DuplicatesRemoved++;
                        // Remove from kept and add the other one
                        kept.RemoveAt(kept.Count - 1);
                        kept.Add(memories[j]);
                        break;
                    }
                }
            }
        }

        return kept;
    }

    /// <summary>
    /// Determines if two memories are duplicates (contain essentially the same information).
    /// Uses content similarity with a conservative threshold.
    /// </summary>
    private static bool AreDuplicates(RetrievedMemory a, RetrievedMemory b)
    {
        // Same memory ID = always duplicate
        if (a.MemoryId == b.MemoryId) return true;

        // Exact content match
        if (string.Equals(a.Content, b.Content, StringComparison.OrdinalIgnoreCase))
            return true;

        // High content overlap (Jaccard similarity on words)
        var wordsA = GetWords(a.Content);
        var wordsB = GetWords(b.Content);

        if (wordsA.Count == 0 || wordsB.Count == 0) return false;

        var intersection = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();

        var similarity = (double)intersection / union;

        // Conservative threshold: only deduplicate when very similar
        return similarity > 0.85;
    }

    /// <summary>
    /// Detects contradictions between memories.
    /// Foundation-level detection using keyword opposition.
    /// </summary>
    private static List<ContradictionInfo> DetectContradictions(List<RetrievedMemory> memories)
    {
        var contradictions = new List<ContradictionInfo>();

        for (int i = 0; i < memories.Count; i++)
        {
            for (int j = i + 1; j < memories.Count; j++)
            {
                var contradiction = CheckForContradiction(memories[i], memories[j]);
                if (contradiction != null)
                {
                    contradictions.Add(contradiction);
                }
            }
        }

        return contradictions;
    }

    private static ContradictionInfo? CheckForContradiction(RetrievedMemory a, RetrievedMemory b)
    {
        // Check for version/technology contradictions
        var aText = $"{a.Title} {a.Content}".ToLowerInvariant();
        var bText = $"{b.Title} {b.Content}".ToLowerInvariant();

        // Look for explicit version contradictions
        var aVersion = ExtractVersion(aText);
        var bVersion = ExtractVersion(bText);

        if (!string.IsNullOrEmpty(aVersion) && !string.IsNullOrEmpty(bVersion) &&
            !string.Equals(aVersion, bVersion, StringComparison.OrdinalIgnoreCase))
        {
            // Same technology mentioned with different versions — possible contradiction
            // Determine which to prefer: newer update time or higher importance
            var preferred = a.UpdatedAt >= b.UpdatedAt && a.Importance >= b.Importance
                ? a.MemoryId
                : b.MemoryId;

            return new ContradictionInfo
            {
                MemoryId1 = a.MemoryId,
                MemoryId2 = b.MemoryId,
                Description = $"Version conflict: {aVersion} vs {bVersion}",
                PreferredMemoryId = preferred
            };
        }

        // Check for negation contradictions (e.g., "use X" vs "don't use X")
        var aNegated = IsNegationOf(aText, bText);
        var bNegated = IsNegationOf(bText, aText);

        if (aNegated || bNegated)
        {
            var preferred = a.UpdatedAt >= b.UpdatedAt
                ? a.MemoryId
                : b.MemoryId;

            return new ContradictionInfo
            {
                MemoryId1 = a.MemoryId,
                MemoryId2 = b.MemoryId,
                Description = $"Negation conflict between memories",
                PreferredMemoryId = preferred
            };
        }

        return null;
    }

    private static string ExtractVersion(string text)
    {
        // Look for patterns like ".net 8", ".net 10", "v2.0", etc.
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:\.net|v|version)\s*(\d+\.?\d*)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static bool IsNegationOf(string text, string possibleOriginal)
    {
        var negations = new[] { "do not ", "don't ", "never ", "avoid ", "no ", "not " };
        foreach (var negation in negations)
        {
            if (text.Contains(negation))
            {
                // Strip the negation and check if the rest matches the original
                var stripped = text.Replace(negation, "").Trim();
                if (similarContent(stripped, possibleOriginal))
                    return true;
            }
        }
        return false;
    }

    private static bool similarContent(string a, string b)
    {
        var wordsA = GetWords(a);
        var wordsB = GetWords(b);
        if (wordsA.Count == 0 || wordsB.Count == 0) return false;

        var intersection = wordsA.Intersect(wordsB).Count();
        return (double)intersection / Math.Max(wordsA.Count, wordsB.Count) > 0.7;
    }

    /// <summary>
    /// Organizes memories into logical sections based on analysis and constraints.
    /// </summary>
    private static List<ContextSection> OrganizeIntoSections(
        List<RetrievedMemory> memories,
        PromptAnalysis analysis,
        List<PromptConstraint> constraints,
        PromptContext context)
    {
        var sections = new List<ContextSection>();

        // Section 1: Project Context (project-scoped memories)
        var projectMemories = memories
            .Where(m => m.Scope == MemoryScope.Project)
            .ToList();
        if (projectMemories.Count > 0)
        {
            sections.Add(BuildSection("project_context", "Project Context", 10, projectMemories));
        }

        // Section 2: Constraints and Rules
        if (constraints.Count > 0)
        {
            var section = new ContextSection
            {
                SectionId = "constraints",
                Heading = "Applicable Rules and Constraints",
                Order = 20,
                Items = constraints.Select(c => new ContextItem
                {
                    Content = $"[{c.Type}] {c.Value}",
                    Label = c.Source.ToString(),
                    SourceMemoryId = c.SourceMemoryId
                }).ToList()
            };
            section.EstimatedTokens = EstimateSectionTokens(section);
            sections.Add(section);
        }

        // Section 3: Relevant Memory (global + private, task-relevant).
        // Workspace memories have their own section so they are not emitted twice.
        var relevantMemories = memories
            .Where(m => m.Scope is MemoryScope.Global or MemoryScope.Private)
            .ToList();
        if (relevantMemories.Count > 0)
        {
            sections.Add(BuildSection("relevant_memory", "Relevant Memory", 30, relevantMemories));
        }

        // Section 4: Workspace Context (workspace-scoped memories)
        var workspaceMemories = memories
            .Where(m => m.Scope == MemoryScope.Workspace)
            .ToList();
        if (workspaceMemories.Count > 0)
        {
            sections.Add(BuildSection("workspace_context", "Workspace Context", 15, workspaceMemories));
        }

        return sections.OrderBy(s => s.Order).ToList();
    }

    private static ContextSection BuildSection(string id, string heading, int order, List<RetrievedMemory> memories)
    {
        var section = new ContextSection
        {
            SectionId = id,
            Heading = heading,
            Order = order,
            Items = memories.Select(m => new ContextItem
            {
                Content = $"[{m.Title}] {TruncateContent(m.Content, 300)}",
                Label = $"{m.Scope} | Importance: {m.Importance:F1}",
                SourceMemoryId = m.MemoryId,
                Importance = m.Importance
            }).OrderByDescending(i => i.Importance).ToList()
        };

        section.EstimatedTokens = EstimateSectionTokens(section);
        return section;
    }

    private static int EstimateSectionTokens(ContextSection section)
    {
        var totalChars = section.Items.Sum(i => i.Content.Length + (i.Label?.Length ?? 0));
        return (int)Math.Ceiling(totalChars / 4.0);
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return content.Length <= maxLength ? content : content[..maxLength] + "...";
    }

    private static HashSet<string> GetWords(string text)
    {
        var words = System.Text.RegularExpressions.Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(w => w.Length > 2)
            .ToHashSet();
        return words;
    }
}
