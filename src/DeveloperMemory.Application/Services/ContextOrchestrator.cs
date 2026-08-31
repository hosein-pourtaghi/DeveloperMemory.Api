using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Orchestrates context gathering from memory, project context, and rules.
/// Produces a unified, token-budget-respecting context.
///
/// Pipeline:
///   Intent Analysis
///     → Memory Retrieval
///     → Project Context
///     → Priority Ranking
///     → Token Budget Selection
///     → Deduplication
///     → Final Context
/// </summary>
public class ContextOrchestrator : IContextOrchestrator
{
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly IProjectContextProvider _projectContextProvider;
    private readonly ILogger<ContextOrchestrator> _logger;

    // Approximate tokens per character (English text ≈ 4 chars/token)
    private const double CharsPerToken = 4.0;

    // Budget allocation percentages
    private const double InstructionsBudgetPct = 0.20;
    private const double RulesBudgetPct = 0.15;
    private const double ProjectContextBudgetPct = 0.25;
    private const double MemoryBudgetPct = 0.40;

    public ContextOrchestrator(
        IMemoryRetrievalService retrievalService,
        IProjectContextProvider projectContextProvider,
        ILogger<ContextOrchestrator> logger)
    {
        _retrievalService = retrievalService;
        _projectContextProvider = projectContextProvider;
        _logger = logger;
    }

    public async Task<ContextOrchestrationResult> OrchestrateAsync(
        ContextOrchestrationRequest request,
        CancellationToken ct = default)
    {
        var result = new ContextOrchestrationResult();
        var budget = request.TokenBudget;

        // ── Step 1: Memory Retrieval ──
        if (request.IncludeMemory && !string.IsNullOrWhiteSpace(request.Input))
        {
            try
            {
                var retrievalRequest = new RetrievalRequest
                {
                    OwnerId = request.OwnerId ?? string.Empty,
                    UserId = request.UserId ?? string.Empty,
                    ProjectId = request.ProjectId,
                    WorkspaceId = request.WorkspaceId,
                    Query = request.Input,
                    MaximumResults = 20,
                    ContextTokenBudget = (int)(budget * MemoryBudgetPct)
                };

                var retrievalResult = await _retrievalService.RetrieveAsync(retrievalRequest, ct);

                var memoryBudget = (int)(budget * MemoryBudgetPct);
                var selectedTokens = 0;

                foreach (var memory in retrievalResult.Memories)
                {
                    var estimatedTokens = EstimateTokens(memory.Content);

                    if (selectedTokens + estimatedTokens > memoryBudget)
                    {
                        result.SkippedMemories.Add(new SkippedContextItem
                        {
                            MemoryId = memory.MemoryId,
                            SkipReason = $"Exceeds memory budget ({memoryBudget} tokens)"
                        });
                        continue;
                    }

                    result.SelectedMemories.Add(new ContextMemoryItem
                    {
                        MemoryId = memory.MemoryId,
                        Content = memory.Content,
                        MemoryType = memory.MemoryType.ToString(),
                        Score = memory.RelevanceScore,
                        Reason = memory.EligibilityReason,
                        Priority = CalculatePriority(memory),
                        EstimatedTokens = estimatedTokens
                    });

                    selectedTokens += estimatedTokens;
                }

                result.EstimatedTokens += selectedTokens;
                result.BudgetUsed += selectedTokens;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory retrieval failed during context orchestration");
                result.Warnings.Add("Memory retrieval unavailable");
            }
        }

        // ── Step 2: Project Context ──
        if (request.IncludeProjectContext && _projectContextProvider.IsAvailable)
        {
            try
            {
                var projectContext = await _projectContextProvider.GetContextAsync(
                    request.ProjectId, request.WorkspaceId, ct);

                if (projectContext != null)
                {
                    var projectBudget = (int)(budget * ProjectContextBudgetPct);
                    var projectTokens = 0;

                    // Add architecture rules
                    foreach (var rule in projectContext.ArchitectureRules)
                    {
                        var tokens = EstimateTokens(rule);
                        if (projectTokens + tokens > projectBudget) break;
                        projectTokens += tokens;
                    }

                    // Add tech stack
                    foreach (var tech in projectContext.TechnologyStack)
                    {
                        var tokens = EstimateTokens(tech);
                        if (projectTokens + tokens > projectBudget) break;
                        projectTokens += tokens;
                    }

                    result.ProjectContext = projectContext;
                    result.EstimatedTokens += projectTokens;
                    result.BudgetUsed += projectTokens;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Project context loading failed");
                result.Warnings.Add("Project context unavailable");
            }
        }

        // ── Step 3: Check budget ──
        result.BudgetExceeded = result.BudgetUsed > budget;

        if (result.BudgetExceeded)
        {
            result.Warnings.Add($"Token budget exceeded: {result.BudgetUsed}/{budget}");
            _logger.LogWarning(
                "Token budget exceeded: {Used}/{Budget}",
                result.BudgetUsed, budget);
        }

        // ── Step 4: Detect conflicts ──
        result.ConflictsDetected = DetectConflicts(result.SelectedMemories);

        _logger.LogDebug(
            "Context orchestration: {Memories} memories, {Skipped} skipped, {Tokens} tokens, {Conflicts} conflicts",
            result.SelectedMemories.Count, result.SkippedMemories.Count,
            result.EstimatedTokens, result.ConflictsDetected);

        return result;
    }

    private static int CalculatePriority(RetrievedMemory memory)
    {
        // Instructions and constraints get highest priority
        return memory.MemoryType switch
        {
            MemoryType.Instruction => 100,
            MemoryType.UserConstraint => 95,
            MemoryType.ArchitectureDecision => 85,
            MemoryType.TechnicalDecision => 80,
            MemoryType.UserPreference => 70,
            MemoryType.ProjectContext => 65,
            MemoryType.UserGoal => 60,
            MemoryType.Fact => 50,
            MemoryType.WorkingContext => 40,
            _ => 30
        };
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    private static int DetectConflicts(List<ContextMemoryItem> memories)
    {
        int conflicts = 0;

        // Simple conflict detection: look for contradictory keywords
        var conflictPairs = new[]
        {
            ("postgresql", "sql server"), ("postgresql", "mysql"),
            ("redis", "memcached"), ("docker", "bare metal"),
            ("async", "synchronous"), ("minimal api", "controller")
        };

        for (int i = 0; i < memories.Count; i++)
        {
            for (int j = i + 1; j < memories.Count; j++)
            {
                var contentA = memories[i].Content.ToLowerInvariant();
                var contentB = memories[j].Content.ToLowerInvariant();

                foreach (var (term1, term2) in conflictPairs)
                {
                    if ((contentA.Contains(term1) && contentB.Contains(term2)) ||
                        (contentA.Contains(term2) && contentB.Contains(term1)))
                    {
                        conflicts++;
                    }
                }
            }
        }

        return conflicts;
    }
}
