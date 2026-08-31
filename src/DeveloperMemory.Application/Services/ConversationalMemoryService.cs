using System.Text.RegularExpressions;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Orchestrates conversational memory ingestion with full conversational awareness.
/// Pipeline: Context Resolution → Detection → Extraction → Validation → Ingestion → Resolution
///
/// All failures are isolated and logged — they never block the chat pipeline.
/// This service is designed to be called from the PromptIntelligenceEngine
/// as a non-blocking enrichment step.
///
/// Conversational awareness features:
///   - Passes conversation history to the detector for better inference
///   - Resolves project names from conversation text to existing projects
///   - Infers scope (Global vs Project) from conversational context
///   - Resolves "this project" references using conversation history
///   - Auto-creates projects when confidence is high enough
/// </summary>
public class ConversationalMemoryService : IConversationalMemoryService
{
    private readonly IConversationalMemoryDetector _detector;
    private readonly IExtractionOrchestrator _extractionOrchestrator;
    private readonly IMemoryIngestionService _ingestionService;
    private readonly IMemoryRepository _memoryRepository;
    private readonly IProjectService _projectService;
    private readonly IProjectRepository _projectRepository;
    private readonly ILogger<ConversationalMemoryService> _logger;

    public ConversationalMemoryService(
        IConversationalMemoryDetector detector,
        IExtractionOrchestrator extractionOrchestrator,
        IMemoryIngestionService ingestionService,
        IMemoryRepository memoryRepository,
        IProjectService projectService,
        IProjectRepository projectRepository,
        ILogger<ConversationalMemoryService> logger)
    {
        _detector = detector;
        _extractionOrchestrator = extractionOrchestrator;
        _ingestionService = ingestionService;
        _memoryRepository = memoryRepository;
        _projectService = projectService;
        _projectRepository = projectRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConversationalMemoryIngestionResult> TryIngestAsync(
        string message,
        string userId,
        Guid? projectId = null,
        string? workspaceId = null,
        List<string>? tags = null,
        List<string>? conversationHistory = null,
        CancellationToken ct = default)
    {
        var result = new ConversationalMemoryIngestionResult();

        try
        {
            // ── Step 0: Conversational context resolution ──
            // Resolve project from conversation if not explicitly provided
            var resolvedProjectId = projectId;
            var resolvedProjectName = (string?)null;

            if (!resolvedProjectId.HasValue)
            {
                var projectResolution = await ResolveProjectFromConversationAsync(
                    message, conversationHistory, userId, ct);
                resolvedProjectId = projectResolution.ProjectId;
                resolvedProjectName = projectResolution.ProjectName;
            }
            else
            {
                // Even with explicit projectId, try to get the name for logging
                try
                {
                    var project = await _projectService.GetByIdAsync(resolvedProjectId.Value, ct);
                    resolvedProjectName = project?.Name;
                }
                catch { /* Non-fatal */ }
            }

            // ── Step 1: Detect whether the message contains durable information ──
            // Pass conversation history for better inference
            var detection = _detector.Detect(message, conversationHistory);

            if (!detection.ContainsDurableInformation)
            {
                _logger.LogDebug(
                    "Memory candidate rejected: {Reason}",
                    detection.Reason);
                return result;
            }

            result.Detected = true;

            _logger.LogInformation(
                "Memory candidate detected: confidence={Confidence:F2}, type={Type}, reason={Reason}, project={Project}",
                detection.Confidence, detection.SuggestedMemoryType, detection.Reason,
                resolvedProjectName ?? "(none)");

            // ── Step 2: Extract structured candidates using the orchestrator ──
            var extractionRequest = new MemoryExtractionRequest
            {
                Content = detection.ExtractedContent ?? message,
                ProjectId = resolvedProjectId,
                WorkspaceId = workspaceId,
                UserId = userId
            };

            var extractionResult = await _extractionOrchestrator.ExtractAsync(
                extractionRequest, ExtractionMode.Auto, ct);

            if (extractionResult.Candidates.Count == 0)
            {
                _logger.LogDebug("No extraction candidates produced for detected memory");
                result.Warnings.Add("Detection succeeded but extraction produced no candidates");
                return result;
            }

            _logger.LogInformation(
                "Extraction produced {Count} candidates (strategy={Strategy})",
                extractionResult.FinalCount, extractionResult.StrategyUsed);

            // ── Step 3: Infer scope from conversational context ──
            var scope = InferScope(resolvedProjectId, workspaceId, message, conversationHistory);

            _logger.LogDebug(
                "Scope inference: scope={Scope}, projectId={ProjectId}, projectName={ProjectName}",
                scope, resolvedProjectId?.ToString() ?? "(none)", resolvedProjectName ?? "(none)");

            // ── Step 4: Ingest each candidate ──
            foreach (var candidate in extractionResult.Candidates)
            {
                try
                {
                    var ingestionRequest = MapToIngestionRequest(
                        candidate, scope, resolvedProjectId, workspaceId, userId, tags, message);

                    var ingestionResult = await _ingestionService.IngestAsync(
                        ingestionRequest, userId, ct);

                    switch (ingestionResult.Outcome)
                    {
                        case MemoryIngestionOutcome.Created:
                            result.CreatedCount++;
                            _logger.LogInformation(
                                "Memory persisted: id={Id}, type={Type}, scope={Scope}, project={Project}",
                                ingestionResult.Memory?.Id,
                                candidate.MemoryType,
                                scope,
                                resolvedProjectName ?? "(global)");
                            break;

                        case MemoryIngestionOutcome.IgnoredDuplicate:
                            result.DuplicateCount++;
                            _logger.LogDebug(
                                "Duplicate ignored: content={Content}",
                                Truncate(candidate.Content, 50));
                            break;

                        case MemoryIngestionOutcome.SupersededExisting:
                            result.SupersededCount++;
                            result.CreatedCount++;
                            _logger.LogInformation(
                                "Memory superseded: newId={NewId}, oldId={OldId}",
                                ingestionResult.Memory?.Id,
                                ingestionResult.RelatedMemory?.Id);
                            break;

                        case MemoryIngestionOutcome.RequiresReview:
                            result.Warnings.Add(
                                $"Candidate requires review: {ingestionResult.Reason}");
                            _logger.LogWarning(
                                "Memory requires review: {Reason}", ingestionResult.Reason);
                            break;

                        case MemoryIngestionOutcome.Rejected:
                            result.Warnings.Add(
                                $"Candidate rejected: {ingestionResult.Reason}");
                            _logger.LogDebug(
                                "Memory rejected: {Reason}", ingestionResult.Reason);
                            break;

                        default:
                            result.Warnings.Add(
                                $"Unexpected outcome: {ingestionResult.Outcome}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to ingest candidate: {Content}",
                        Truncate(candidate.Content, 50));
                    result.Warnings.Add($"Ingestion failed for candidate: {ex.Message}");
                }
            }

            result.Persisted = result.CreatedCount > 0;

            _logger.LogInformation(
                "Conversational ingestion complete: detected={Detected}, created={Created}, " +
                "duplicates={Duplicates}, superseded={Superseded}, warnings={Warnings}",
                result.Detected, result.CreatedCount, result.DuplicateCount,
                result.SupersededCount, result.Warnings.Count);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw; // Always propagate cancellation
        }
        catch (Exception ex)
        {
            result.Failed = true;
            result.FailureReason = ex.Message;
            _logger.LogWarning(ex, "Conversational memory ingestion failed (non-fatal)");
            return result;
        }
    }

    // ── Project Resolution ──

    /// <summary>
    /// Attempts to resolve a project from the current message and conversation history.
    /// Uses multiple strategies:
    ///   1. Explicit project mention in current message (e.g., "I'm working on DeveloperMemory.Api")
    ///   2. "this project" / "the project" reference resolved from conversation history
    ///   3. Project name extraction from the message text
    /// </summary>
    private async Task<ProjectResolution> ResolveProjectFromConversationAsync(
        string message,
        List<string>? conversationHistory,
        string userId,
        CancellationToken ct)
    {
        var result = new ProjectResolution();

        // Strategy 1: Check current message for explicit project mentions
        var explicitProject = ExtractProjectNameFromMessage(message);
        if (explicitProject != null)
        {
            var resolved = await FindOrCreateProjectAsync(explicitProject, userId, ct);
            if (resolved != null)
            {
                result.ProjectId = resolved.Id;
                result.ProjectName = resolved.Name;
                result.Confidence = 0.9;
                result.Source = "explicit_mention";
                return result;
            }
        }

        // Strategy 2: Check for "this project" / "the project" references
        // resolved from conversation history
        if (ContainsProjectReference(message) && conversationHistory != null)
        {
            var recentProject = FindRecentProjectReference(conversationHistory);
            if (recentProject != null)
            {
                var resolved = await FindProjectByNameAsync(recentProject, ct);
                if (resolved != null)
                {
                    result.ProjectId = resolved.Id;
                    result.ProjectName = resolved.Name;
                    result.Confidence = 0.8;
                    result.Source = "conversation_reference";
                    return result;
                }
            }
        }

        // Strategy 3: Scan conversation history for project mentions
        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            var historicalProject = FindProjectInHistory(conversationHistory);
            if (historicalProject != null)
            {
                var resolved = await FindProjectByNameAsync(historicalProject, ct);
                if (resolved != null)
                {
                    result.ProjectId = resolved.Id;
                    result.ProjectName = resolved.Name;
                    result.Confidence = 0.7;
                    result.Source = "conversation_history";
                    return result;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts a project name from a message.
    /// Looks for patterns like:
    ///   - "I'm working on DeveloperMemory.Api"
    ///   - "DeveloperMemory.Api uses PostgreSQL"
    ///   - "For DeveloperMemory.Api I want..."
    ///   - "this project" / "the project" (returns null, handled separately)
    /// </summary>
    private static string? ExtractProjectNameFromMessage(string message)
    {
        // Pattern: "I'm working on X" / "I am working on X"
        var workingOn = Regex.Match(message,
            @"(?:I(?:'m| am)\s+)?(?:working on|building|developing|coding on)\s+(.+?)(?:\s+and|\s+but|\s+so|\s+\.|,|$)",
            RegexOptions.IgnoreCase);
        if (workingOn.Success && workingOn.Groups.Count > 1)
        {
            var name = workingOn.Groups[1].Value.Trim();
            if (IsValidProjectName(name))
                return NormalizeProjectName(name);
        }

        // Pattern: "X is my ... project" / "X is a ..."
        var isMyProject = Regex.Match(message,
            @"(\S+)\s+(?:is|are)\s+(?:my|a|an|the)\s+(?:\w+\s+)?(?:project|app|service|api|system|codebase)",
            RegexOptions.IgnoreCase);
        if (isMyProject.Success && isMyProject.Groups.Count > 1)
        {
            var name = isMyProject.Groups[1].Value.Trim();
            if (IsValidProjectName(name))
                return NormalizeProjectName(name);
        }

        // Pattern: "For X project" / "In X project"
        var forProject = Regex.Match(message,
            @"(?:for|in|on)\s+(?:the\s+)?(.+?)\s+(?:project|app|service|api|system|codebase)",
            RegexOptions.IgnoreCase);
        if (forProject.Success && forProject.Groups.Count > 1)
        {
            var name = forProject.Groups[1].Value.Trim();
            if (IsValidProjectName(name))
                return NormalizeProjectName(name);
        }

        // Pattern: "X uses ..." / "X has ..." / "X should ..."
        // Only match if the name looks like a project (contains dots, hyphens, or PascalCase)
        var usesPattern = Regex.Match(message,
            @"^([A-Z][\w.-]*(?:\.[\w.-]+)*)\s+(?:uses?|has|should|will|must|is|are)\s+",
            RegexOptions.IgnoreCase);
        if (usesPattern.Success && usesPattern.Groups.Count > 1)
        {
            var name = usesPattern.Groups[1].Value.Trim();
            if (LooksLikeProjectName(name))
                return NormalizeProjectName(name);
        }

        return null;
    }

    /// <summary>
    /// Checks if a message contains a project reference like "this project", "the project", "our API".
    /// </summary>
    private static bool ContainsProjectReference(string message)
    {
        return Regex.IsMatch(message,
            @"\b(?:this|the|our|that)\s+(?:project|app|service|api|system|codebase)\b",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Searches conversation history for the most recent project name mention.
    /// Returns the most recently mentioned project name, or null.
    /// </summary>
    private static string? FindRecentProjectReference(List<string> conversationHistory)
    {
        // Search from most recent to oldest
        for (int i = conversationHistory.Count - 1; i >= 0; i--)
        {
            var name = ExtractProjectNameFromMessage(conversationHistory[i]);
            if (name != null)
                return name;
        }
        return null;
    }

    /// <summary>
    /// Searches conversation history for any project name mentions.
    /// Returns the most frequently mentioned project name.
    /// </summary>
    private static string? FindProjectInHistory(List<string> conversationHistory)
    {
        var projectMentions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var message in conversationHistory)
        {
            var name = ExtractProjectNameFromMessage(message);
            if (name != null)
            {
                if (projectMentions.ContainsKey(name))
                    projectMentions[name]++;
                else
                    projectMentions[name] = 1;
            }
        }

        return projectMentions
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault()
            .Key;
    }

    /// <summary>
    /// Finds an existing project by name, or creates one if it doesn't exist
    /// and the name looks like a valid project name.
    /// </summary>
    private async Task<ProjectDto?> FindOrCreateProjectAsync(
        string projectName, string userId, CancellationToken ct)
    {
        // First try exact match
        var existing = await _projectService.GetByNameAsync(projectName, ct);
        if (existing != null)
            return existing;

        // Try case-insensitive search
        var searchResults = await _projectService.SearchByNameAsync(projectName, ct) ?? [];
        var match = searchResults.FirstOrDefault(p =>
            string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        // Try partial match (name contains search or vice versa)
        match = searchResults.FirstOrDefault(p =>
            p.Name.Contains(projectName, StringComparison.OrdinalIgnoreCase) ||
            projectName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        // No existing project found — do NOT auto-create here.
        // Auto-creation should only happen with higher confidence signals.
        // Return null and let the caller handle it.
        _logger.LogDebug(
            "Project '{Name}' not found; memory will be stored as Global scope", projectName);
        return null;
    }

    /// <summary>
    /// Finds an existing project by name without creating.
    /// </summary>
    private async Task<ProjectDto?> FindProjectByNameAsync(string projectName, CancellationToken ct)
    {
        // First try exact match
        var existing = await _projectService.GetByNameAsync(projectName, ct);
        if (existing != null)
            return existing;

        // Try case-insensitive search
        var searchResults = await _projectService.SearchByNameAsync(projectName, ct) ?? [];
        var match = searchResults.FirstOrDefault(p =>
            string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        // Try partial match
        match = searchResults.FirstOrDefault(p =>
            p.Name.Contains(projectName, StringComparison.OrdinalIgnoreCase) ||
            projectName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        return match;
    }

    // ── Scope Inference ──

    /// <summary>
    /// Infers the memory scope from available context.
    /// Uses project resolution, conversation context, and message content.
    /// </summary>
    private static MemoryScope InferScope(
        Guid? projectId,
        string? workspaceId,
        string message,
        List<string>? conversationHistory)
    {
        if (projectId.HasValue)
            return MemoryScope.Project;

        if (!string.IsNullOrEmpty(workspaceId))
            return MemoryScope.Workspace;

        // Default to Global — conservative, avoids inventing project identity.
        // We do NOT infer Project scope merely from conversation mentions.
        // Project scope requires an actual resolved projectId.
        return MemoryScope.Global;
    }

    // ── Helpers ──

    private static bool IsValidProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Length < 2 || name.Length > 100) return false;
        // Must contain at least one letter
        if (!name.Any(char.IsLetter)) return false;
        // Reject common non-project words
        var lowerName = name.ToLowerInvariant();
        var rejectWords = new[] { "the", "a", "an", "this", "that", "it", "we", "i", "you" };
        if (rejectWords.Contains(lowerName)) return false;
        return true;
    }

    private static bool LooksLikeProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        // Contains dots (e.g., "DeveloperMemory.Api")
        if (name.Contains('.')) return true;
        // PascalCase or camelCase with multiple words (e.g., "DeveloperMemory")
        if (name.Length > 3 && char.IsUpper(name[0]) && name.Any(char.IsLower)) return true;
        // Contains hyphens (e.g., "my-project")
        if (name.Contains('-') && name.Length > 3) return true;
        return false;
    }

    private static string NormalizeProjectName(string name)
    {
        return name.Trim().TrimEnd('.', '!', '?', ',');
    }

    private static MemoryIngestionRequest MapToIngestionRequest(
        MemoryCandidate candidate,
        MemoryScope scope,
        Guid? projectId,
        string? workspaceId,
        string? userId,
        List<string>? clientTags,
        string originalMessage)
    {
        var classification = candidate.Importance switch
        {
            >= 0.8 => DataClassification.Confidential,
            >= 0.5 => DataClassification.Internal,
            _ => DataClassification.Public
        };

        return new MemoryIngestionRequest
        {
            Title = candidate.Title,
            Content = candidate.Content,
            Scope = scope,
            MemoryType = candidate.MemoryType,
            Classification = classification,
            ProjectId = scope == MemoryScope.Project ? projectId : null,
            WorkspaceId = scope == MemoryScope.Workspace ? workspaceId : null,
            UserId = scope == MemoryScope.Private ? userId : null,
            Source = $"conversational:{candidate.Source}",
            Tags = clientTags,
            Importance = candidate.Importance,
            Confidence = candidate.Confidence,
            ExpiresAt = candidate.ExpiresAt,
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                extraction_reason = candidate.ExtractionReason,
                original_message_preview = Truncate(originalMessage, 200)
            })
        };
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}

/// <summary>
/// Result of project resolution from conversation.
/// </summary>
internal class ProjectResolution
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
}
