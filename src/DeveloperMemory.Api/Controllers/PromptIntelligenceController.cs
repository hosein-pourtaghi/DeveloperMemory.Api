using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// Prompt Intelligence API endpoints.
/// Provides analysis-only and context-aware prompt preparation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PromptIntelligenceController : ControllerBase
{
    private readonly IPromptIntelligenceEngine _intelligenceEngine;
    private readonly IIntentAnalyzer _intentAnalyzer;
    private readonly IContextOrchestrator _contextOrchestrator;
    private readonly PromptConstructionEngine _constructionEngine;
    private readonly DeterministicPromptOptimizer _optimizer;
    private readonly ILogger<PromptIntelligenceController> _logger;

    public PromptIntelligenceController(
        IPromptIntelligenceEngine intelligenceEngine,
        IIntentAnalyzer intentAnalyzer,
        IContextOrchestrator contextOrchestrator,
        PromptConstructionEngine constructionEngine,
        DeterministicPromptOptimizer optimizer,
        ILogger<PromptIntelligenceController> logger)
    {
        _intelligenceEngine = intelligenceEngine;
        _intentAnalyzer = intentAnalyzer;
        _contextOrchestrator = contextOrchestrator;
        _constructionEngine = constructionEngine;
        _optimizer = optimizer;
        _logger = logger;
    }

    /// <summary>
    /// Analyze input and return memory candidates without persisting them.
    /// Returns intent analysis, relevant context, token estimate, and optimized prompt.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<object>> Analyze(
        [FromBody] PromptAnalyzeRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new { error = new { message = "Input is required.", code = "validation_error" } });
        }

        _logger.LogInformation("Prompt analysis requested: {InputLength} chars", request.Input.Length);

        // Step 1: Intent Analysis
        var intent = await _intentAnalyzer.AnalyzeAsync(request.Input, null, ct);

        // Step 2: Context Orchestration
        var contextRequest = new ContextOrchestrationRequest
        {
            Input = request.Input,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            UserId = request.UserId,
            TokenBudget = request.TokenBudget,
            IncludeMemory = request.IncludeMemory,
            IncludeProjectContext = request.IncludeProjectContext,
            IntentAnalysis = intent
        };

        var context = await _contextOrchestrator.OrchestrateAsync(contextRequest, ct);

        // Step 3: Prompt Construction
        var construction = _constructionEngine.Construct(intent, context, request.Input);

        // Step 4: Optimization
        var optimization = _optimizer.Optimize(construction);

        return Ok(new
        {
            intent = new
            {
                intent.Intent,
                intent.TaskType,
                intent.TechnicalDomain,
                intent.Complexity,
                intent.RiskLevel,
                intent.GoalSummary,
                intent.IsMemoryInstruction,
                intent.RequiresProjectContext,
                intent.Keywords,
                intent.TechnicalContext,
                intent.ExplicitConstraints
            },
            context = new
            {
                selectedMemories = context.SelectedMemories.Select(m => new
                {
                    m.MemoryId,
                    m.Content,
                    m.MemoryType,
                    m.Score,
                    m.Reason,
                    m.Priority,
                    m.EstimatedTokens
                }),
                skippedMemories = context.SkippedMemories.Select(s => new
                {
                    s.MemoryId,
                    s.SkipReason
                }),
                projectContext = context.ProjectContext != null ? new
                {
                    context.ProjectContext.ProjectName,
                    context.ProjectContext.ArchitectureRules,
                    context.ProjectContext.TechnologyStack,
                    context.ProjectContext.CodingConventions
                } : null,
                estimatedTokens = context.EstimatedTokens,
                budgetExceeded = context.BudgetExceeded,
                conflictsDetected = context.ConflictsDetected
            },
            prompt = new
            {
                optimization.OptimizedPrompt,
                sections = construction.Sections.Select(s => new
                {
                    s.Type,
                    s.Header,
                    s.EstimatedTokens
                }),
                totalEstimatedTokens = construction.TotalEstimatedTokens,
                injectionDefenseApplied = construction.InjectionDefenseApplied,
                optimizationApplied = optimization.OptimizationApplied,
                tokensSaved = optimization.EstimatedTokensSaved
            },
            metadata = new
            {
                timestamp = DateTime.UtcNow,
                version = "1.0"
            }
        });
    }

    /// <summary>
    /// Process a request through the full intelligence pipeline.
    /// Returns a complete PromptPackage ready for downstream consumption.
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult<object>> Process(
        [FromBody] PromptProcessRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new { error = new { message = "Input is required.", code = "validation_error" } });
        }

        var package = await _intelligenceEngine.ProcessAsync(
            request.Input,
            request.UserId ?? "anonymous",
            request.ProjectId,
            request.WorkspaceId,
            request.TokenBudget,
            ct);

        return Ok(new
        {
            status = package.Status.ToString(),
            originalRequest = package.OriginalRequest,
            analysis = new
            {
                package.Analysis.Intent,
                package.Analysis.TaskType,
                package.Analysis.Keywords
            },
            optimizedPrompt = package.OptimizedPrompt,
            metadata = new
            {
                package.Metadata.TotalDurationMs,
                package.Metadata.CandidateMemoryCount,
                package.Metadata.RefinedMemoryCount,
                package.Metadata.DuplicatesRemoved,
                package.Metadata.ConflictsDetected,
                package.Metadata.FinalPromptLength,
                package.Warnings,
                package.DegradationReasons
            }
        });
    }
}

/// <summary>
/// Request for prompt analysis.
/// </summary>
public class PromptAnalyzeRequest
{
    /// <summary>The user input to analyze.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Workspace identifier.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>User identifier.</summary>
    public string? UserId { get; set; }

    /// <summary>Maximum tokens for context.</summary>
    public int TokenBudget { get; set; } = 4000;

    /// <summary>Whether to include memory context.</summary>
    public bool IncludeMemory { get; set; } = true;

    /// <summary>Whether to include project context.</summary>
    public bool IncludeProjectContext { get; set; } = true;
}

/// <summary>
/// Request for full prompt processing.
/// </summary>
public class PromptProcessRequest
{
    /// <summary>The user input.</summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>Project identifier.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Workspace identifier.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>User identifier.</summary>
    public string? UserId { get; set; }

    /// <summary>Maximum tokens for context.</summary>
    public int TokenBudget { get; set; } = 4000;
}
