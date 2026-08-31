using System.Text.RegularExpressions;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic normalization of knowledge documents, developer profiles,
/// and raw content into CanonicalMemoryCandidate instances.
///
/// Normalization rules:
///   - Title: trimmed, non-empty (fallback to "Untitled")
///   - Content: trimmed, non-empty (reject if empty)
///   - NormalizedContent: lowercase, punctuation stripped, whitespace collapsed
///   - MemoryType: inferred from content patterns (matching existing ConversationalMemoryDetector patterns)
///   - Scope: derived from project association
///   - Classification: inferred from importance (matching existing ConversationalMemoryService)
///   - Source: "knowledge:{filename}" or "profile:{name}" or caller-specified
///   - Tags: union of caller tags + inferred tags
///   - Confidence: high for knowledge docs (0.9), moderate for profiles (0.7)
///   - Importance: 0.5 default, higher for architectural/technical decisions
///
/// This service is purely deterministic — no LLM calls, no I/O, no side effects.
/// </summary>
public class MemoryNormalizer : IMemoryNormalizationService
{
    private static readonly Regex PunctuationRegex = new(@"[^\w\s]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public IReadOnlyList<CanonicalMemoryCandidate> NormalizeKnowledgeDocument(
        string title,
        string content,
        string? project = null,
        List<string>? tags = null,
        string? filePath = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var candidates = new List<CanonicalMemoryCandidate>();

        // Split content into sections by ## headings if the document is substantial
        var sections = SplitIntoSections(content);

        if (sections.Count <= 1)
        {
            // Single section — one candidate
            var inferredType = InferMemoryType(content);
            var inferredImportance = InferImportance(content);
            candidates.Add(CreateCandidate(
                title,
                content,
                project: project,
                tags: tags,
                source: BuildSource("knowledge", filePath ?? title),
                memoryType: inferredType,
                importance: inferredImportance,
                confidence: 0.9));
        }
        else
        {
            // Multiple sections — each significant section becomes a candidate
            foreach (var (sectionTitle, sectionContent) in sections)
            {
                if (string.IsNullOrWhiteSpace(sectionContent) || sectionContent.Trim().Length < 10)
                    continue;

                var sectionTags = new List<string>(tags ?? []);
                var inferredType = InferMemoryType(sectionContent);
                var inferredImportance = InferImportance(sectionContent);

                candidates.Add(CreateCandidate(
                    sectionTitle,
                    sectionContent,
                    project: project,
                    tags: sectionTags,
                    source: BuildSource("knowledge", filePath ?? title),
                    memoryType: inferredType,
                    importance: inferredImportance,
                    confidence: 0.9));
            }
        }

        return candidates;
    }

    public IReadOnlyList<CanonicalMemoryCandidate> NormalizeDeveloperProfile(
        string name,
        string role,
        string bio,
        List<string>? skills = null,
        string? experience = null,
        string? filePath = null)
    {
        var candidates = new List<CanonicalMemoryCandidate>();
        var source = BuildSource("profile", filePath ?? name);

        // Role identity fact
        if (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(name))
        {
            candidates.Add(CreateCandidate(
                $"{name} Role",
                $"{name} is a {role}.",
                tags: ["developer-profile", "role"],
                source: source,
                memoryType: MemoryType.Fact,
                importance: 0.5,
                confidence: 0.7));
        }

        // Bio content
        if (!string.IsNullOrWhiteSpace(bio) && bio.Trim().Length > 10)
        {
            candidates.Add(CreateCandidate(
                $"{name} Bio",
                bio.Trim(),
                tags: ["developer-profile", "bio"],
                source: source,
                memoryType: MemoryType.Fact,
                importance: 0.4,
                confidence: 0.7));
        }

        // Skills
        if (skills != null && skills.Count > 0)
        {
            var skillList = string.Join(", ", skills);
            candidates.Add(CreateCandidate(
                $"{name} Skills",
                $"{name}'s skills: {skillList}",
                tags: ["developer-profile", "skills", .. skills.Take(5)],
                source: source,
                memoryType: MemoryType.Fact,
                importance: 0.6,
                confidence: 0.7));
        }

        // Experience
        if (!string.IsNullOrWhiteSpace(experience) && experience.Trim().Length > 5)
        {
            candidates.Add(CreateCandidate(
                $"{name} Experience",
                $"{name}'s experience: {experience.Trim()}",
                tags: ["developer-profile", "experience"],
                source: source,
                memoryType: MemoryType.Fact,
                importance: 0.4,
                confidence: 0.7));
        }

        return candidates;
    }

    public CanonicalMemoryCandidate NormalizeRaw(
        string title,
        string content,
        MemoryScope scope = MemoryScope.Global,
        Guid? projectId = null,
        string? source = null)
    {
        return CreateCandidate(
            title,
            content,
            scope: scope,
            projectId: projectId,
            source: source ?? "raw",
            memoryType: InferMemoryType(content),
            importance: InferImportance(content),
            confidence: 0.5);
    }

    // ── Private helpers ──

    private static CanonicalMemoryCandidate CreateCandidate(
        string title,
        string content,
        string? project = null,
        List<string>? tags = null,
        string? source = null,
        MemoryType memoryType = MemoryType.Fact,
        double importance = 0.5,
        double confidence = 0.5,
        MemoryScope scope = MemoryScope.Global,
        Guid? projectId = null)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        var normalizedContent = content.Trim();
        var normalized = ComputeNormalizedContent(normalizedTitle, normalizedContent);

        var effectiveScope = projectId.HasValue ? MemoryScope.Project :
                             !string.IsNullOrWhiteSpace(project) ? MemoryScope.Project :
                             scope;

        // Infer tags from content if none provided
        var effectiveTags = new List<string>(tags ?? []);
        AddInferredTags(normalizedContent, effectiveTags);

        return new CanonicalMemoryCandidate
        {
            Title = normalizedTitle,
            Content = normalizedContent,
            NormalizedContent = normalized,
            MemoryType = memoryType,
            Scope = effectiveScope,
            Classification = InferClassification(importance),
            ProjectId = projectId,
            Source = source ?? string.Empty,
            Tags = effectiveTags,
            Confidence = confidence,
            Importance = importance,
            MetadataJson = null
        };
    }

    private static string ComputeNormalizedContent(string title, string content)
    {
        var text = $"{title} {content}".ToLowerInvariant();
        text = PunctuationRegex.Replace(text, " ");
        text = WhitespaceRegex.Replace(text, " ").Trim();
        return text;
    }

    private static MemoryType InferMemoryType(string content)
    {
        var lower = content.ToLowerInvariant();

        // Match existing ConversationalMemoryDetector patterns.
        // Order: most specific first (architecture > technical > preference > constraint > instruction > identity > project > fact)
        if (Regex.IsMatch(lower, @"(?:remember|save|store|note)\s+(?:that\s+)?"))
            return MemoryType.Instruction;
        if (Regex.IsMatch(lower, @"(?:prefer|like|love|enjoy|favor)\s"))
            return MemoryType.UserPreference;
        if (Regex.IsMatch(lower, @"(?:don'?t|do not|never|avoid|no)\s+(?:use|recommend|suggest)"))
            return MemoryType.UserConstraint;
        if (Regex.IsMatch(lower, @"(?:architect|design pattern|clean architecture|microservice|monolith)"))
            return MemoryType.ArchitectureDecision;
        if (Regex.IsMatch(lower, @"(?:using|adopting|chose|selected|decided on)\s+"))
            return MemoryType.TechnicalDecision;
        if (Regex.IsMatch(lower, @"(?:this project|the project|we use|we follow)\s+"))
            return MemoryType.ProjectContext;
        if (Regex.IsMatch(lower, @"(?:i am|i'm|we are)\s+(?:a |an )?"))
            return MemoryType.Fact;

        return MemoryType.Fact;
    }

    private static double InferImportance(string content)
    {
        var lower = content.ToLowerInvariant();

        // Architecture/technical decisions are important
        if (Regex.IsMatch(lower, @"(?:architect|design|pattern|decision|chose|selected)"))
            return 0.8;
        // Constraints are important
        if (Regex.IsMatch(lower, @"(?:never|must not|cannot|don't use|avoid|no use)"))
            return 0.7;
        // Preferences are moderate
        if (Regex.IsMatch(lower, @"(?:prefer|like|love)"))
            return 0.6;
        // Facts and project context
        if (Regex.IsMatch(lower, @"(?:project|api|service|database|framework)"))
            return 0.5;

        return 0.4;
    }

    private static DataClassification InferClassification(double importance)
    {
        return importance switch
        {
            >= 0.8 => DataClassification.Confidential,
            >= 0.5 => DataClassification.Internal,
            _ => DataClassification.Public
        };
    }

    private static void AddInferredTags(string content, List<string> tags)
    {
        var lower = content.ToLowerInvariant();

        if (lower.Contains("database") || lower.Contains("postgresql") || lower.Contains("mysql") ||
            lower.Contains("redis") || lower.Contains("sql"))
            tags.Add("database");

        if (lower.Contains("api") || lower.Contains("endpoint") || lower.Contains("controller") ||
            lower.Contains("rest"))
            tags.Add("api");

        if (lower.Contains("test") || lower.Contains("spec") || lower.Contains("assertion"))
            tags.Add("testing");

        if (lower.Contains("security") || lower.Contains("auth") || lower.Contains("credential"))
            tags.Add("security");

        if (lower.Contains("docker") || lower.Contains("container") || lower.Contains("deploy"))
            tags.Add("deployment");

        if (lower.Contains("c#") || lower.Contains(".net") || lower.Contains("asp.net") ||
            lower.Contains("entity framework") || lower.Contains("ef core"))
            tags.Add("dotnet");

        if (Regex.IsMatch(lower, @"\b(?:react|angular|vue|blazor|typescript|javascript)\b"))
            tags.Add("frontend");
    }

    /// <summary>
    /// Splits markdown content into sections by ## headings.
    /// Returns a list of (title, content) tuples.
    /// </summary>
    private static List<(string Title, string Content)> SplitIntoSections(string content)
    {
        var sections = new List<(string Title, string Content)>();
        var lines = content.Split('\n');

        var currentTitle = string.Empty;
        var currentContent = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("## "))
            {
                // Save previous section
                if (currentContent.Length > 0)
                {
                    sections.Add((currentTitle, currentContent.ToString()));
                }
                currentTitle = line.TrimStart()[3..].Trim();
                currentContent.Clear();
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }

        // Save last section
        if (currentContent.Length > 0)
        {
            sections.Add((currentTitle, currentContent.ToString()));
        }

        return sections;
    }

    private static string BuildSource(string kind, string identifier)
    {
        // Trim path to just filename for readability
        var name = Path.GetFileNameWithoutExtension(identifier);
        return $"{kind}:{name}";
    }
}
