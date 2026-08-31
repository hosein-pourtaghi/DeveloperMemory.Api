using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic intent analyzer using keyword patterns and request structure.
/// Always provides a baseline result — no LLM required.
/// </summary>
public class DeterministicIntentAnalyzer : IIntentAnalyzer
{
    public Task<IntentAnalysisResult> AnalyzeAsync(
        string input,
        PromptContext? context = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(new IntentAnalysisResult
            {
                OriginalInput = input ?? string.Empty,
                Intent = IntentType.General,
                TaskType = TaskType.General,
                IsSimpleQuery = true
            });
        }

        var lower = input.ToLowerInvariant();

        var intent = DetectIntent(lower);
        var taskType = DetectTaskType(lower, intent);
        var domain = DetectTechnicalDomain(lower);
        var requiredContext = DetermineRequiredContext(intent, taskType, lower);
        var riskLevel = AssessRisk(lower);
        var complexity = AssessComplexity(lower, input.Length);
        var keywords = ExtractKeywords(input);
        var techContext = ExtractTechnicalContext(lower);
        var explicitConstraints = ExtractExplicitConstraints(input);
        var isMemoryInstruction = DetectMemoryInstruction(lower);

        return Task.FromResult(new IntentAnalysisResult
        {
            OriginalInput = input,
            Intent = intent,
            TaskType = taskType,
            TechnicalDomain = domain,
            RequiredContext = requiredContext,
            RiskLevel = riskLevel,
            Complexity = complexity,
            Keywords = keywords,
            TechnicalContext = techContext,
            ExplicitConstraints = explicitConstraints,
            GoalSummary = SummarizeGoal(input, intent),
            IsMemoryInstruction = isMemoryInstruction,
            RequiresProjectContext = requiredContext.Contains(RequiredContextType.ProjectArchitecture) ||
                                     requiredContext.Contains(RequiredContextType.ProjectDecisions),
            IsSimpleQuery = intent == IntentType.General && input.Length < 100
        });
    }

    private static IntentType DetectIntent(string lower)
    {
        if (HasAny(lower, ["fix this", "error", "bug", "crash", "exception", "failing", "broken", "does not work", "doesn't work", "won't compile"]))
            return IntentType.Debugging;

        if (HasAny(lower, ["design", "architecture", "structure", "restructure", "organize the system"]))
            return IntentType.Architecture;

        if (HasAny(lower, ["refactor", "clean up", "reorganize", "simplify", "extract method"]))
            return IntentType.Refactoring;

        if (HasAny(lower, ["document", "write docs", "readme", "xml comment", "api documentation"]))
            return IntentType.Documentation;

        if (HasAny(lower, ["research", "investigate", "compare", "evaluate", "alternatives"]))
            return IntentType.Research;

        if (HasAny(lower, ["explain", "why does", "how does", "what is", "describe"]))
            return IntentType.Explanation;

        if (HasAny(lower, ["plan", "roadmap", "phase", "implement the next", "break down"]))
            return IntentType.Planning;

        if (HasAny(lower, ["implement", "create", "add", "build", "write", "modify", "update", "change"]))
            return IntentType.Coding;

        return IntentType.General;
    }

    private static TaskType DetectTaskType(string lower, IntentType intent)
    {
        if (HasAny(lower, ["performance", "slow", "latency", "optimize", "benchmark"]))
            return TaskType.Performance;

        if (HasAny(lower, ["security", "vulnerability", "encrypt", "auth", "sanitize"]))
            return TaskType.Security;

        if (HasAny(lower, ["test", "spec", "assert", "mock", "unit test"]))
            return TaskType.Testing;

        return intent switch
        {
            IntentType.Coding => TaskType.Coding,
            IntentType.Debugging => TaskType.Debugging,
            IntentType.Architecture => TaskType.Architecture,
            IntentType.Documentation => TaskType.Documentation,
            IntentType.Research => TaskType.Research,
            IntentType.Explanation => TaskType.Explanation,
            IntentType.Refactoring => TaskType.Refactoring,
            IntentType.Planning => TaskType.Planning,
            _ => TaskType.General
        };
    }

    private static string DetectTechnicalDomain(string lower)
    {
        if (HasAny(lower, ["database", "sql", "query", "migration", "ef core", "entity framework"]))
            return "Database";

        if (HasAny(lower, ["api", "endpoint", "controller", "rest", "http", "swagger"]))
            return "API";

        if (HasAny(lower, ["architecture", "design", "pattern", "structure", "clean architecture"]))
            return "Architecture";

        if (HasAny(lower, ["test", "spec", "mock", "assert"]))
            return "Testing";

        if (HasAny(lower, ["deploy", "docker", "ci/cd", "pipeline", "kubernetes"]))
            return "DevOps";

        if (HasAny(lower, ["auth", "login", "security", "token", "jwt"]))
            return "Security";

        return "General";
    }

    private static List<RequiredContextType> DetermineRequiredContext(
        IntentType intent, TaskType taskType, string lower)
    {
        var context = new List<RequiredContextType>();

        // All requests may benefit from memory context
        context.Add(RequiredContextType.Memory);

        // Architecture/debugging/implementation requests benefit from project context
        if (intent is IntentType.Architecture or IntentType.Debugging or IntentType.Coding or IntentType.Planning)
        {
            context.Add(RequiredContextType.ProjectArchitecture);
        }

        // Coding requests may need conventions
        if (intent is IntentType.Coding or IntentType.Refactoring)
        {
            context.Add(RequiredContextType.CodingConventions);
        }

        // Technical stack queries
        if (HasAny(lower, ["stack", "technology", "framework", "library", "use"]))
        {
            context.Add(RequiredContextType.TechnicalStack);
        }

        // Decision queries
        if (intent is IntentType.Research or IntentType.Architecture)
        {
            context.Add(RequiredContextType.ProjectDecisions);
        }

        return context;
    }

    private static RiskLevel AssessRisk(string lower)
    {
        if (HasAny(lower, ["delete", "remove", "drop", "destroy", "irreversible", "production"]))
            return RiskLevel.High;

        if (HasAny(lower, ["migrate", "upgrade", "refactor", "restructure", "breaking change"]))
            return RiskLevel.Elevated;

        return RiskLevel.Normal;
    }

    private static ComplexityLevel AssessComplexity(string lower, int length)
    {
        if (length < 50 && !HasAny(lower, ["complex", "advanced", "multi", "distributed"]))
            return ComplexityLevel.Simple;

        if (HasAny(lower, ["complex", "advanced", "distributed", "microservice", "enterprise"]))
            return ComplexityLevel.Expert;

        if (HasAny(lower, ["multiple", "integrate", "orchestrate", "coordinate"]))
            return ComplexityLevel.Complex;

        return ComplexityLevel.Medium;
    }

    private static bool DetectMemoryInstruction(string lower)
    {
        return HasAny(lower, ["remember", "note that", "keep in mind", "always", "never", "must", "should not", "don't", "do not"]);
    }

    private static List<string> ExtractKeywords(string input)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "can", "shall", "this", "that", "these",
            "those", "it", "its", "to", "of", "in", "for", "on", "with", "at",
            "by", "from", "as", "into", "through", "during", "before", "after",
            "and", "but", "or", "so", "if", "then", "than", "too", "very",
            "not", "no", "nor", "just", "about", "up", "out", "i", "me", "my",
            "we", "our", "you", "your", "he", "she", "they", "them"
        };

        return System.Text.RegularExpressions.Regex.Split(input, @"\W+")
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .GroupBy(w => w.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(15)
            .Select(g => g.Key)
            .ToList();
    }

    private static List<string> ExtractTechnicalContext(string lower)
    {
        var techTerms = new List<string>();
        var knownTech = new Dictionary<string, string>
        {
            ["ef core"] = "EF Core", ["postgresql"] = "PostgreSQL", [".net"] = ".NET",
            ["c#"] = "C#", ["aspnet"] = "ASP.NET", ["react"] = "React",
            ["typescript"] = "TypeScript", ["docker"] = "Docker", ["redis"] = "Redis",
            ["clean architecture"] = "Clean Architecture", ["xunit"] = "xUnit"
        };

        foreach (var (term, label) in knownTech)
        {
            if (lower.Contains(term)) techTerms.Add(label);
        }

        return techTerms.Distinct().ToList();
    }

    private static List<string> ExtractExplicitConstraints(string input)
    {
        var constraints = new List<string>();
        var patterns = System.Text.RegularExpressions.Regex.Matches(
            input, @"""([^""]+)""");

        foreach (System.Text.RegularExpressions.Match match in patterns)
        {
            constraints.Add(match.Groups[1].Value.Trim());
        }

        return constraints;
    }

    private static string SummarizeGoal(string input, IntentType intent)
    {
        var prefix = intent switch
        {
            IntentType.Coding => "Code implementation",
            IntentType.Debugging => "Bug fix",
            IntentType.Architecture => "Architecture design",
            IntentType.Documentation => "Documentation",
            IntentType.Research => "Research",
            IntentType.Explanation => "Explanation",
            IntentType.Refactoring => "Refactoring",
            IntentType.Planning => "Planning",
            _ => "Task"
        };

        var truncated = input.Length > 100 ? input[..100] + "..." : input;
        return $"{prefix}: {truncated}";
    }

    private static bool HasAny(string text, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (text.Contains(pattern)) return true;
        }
        return false;
    }
}
