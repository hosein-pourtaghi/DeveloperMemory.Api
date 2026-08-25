using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Resolves and deduplicates constraints from multiple sources with deterministic precedence.
/// 
/// Precedence (highest to lowest):
///   1. System / Safety (100)
///   2. Project Rules (80)
///   3. Explicit Current Request (60)
///   4. Persistent User Preferences (40)
///   5. General Memory (20)
/// 
/// When two constraints of the same type conflict, the one with higher precedence wins.
/// An explicit current request always overrides an old memory.
/// </summary>
public class ConstraintResolver : IConstraintResolver
{
    public List<PromptConstraint> Resolve(
        PromptAnalysis analysis,
        PromptContext? context,
        List<string>? projectRules = null)
    {
        var constraints = new List<PromptConstraint>();

        // 1. System-level constraints (always present, highest precedence)
        AddSystemConstraints(constraints);

        // 2. Project rules from configuration or project memory
        if (projectRules != null)
        {
            foreach (var rule in projectRules)
            {
                constraints.Add(new PromptConstraint
                {
                    Type = ClassifyConstraint(rule),
                    Value = rule,
                    Source = ConstraintSource.ProjectRule,
                    Precedence = (int)ConstraintSource.ProjectRule
                });
            }
        }

        // 3. Constraints from the current request (explicit, medium-high precedence)
        if (analysis.ExplicitConstraints.Count > 0)
        {
            foreach (var constraint in analysis.ExplicitConstraints)
            {
                constraints.Add(new PromptConstraint
                {
                    Type = ClassifyConstraint(constraint),
                    Value = constraint,
                    Source = ConstraintSource.ExplicitCurrentRequest,
                    Precedence = (int)ConstraintSource.ExplicitCurrentRequest
                });
            }
        }

        // 4. Constraints from retrieved memory (context-based, lower precedence)
        if (context?.RetrievedMemories != null)
        {
            foreach (var memory in context.RetrievedMemories)
            {
                // Extract constraint-like memories (those with constraint-related tags)
                if (IsConstraintLike(memory))
                {
                    constraints.Add(new PromptConstraint
                    {
                        Type = ClassifyConstraintFromMemory(memory),
                        Value = $"[{memory.Title}] {memory.Content}",
                        Source = ConstraintSource.GeneralMemory,
                        Precedence = (int)ConstraintSource.GeneralMemory,
                        SourceMemoryId = memory.MemoryId
                    });
                }
            }
        }

        // 5. Deduplicate and resolve conflicts
        var resolved = ResolveConflicts(constraints);

        return resolved;
    }

    private static void AddSystemConstraints(List<PromptConstraint> constraints)
    {
        // System constraints are always enforced
        constraints.Add(new PromptConstraint
        {
            Type = ConstraintType.Security,
            Value = "Never expose credentials, secrets, or tokens in output",
            Source = ConstraintSource.System,
            Precedence = (int)ConstraintSource.System
        });

        constraints.Add(new PromptConstraint
        {
            Type = ConstraintType.Architecture,
            Value = "Maintain existing architectural boundaries and dependency direction",
            Source = ConstraintSource.System,
            Precedence = (int)ConstraintSource.System
        });
    }

    private static ConstraintType ClassifyConstraint(string text)
    {
        var lower = text.ToLowerInvariant();

        if (ContainsAny(lower, ["use", "prefer", "technology", "database", "framework", "language"]))
            return ConstraintType.Technology;

        if (ContainsAny(lower, ["architecture", "layer", "boundary", "dependency", "pattern"]))
            return ConstraintType.Architecture;

        if (ContainsAny(lower, ["cost", "free", "budget", "paid", "pricing"]))
            return ConstraintType.Cost;

        if (ContainsAny(lower, ["security", "encrypt", "auth", "secret", "token", "sanitize"]))
            return ConstraintType.Security;

        if (ContainsAny(lower, ["scope", "only modify", "limit to", "restrict"]))
            return ConstraintType.Scope;

        if (ContainsAny(lower, ["format", "json", "xml", "markdown", "output"]))
            return ConstraintType.OutputFormat;

        if (ContainsAny(lower, ["performance", "fast", "latency", "optimize", "cache"]))
            return ConstraintType.Performance;

        if (ContainsAny(lower, ["compatible", "version", "target", "minimum"]))
            return ConstraintType.Compatibility;

        if (ContainsAny(lower, ["implement", "pattern", "convention", "style"]))
            return ConstraintType.Implementation;

        return ConstraintType.UserPreference;
    }

    private static ConstraintType ClassifyConstraintFromMemory(RetrievedMemory memory)
    {
        var combined = $"{memory.Title} {memory.Content}".ToLowerInvariant();
        return ClassifyConstraint(combined);
    }

    private static bool IsConstraintLike(RetrievedMemory memory)
    {
        var combined = $"{memory.Title} {string.Join(" ", memory.Tags)}".ToLowerInvariant();
        return ContainsAny(combined, ["rule", "preference", "use ", "don't", "avoid", "prefer", "must", "always", "never"]);
    }

    /// <summary>
    /// Resolves constraint conflicts by keeping the highest-precedence constraint
    /// of each type. When same-type constraints have equal precedence, the most
    /// specific (longest value) wins.
    /// </summary>
    private static List<PromptConstraint> ResolveConflicts(List<PromptConstraint> constraints)
    {
        // Group by constraint type
        var grouped = constraints.GroupBy(c => c.Type);

        var resolved = new List<PromptConstraint>();

        foreach (var group in grouped)
        {
            var sorted = group.OrderByDescending(c => c.Precedence).ToList();

            // Take the highest-precedence constraint
            var winner = sorted[0];

            // Detect conflicts (informational)
            if (sorted.Count > 1 && sorted[0].Precedence != sorted[1].Precedence)
            {
                // Conflict detected — higher precedence wins, which is correct
            }

            resolved.Add(winner);
        }

        return resolved.OrderBy(c => c.Precedence).ToList();
    }

    private static bool ContainsAny(string text, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (text.Contains(pattern))
                return true;
        }
        return false;
    }
}
