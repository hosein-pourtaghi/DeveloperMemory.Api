using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Orchestrates agent-aware context retrieval.
/// 
/// Pipeline:
///   1. Resolve agent context (IAgentContextProvider)
///   2. Build RetrievalRequest enriched with agent signals
///   3. Delegate to existing MemoryRetrievalService (Phase-S pipeline)
///   4. Assemble context sections from ranked memories
///   5. Extract instructions/constraints from memories
/// 
/// This service does NOT replace the existing retrieval pipeline.
/// It enriches the RetrievalRequest with agent-specific context signals
/// that the Phase-S RelevanceRanker already knows how to score.
/// </summary>
public class AgentContextService : IAgentContextService
{
    private readonly IAgentContextProvider _contextProvider;
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly ILogger<AgentContextService> _logger;

    public AgentContextService(
        IAgentContextProvider contextProvider,
        IMemoryRetrievalService retrievalService,
        ILogger<AgentContextService> logger)
    {
        _contextProvider = contextProvider;
        _retrievalService = retrievalService;
        _logger = logger;
    }

    public async Task<AgentContextResult> RetrieveContextAsync(
        AgentContextRetrievalRequest request,
        string ownerId,
        CancellationToken ct = default)
    {
        // Step 1: Resolve agent context
        var agentContext = _contextProvider.Resolve(new AgentContextRequest
        {
            AgentId = request.AgentId,
            AgentType = request.AgentType,
            Task = request.Task,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            Tags = request.Tags,
            Constraints = request.Constraints,
            ConversationHistory = request.ConversationHistory
        });

        _logger.LogInformation(
            "Agent context resolved: agent={AgentId}, type={AgentType}, intent={Intent}, " +
            "project={ProjectId}, confidence={Confidence:F2}",
            agentContext.AgentId, agentContext.AgentType, agentContext.TaskIntent,
            agentContext.ProjectId?.ToString() ?? "(none)", agentContext.Confidence);

        // Step 2: Build enriched RetrievalRequest
        // Use the task description as query if no explicit query provided
        var query = !string.IsNullOrWhiteSpace(request.Query)
            ? request.Query
            : DeriveQueryFromTask(request.Task, agentContext.TaskIntent);

        var retrievalRequest = new RetrievalRequest
        {
            OwnerId = ownerId,
            UserId = ownerId,
            ProjectId = agentContext.ProjectId,
            WorkspaceId = agentContext.WorkspaceId,
            Query = query,
            MaximumResults = request.MaxResults,
            ContextTokenBudget = request.ContextTokenBudget,
            RequiredCategories = BuildRequiredCategories(agentContext),
            ExcludedCategories = BuildExcludedCategories(agentContext)
        };

        // Step 3: Delegate to existing retrieval pipeline (Phase-S)
        var retrievalResult = await _retrievalService.RetrieveAsync(retrievalRequest, ct);

        // Step 4: Assemble context sections from ranked memories
        var contextSections = AssembleContextSections(retrievalResult.Memories, agentContext);

        // Step 5: Extract instructions/constraints
        var instructions = ExtractInstructions(retrievalResult.Memories);

        var result = new AgentContextResult
        {
            AgentContext = agentContext,
            Memories = retrievalResult.Memories,
            ContextSections = contextSections,
            Instructions = instructions,
            Metadata = retrievalResult.Metadata,
            TotalCandidates = retrievalResult.Metadata.CandidateCount,
            SelectedCount = retrievalResult.Metadata.SelectedCount,
            EstimatedTokensUsed = retrievalResult.Metadata.EstimatedTokensUsed
        };

        _logger.LogInformation(
            "Agent context retrieval complete: agent={AgentId}, candidates={Candidates}, " +
            "selected={Selected}, sections={Sections}, tokens={Tokens}",
            agentContext.AgentId, result.TotalCandidates, result.SelectedCount,
            contextSections.Count, result.EstimatedTokensUsed);

        return result;
    }

    // ── Private helpers ──

    private static string DeriveQueryFromTask(string? task, TaskIntent intent)
    {
        if (string.IsNullOrWhiteSpace(task))
            return string.Empty;

        // For memory capture, use the full task
        if (intent == TaskIntent.MemoryCapture)
            return task;

        // For other intents, use the task as-is (it's already descriptive)
        return task;
    }

    /// <summary>
    /// Builds required category tags based on agent type and task intent.
    /// These map to tag values on memories. If categories are set, only memories
    /// with at least one matching tag are included.
    /// 
    /// Returns null (no filter) for all agent types because the Phase-S
    /// RelevanceRanker already applies memory-type relevance scoring.
    /// Filtering by tags that may not exist on memories would incorrectly
    /// exclude relevant results.
    /// </summary>
    private static List<string>? BuildRequiredCategories(AgentContext agentContext)
    {
        return null;
    }

    /// <summary>
    /// Builds excluded categories based on agent context.
    /// For example, DevOps agents don't need frontend conventions.
    /// Returns null (no exclusion) by default.
    /// </summary>
    private static List<string>? BuildExcludedCategories(AgentContext agentContext)
    {
        return agentContext.AgentType switch
        {
            AgentType.DevOps => ["frontend"],
            _ => null
        };
    }

    /// <summary>
    /// Assembles structured context sections from ranked memories.
    /// Groups memories by their semantic role in the context.
    /// </summary>
    private static List<AgentContextSection> AssembleContextSections(
        List<RetrievedMemory> memories, AgentContext agentContext)
    {
        var sections = new List<AgentContextSection>();

        // Group memories by memory type
        var grouped = memories
            .Where(m => m.State == MemoryState.Active)
            .GroupBy(m => m.MemoryType);

        foreach (var group in grouped)
        {
            var sectionType = group.Key.ToString();
            var memories_list = group.ToList();

            var section = new AgentContextSection
            {
                SectionType = sectionType,
                Title = FormatSectionTitle(group.Key),
                Content = string.Join("\n", memories_list.Select(m => $"- {m.Content}")),
                RelevanceScore = memories_list.Average(m => m.RelevanceScore),
                ContributingMemoryIds = memories_list.Select(m => m.MemoryId).ToList()
            };

            sections.Add(section);
        }

        // Sort sections by relevance
        return sections
            .OrderByDescending(s => s.RelevanceScore)
            .ToList();
    }

    /// <summary>
    /// Extracts instructions and constraints from retrieved memories.
    /// Instructions and constraints are high-priority context for agents.
    /// </summary>
    private static List<string> ExtractInstructions(List<RetrievedMemory> memories)
    {
        var instructions = new List<string>();

        foreach (var memory in memories)
        {
            if (memory.State != MemoryState.Active) continue;

            if (memory.MemoryType == MemoryType.Instruction ||
                memory.MemoryType == MemoryType.UserConstraint)
            {
                instructions.Add(memory.Content);
            }
        }

        return instructions;
    }

    private static string FormatSectionTitle(MemoryType type)
    {
        return type switch
        {
            MemoryType.UserPreference => "Preferences",
            MemoryType.UserGoal => "Goals",
            MemoryType.UserConstraint => "Constraints",
            MemoryType.ProjectContext => "Project Context",
            MemoryType.ArchitectureDecision => "Architecture Decisions",
            MemoryType.TechnicalDecision => "Technical Decisions",
            MemoryType.WorkingContext => "Working Context",
            MemoryType.Instruction => "Instructions",
            MemoryType.Fact => "Facts",
            MemoryType.ConversationContext => "Conversation Context",
            _ => "Other"
        };
    }
}
