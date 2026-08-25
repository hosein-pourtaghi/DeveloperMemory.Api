using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using System.Text.RegularExpressions;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Deterministic, provider-independent prompt analyzer.
/// Uses keyword patterns, request structure, and known patterns for intent detection.
/// 
/// This implementation is intentionally simple and replaceable.
/// Future: LlmPromptAnalyzer can provide richer analysis.
/// </summary>
public partial class DeterministicPromptAnalyzer : IPromptAnalyzer
{
    public PromptAnalysis Analyze(string request, PromptContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return new PromptAnalysis
            {
                OriginalRequest = request ?? string.Empty,
                Intent = IntentType.General,
                TaskType = TaskType.General
            };
        }

        var lower = request.ToLowerInvariant();

        var intent = DetectIntent(lower);
        var taskType = DetectTaskType(lower, intent);
        var keywords = ExtractKeywords(request);
        var projectRefs = ExtractProjectReferences(request);
        var techContext = ExtractTechnicalContext(lower);
        var explicitConstraints = ExtractExplicitConstraints(request);

        return new PromptAnalysis
        {
            OriginalRequest = request,
            Intent = intent,
            TaskType = taskType,
            UserGoal = SummarizeGoal(request, intent),
            RequestedOutput = DetectRequestedOutput(lower),
            Keywords = keywords,
            ProjectReferences = projectRefs,
            TechnicalContext = techContext,
            ExplicitConstraints = explicitConstraints
        };
    }

    private static IntentType DetectIntent(string lower)
    {
        // Debugging patterns
        if (HasAnyPattern(lower, ["fix this", "error", "bug", "crash", "exception", "failing", "broken", "does not work", "doesn't work", "won't compile"]))
            return IntentType.Debugging;

        // Architecture patterns
        if (HasAnyPattern(lower, ["design", "architecture", "structure", "refactor the", "restructure", "organize the system"]))
            return IntentType.Architecture;

        // Refactoring patterns
        if (HasAnyPattern(lower, ["refactor", "clean up", "reorganize", "simplify", "extract method", "move to", "rename"]))
            return IntentType.Refactoring;

        // Documentation patterns
        if (HasAnyPattern(lower, ["document", "write docs", "readme", "xml comment", "api documentation", "generate documentation"]))
            return IntentType.Documentation;

        // Research patterns
        if (HasAnyPattern(lower, ["research", "investigate", "compare", "evaluate", "what are the options", "alternatives"]))
            return IntentType.Research;

        // Explanation patterns
        if (HasAnyPattern(lower, ["explain", "why does", "how does", "what is", "describe", "tell me about"]))
            return IntentType.Explanation;

        // Planning patterns
        if (HasAnyPattern(lower, ["plan", "roadmap", "phase", "implement the next", "what should we", "break down"]))
            return IntentType.Planning;

        // Coding patterns (broad catch for implementation requests)
        if (HasAnyPattern(lower, ["implement", "create", "add", "build", "write", "modify", "update", "change", "make", "code"]))
            return IntentType.Coding;

        return IntentType.General;
    }

    private static TaskType DetectTaskType(string lower, IntentType intent)
    {
        // Map intent to task type as primary classification
        var intentToTask = new Dictionary<IntentType, TaskType>
        {
            [IntentType.Coding] = TaskType.Coding,
            [IntentType.Debugging] = TaskType.Debugging,
            [IntentType.Architecture] = TaskType.Architecture,
            [IntentType.Documentation] = TaskType.Documentation,
            [IntentType.Research] = TaskType.Research,
            [IntentType.Explanation] = TaskType.Explanation,
            [IntentType.Refactoring] = TaskType.Refactoring,
            [IntentType.Planning] = TaskType.Planning,
            [IntentType.General] = TaskType.General
        };

        if (intentToTask.TryGetValue(intent, out var taskType))
        {
            // Refine with secondary signals
            if (HasAnyPattern(lower, ["performance", "slow", "latency", "optimize", "benchmark"]))
                return TaskType.Performance;

            if (HasAnyPattern(lower, ["security", "vulnerability", "encrypt", "auth", "sanitize", "hardening"]))
                return TaskType.Security;

            if (HasAnyPattern(lower, ["test", "spec", "assert", "mock", "unit test", "integration test"]))
                return TaskType.Testing;

            return taskType;
        }

        return TaskType.General;
    }

    private static List<string> ExtractKeywords(string request)
    {
        // Simple keyword extraction: split on common delimiters,
        // filter short/stop words, take top terms
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "can", "shall", "this", "that", "these",
            "those", "it", "its", "to", "of", "in", "for", "on", "with", "at",
            "by", "from", "as", "into", "through", "during", "before", "after",
            "and", "but", "or", "so", "if", "then", "than", "too", "very",
            "not", "no", "nor", "just", "about", "up", "out", "i", "me", "my",
            "we", "our", "you", "your", "he", "she", "they", "them", "what",
            "which", "who", "how", "when", "where", "why", "all", "each", "every"
        };

        var words = WordsRegex().Split(request)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .GroupBy(w => w.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => g.Key)
            .ToList();

        return words;
    }

    private static List<string> ExtractProjectReferences(string request)
    {
        var refs = new List<string>();

        // Look for explicit project references
        var projectMatches = ProjectRefRegex().Matches(request);
        foreach (Match match in projectMatches)
        {
            refs.Add(match.Value);
        }

        // Look for file references
        var fileMatches = FileRefRegex().Matches(request);
        foreach (Match match in fileMatches)
        {
            refs.Add(match.Value);
        }

        return refs.Distinct().ToList();
    }

    private static List<string> ExtractTechnicalContext(string lower)
    {
        var techTerms = new List<string>();

        var knownTech = new Dictionary<string, string>
        {
            ["ef core"] = "EF Core",
            ["entity framework"] = "Entity Framework",
            ["postgresql"] = "PostgreSQL",
            ["postgres"] = "PostgreSQL",
            [".net"] = ".NET",
            ["dotnet"] = ".NET",
            ["c#"] = "C#",
            ["aspnet"] = "ASP.NET",
            ["asp.net"] = "ASP.NET",
            ["react"] = "React",
            ["typescript"] = "TypeScript",
            ["docker"] = "Docker",
            ["kubernetes"] = "Kubernetes",
            ["redis"] = "Redis",
            ["sqlite"] = "SQLite",
            ["mongodb"] = "MongoDB",
            ["swagger"] = "Swagger",
            ["openapi"] = "OpenAPI",
            ["serilog"] = "Serilog",
            ["opentelemetry"] = "OpenTelemetry",
            ["clean architecture"] = "Clean Architecture",
            ["dependency injection"] = "Dependency Injection",
            ["xunit"] = "xUnit",
            ["nunit"] = "NUnit",
            ["mstest"] = "MSTest",
            ["nuget"] = "NuGet",
            ["linq"] = "LINQ",
            ["async"] = "async/await",
            ["middleware"] = "middleware",
            ["pipeline"] = "pipeline"
        };

        foreach (var (term, label) in knownTech)
        {
            if (lower.Contains(term))
            {
                techTerms.Add(label);
            }
        }

        return techTerms.Distinct().ToList();
    }

    private static List<string> ExtractExplicitConstraints(string request)
    {
        var constraints = new List<string>();

        var constraintPatterns = ConstraintPatternRegex().Matches(request);
        foreach (Match match in constraintPatterns)
        {
            constraints.Add(match.Groups[1].Value.Trim());
        }

        return constraints;
    }

    private static string DetectRequestedOutput(string lower)
    {
        if (HasAnyPattern(lower, ["return json", "as json", "json response"]))
            return "json";

        if (HasAnyPattern(lower, ["return xml", "as xml", "xml response"]))
            return "xml";

        if (HasAnyPattern(lower, ["as markdown", "in markdown", "format as markdown"]))
            return "markdown";

        if (HasAnyPattern(lower, ["as code", "code block", "in code"]))
            return "code";

        if (HasAnyPattern(lower, ["as table", "in table format", "tabular"]))
            return "table";

        return null;
    }

    private static string SummarizeGoal(string request, IntentType intent)
    {
        // For deterministic analysis, the goal is a concise summary based on intent
        var prefix = intent switch
        {
            IntentType.Coding => "Code implementation",
            IntentType.Debugging => "Bug fix / error resolution",
            IntentType.Architecture => "Architecture design",
            IntentType.Documentation => "Documentation",
            IntentType.Research => "Research / investigation",
            IntentType.Explanation => "Explanation",
            IntentType.Refactoring => "Code refactoring",
            IntentType.Planning => "Planning",
            _ => "Task"
        };

        return $"{prefix}: {Truncate(request, 100)}";
    }

    private static bool HasAnyPattern(string text, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (text.Contains(pattern))
                return true;
        }
        return false;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    [GeneratedRegex(@"[A-Za-z0-9_\-]+")]
    private static partial Regex WordsRegex();

    [GeneratedRegex(@"\b[\w\-]+\.(cs|csproj|sln|ts|tsx|js|jsx|py|json|md|yaml|yml|xml|sql|cshtml|razor)\b")]
    private static partial Regex FileRefRegex();

    [GeneratedRegex(@"\b(?:project|module|service|class|method)\s+[A-Za-z][\w]*")]
    private static partial Regex ProjectRefRegex();

    [GeneratedRegex(@"\"([^\"]+)\"")]
    private static partial Regex ConstraintPatternRegex();
}
