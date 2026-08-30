using System.Text.RegularExpressions;
using DeveloperMemory.Application.Contracts;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Deterministic agent context provider. Classifies agent type and task intent
/// from request data using pattern matching. No LLM calls.
/// 
/// Resolution order:
///   1. Explicit agent type from request
///   2. Infer from agent ID patterns (e.g., "cursor" → Coding)
///   3. Infer from task description patterns
///   4. Default to General
/// </summary>
public partial class AgentContextProvider : IAgentContextProvider
{
    public AgentContext Resolve(AgentContextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return new AgentContext
            {
                AgentId = "anonymous",
                AgentType = AgentType.General,
                TaskIntent = ClassifyTaskIntent(request.Task),
                TaskDescription = request.Task ?? string.Empty,
                ProjectId = request.ProjectId,
                WorkspaceId = request.WorkspaceId,
                Tags = request.Tags ?? [],
                Constraints = request.Constraints ?? [],
                ConversationHistory = request.ConversationHistory ?? [],
                Confidence = 0.5,
                ResolutionExplanation = "No agent ID provided; using default context"
            };
        }

        var agentType = request.AgentType ?? InferAgentType(request.AgentId, request.Task);
        var taskIntent = ClassifyTaskIntent(request.Task);
        var query = DeriveQuery(request.Task, taskIntent);

        return new AgentContext
        {
            AgentId = request.AgentId,
            AgentType = agentType,
            TaskDescription = request.Task ?? string.Empty,
            TaskIntent = taskIntent,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            Tags = request.Tags ?? [],
            Constraints = request.Constraints ?? [],
            ConversationHistory = request.ConversationHistory ?? [],
            ProjectExplicit = request.ProjectId.HasValue,
            Confidence = CalculateConfidence(request),
            ResolutionExplanation = BuildExplanation(agentType, taskIntent, request)
        };
    }

    /// <summary>
    /// Infers agent type from agent ID and task description.
    /// </summary>
    private static AgentType InferAgentType(string agentId, string? task)
    {
        var idLower = agentId.ToLowerInvariant();

        // Direct agent ID pattern matching (using Contains for reliability with hyphens)
        if (idLower.Contains("copilot") || idLower.Contains("cursor") || idLower.Contains("windsurf") ||
            idLower.Contains("codeium") || idLower.Contains("tabnine") || idLower.Contains("aider") ||
            idLower.Contains("continue") || idLower.Contains("cline") || idLower.Contains("devin"))
            return AgentType.Coding;
        if (idLower.Contains("doc") || idLower.Contains("documentation") || idLower.Contains("sphinx") ||
            idLower.Contains("mkdocs") || idLower.Contains("docusaurus"))
            return AgentType.Documentation;
        if (idLower.Contains("planner") || idLower.Contains("planning") || idLower.Contains("roadmap") ||
            idLower.Contains("jira") || idLower.Contains("linear"))
            return AgentType.Planning;
        if (idLower.Contains("test") || idLower.Contains("qa") || idLower.Contains("cypress") ||
            idLower.Contains("playwright") || idLower.Contains("selenium"))
            return AgentType.Testing;
        if (idLower.Contains("devops") || idLower.Contains("deploy") || idLower.Contains("terraform") ||
            idLower.Contains("k8s") || idLower.Contains("kubernetes"))
            return AgentType.DevOps;

        // Infer from task description if agent ID is ambiguous
        // Order: more specific patterns first (documentation before coding, since "write" is generic)
        if (!string.IsNullOrWhiteSpace(task))
        {
            var taskLower = task.ToLowerInvariant();

            if (DocumentationPatterns().IsMatch(taskLower))
                return AgentType.Documentation;
            if (TestingTaskPatterns().IsMatch(taskLower))
                return AgentType.Testing;
            if (DevOpsTaskPatterns().IsMatch(taskLower))
                return AgentType.DevOps;
            if (PlanningTaskPatterns().IsMatch(taskLower))
                return AgentType.Planning;
            if (CodingTaskPatterns().IsMatch(taskLower))
                return AgentType.Coding;
        }

        return AgentType.General;
    }

    /// <summary>
    /// Classifies the task intent from the task description.
    /// </summary>
    private static TaskIntent ClassifyTaskIntent(string? task)
    {
        if (string.IsNullOrWhiteSpace(task))
            return TaskIntent.General;

        var lower = task.ToLowerInvariant();

        if (MemoryCapturePatterns().IsMatch(lower))
            return TaskIntent.MemoryCapture;
        if (DebugPatterns().IsMatch(lower))
            return TaskIntent.Debug;
        if (ArchitecturePatterns().IsMatch(lower))
            return TaskIntent.Architecture;
        if (ImplementPatterns().IsMatch(lower))
            return TaskIntent.Implement;
        if (RefactorPatterns().IsMatch(lower))
            return TaskIntent.Refactor;
        if (DocumentationPatterns().IsMatch(lower))
            return TaskIntent.Documentation;
        if (TestingPatterns().IsMatch(lower))
            return TaskIntent.Testing;
        if (DeploymentPatterns().IsMatch(lower))
            return TaskIntent.Deployment;
        if (QueryPatterns().IsMatch(lower))
            return TaskIntent.Query;

        return TaskIntent.General;
    }

    /// <summary>
    /// Derives a search query from the task description and intent.
    /// </summary>
    private static string DeriveQuery(string? task, TaskIntent intent)
    {
        if (string.IsNullOrWhiteSpace(task))
            return string.Empty;

        // For memory capture, use the full task as query
        if (intent == TaskIntent.MemoryCapture)
            return task;

        // For other intents, extract key terms
        var lower = task.ToLowerInvariant();

        // Remove common filler phrases
        lower = QueryFillerPatterns().Replace(lower, " ").Trim();
        lower = Regex.Replace(lower, @"\s+", " ");

        return lower;
    }

    /// <summary>
    /// Calculates confidence in the resolved context.
    /// </summary>
    private static double CalculateConfidence(AgentContextRequest request)
    {
        double confidence = 0.5;

        if (!string.IsNullOrWhiteSpace(request.AgentId))
            confidence += 0.2;

        if (request.AgentType.HasValue)
            confidence += 0.1;

        if (!string.IsNullOrWhiteSpace(request.Task))
            confidence += 0.1;

        if (request.ProjectId.HasValue)
            confidence += 0.1;

        return Math.Min(confidence, 1.0);
    }

    private static string BuildExplanation(AgentType agentType, TaskIntent intent, AgentContextRequest request)
    {
        var parts = new List<string>();

        parts.Add($"Agent type: {agentType}");
        parts.Add($"Task intent: {intent}");

        if (request.ProjectId.HasValue)
            parts.Add("Project: explicit");
        else
            parts.Add("Project: none");

        if (!string.IsNullOrWhiteSpace(request.WorkspaceId))
            parts.Add($"Workspace: {request.WorkspaceId}");

        return string.Join("; ", parts);
    }

    // ── Task Patterns ──

    [GeneratedRegex(@"\b(implement|build|create|add|write|develop|code|construct)\b")]
    private static partial Regex CodingTaskPatterns();

    [GeneratedRegex(@"\b(plan|design|architect|structure|organize|strategy|roadmap)\b")]
    private static partial Regex PlanningTaskPatterns();

    [GeneratedRegex(@"\b(test|verify|validate|check|assert|spec|coverage)\b")]
    private static partial Regex TestingTaskPatterns();

    [GeneratedRegex(@"\b(deploy|release|ship|publish|ci|cd|pipeline|infrastructure)\b")]
    private static partial Regex DevOpsTaskPatterns();

    // ── Intent Patterns ──

    [GeneratedRegex(@"\b(remember|save|store|note|persist|capture|learn)\b")]
    private static partial Regex MemoryCapturePatterns();

    [GeneratedRegex(@"\b(fix|debug|error|bug|issue|broken|fail|crash|exception)\b")]
    private static partial Regex DebugPatterns();

    [GeneratedRegex(@"\b(architect|design|clean architecture|microservice|monolith|structure)\b")]
    private static partial Regex ArchitecturePatterns();

    [GeneratedRegex(@"\b(implement|build|create|add|write|develop|feature)\b")]
    private static partial Regex ImplementPatterns();

    [GeneratedRegex(@"\b(refactor|improve|optimize|simplify|clean|restructure)\b")]
    private static partial Regex RefactorPatterns();

    [GeneratedRegex(@"\b(document|doc|explain|describe|annotate|comment|readme)\b")]
    private static partial Regex DocumentationPatterns();

    [GeneratedRegex(@"\b(test|verify|validate|check|assert|spec|coverage)\b")]
    private static partial Regex TestingPatterns();

    [GeneratedRegex(@"\b(deploy|release|ship|publish|ci|cd|pipeline)\b")]
    private static partial Regex DeploymentPatterns();

    [GeneratedRegex(@"\b(what|how|why|when|where|who|which|tell me|show me|find|search)\b")]
    private static partial Regex QueryPatterns();

    [GeneratedRegex(@"\b(please|can you|could you|i need to|i want to|help me|let's|we should)\b")]
    private static partial Regex QueryFillerPatterns();
}
