using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
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
    /// Get all enabled prompt profiles.
    /// </summary>
    [HttpGet("profiles")]
    public async Task<ActionResult<object>> GetProfiles(
        IPromptProfileProvider profileProvider,
        CancellationToken ct)
    {
        var profiles = await profileProvider.GetEnabledProfilesAsync(ct);
        return Ok(profiles.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Version,
            p.Enabled,
            configuration = p.GetConfiguration()
        }));
    }

    /// <summary>
    /// Get a specific prompt profile by name.
    /// </summary>
    [HttpGet("profiles/{name}")]
    public async Task<ActionResult<object>> GetProfile(
        string name,
        IPromptProfileProvider profileProvider,
        CancellationToken ct)
    {
        var profile = await profileProvider.GetByNameAsync(name, ct);
        if (profile == null) return NotFound();

        return Ok(new
        {
            profile.Id,
            profile.Name,
            profile.Description,
            profile.Version,
            profile.Enabled,
            configuration = profile.GetConfiguration()
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
    /// Get processing history with optional filters.
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<object>> GetHistory(
        [FromServices] PromptProcessingRecordRepository historyRepo,
        [FromQuery] Guid? profileId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? optimizationMode,
        [FromQuery] string? validationStatus,
        [FromQuery] bool? fallbackUsed,
        [FromQuery] int maxResults = 50,
        CancellationToken ct = default)
    {
        var records = await historyRepo.QueryAsync(
            profileId, from, to, optimizationMode, validationStatus,
            fallbackUsed, maxResults, ct);

        return Ok(records.Select(r => new
        {
            r.Id,
            r.CorrelationId,
            r.CreatedAt,
            r.ProfileId,
            r.ProfileVersion,
            r.Intent,
            r.TaskType,
            r.OptimizationMode,
            r.Optimizer,
            r.WasLlmUsed,
            r.WasFallbackUsed,
            r.TokenBudget,
            r.EstimatedInputTokens,
            r.EstimatedOutputTokens,
            r.QualityScore,
            r.ValidationStatus,
            r.ProcessingDurationMs,
            r.ExperimentId,
            r.VariantId,
            r.MemoryCount,
            r.ConflictsDetected,
            r.QualityGatePassed
        }));
    }

    /// <summary>
    /// Get a specific processing record by ID.
    /// </summary>
    [HttpGet("history/{id:guid}")]
    public async Task<ActionResult<object>> GetHistoryRecord(
        Guid id,
        [FromServices] PromptProcessingRecordRepository historyRepo,
        CancellationToken ct)
    {
        var record = await historyRepo.GetByIdAsync(id, ct);
        if (record == null) return NotFound();

        return Ok(new
        {
            record.Id,
            record.CorrelationId,
            record.CreatedAt,
            record.ProfileId,
            record.ProfileVersion,
            record.Intent,
            record.TaskType,
            record.OptimizationMode,
            record.Optimizer,
            record.OptimizerVersion,
            record.Model,
            record.WasLlmUsed,
            record.WasFallbackUsed,
            record.TokenBudget,
            record.EstimatedInputTokens,
            record.EstimatedOutputTokens,
            record.QualityScore,
            record.ValidationStatus,
            record.ProcessingDurationMs,
            record.ExperimentId,
            record.VariantId,
            record.MemoryIdsUsed,
            record.ProjectId,
            record.WorkspaceId,
            record.MemoryCount,
            record.ConflictsDetected,
            record.QualityGatePassed,
            record.QualityGateFailureReason
        });
    }

    /// <summary>
    /// Get version history for a profile.
    /// </summary>
    [HttpGet("profiles/{name}/versions")]
    public async Task<ActionResult<object>> GetProfileVersions(
        string name,
        IPromptProfileProvider profileProvider,
        CancellationToken ct)
    {
        var profile = await profileProvider.GetByNameAsync(name, ct);
        if (profile == null) return NotFound();

        // If the provider is a PromptProfileRepository, we can get versions
        if (profileProvider is Persistence.PromptProfileRepository repo)
        {
            var versions = await repo.GetVersionsAsync(profile.Id, ct);
            return Ok(versions.Select(v => new
            {
                v.Id,
                v.Version,
                v.IsActive,
                v.CreatedAt,
                v.CreatedBy,
                v.ChangeDescription,
                configuration = v.GetConfiguration()
            }));
        }

        // Fallback: return just the current version
        return Ok(new[] { new
        {
            Id = profile.Id,
            profile.Version,
            IsActive = true,
            profile.CreatedAt,
            CreatedBy = "system",
            ChangeDescription = (string?)null,
            configuration = profile.GetConfiguration()
        } });
    }

    /// <summary>
    /// Create a new prompt profile.
    /// </summary>
    [HttpPost("profiles")]
    public async Task<ActionResult<object>> CreateProfile(
        [FromBody] CreateProfileRequest request,
        IPromptProfileProvider profileProvider,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = new { message = "Name is required.", code = "validation_error" } });
        }

        var existing = await profileProvider.GetByNameAsync(request.Name, ct);
        if (existing != null)
        {
            return Conflict(new { error = new { message = $"Profile '{request.Name}' already exists.", code = "conflict" } });
        }

        var profile = new Domain.Entities.PromptProfile
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Enabled = request.Enabled
        };

        if (request.Configuration != null)
        {
            profile.SetConfiguration(request.Configuration);
        }

        var created = await profileProvider.CreateAsync(profile, ct);

        return CreatedAtAction(nameof(GetProfile), new { name = created.Name }, new
        {
            created.Id,
            created.Name,
            created.Description,
            created.Version,
            created.Enabled,
            configuration = created.GetConfiguration()
        });
    }

    /// <summary>
    /// Rollback a profile to a specific version.
    /// </summary>
    [HttpPost("profiles/{name}/rollback")]
    public async Task<ActionResult<object>> RollbackProfile(
        string name,
        [FromBody] RollbackRequest request,
        IPromptProfileProvider profileProvider,
        CancellationToken ct)
    {
        if (profileProvider is not Persistence.PromptProfileRepository repo)
        {
            return BadRequest(new { error = new { message = "Profile rollback not supported in current configuration.", code = "unsupported" } });
        }

        var profile = await repo.GetByNameAsync(name, ct);
        if (profile == null) return NotFound();

        var result = await repo.RollbackAsync(profile.Id, request.TargetVersion, ct);
        if (result == null)
        {
            return NotFound(new { error = new { message = $"Version {request.TargetVersion} not found.", code = "version_not_found" } });
        }

        return Ok(new
        {
            result.Id,
            result.Name,
            result.Version,
            result.Description,
            result.Enabled,
            configuration = result.GetConfiguration()
        });
    }

    /// <summary>
    /// Get audit trail for a correlation ID.
    /// </summary>
    [HttpGet("audit/{correlationId}")]
    public async Task<ActionResult<object>> GetAuditTrail(
        string correlationId,
        IPromptIntelligenceAudit audit,
        CancellationToken ct)
    {
        var events = await audit.GetEventsByCorrelationAsync(correlationId, ct);
        return Ok(events.Select(e => new
        {
            e.Id,
            e.CorrelationId,
            e.CreatedAt,
            EventType = e.EventType.ToString(),
            e.ProcessingRecordId,
            e.ProfileId,
            e.Details
        }));
    }

    /// <summary>
    /// Get recent audit events.
    /// </summary>
    [HttpGet("audit")]
    public async Task<ActionResult<object>> GetRecentAuditEvents(
        IPromptIntelligenceAudit audit,
        [FromQuery] int count = 50,
        CancellationToken ct = default)
    {
        var events = await audit.GetRecentEventsAsync(count, ct);
        return Ok(events.Select(e => new
        {
            e.Id,
            e.CorrelationId,
            e.CreatedAt,
            EventType = e.EventType.ToString(),
            e.ProcessingRecordId,
            e.ProfileId,
            e.Details
        }));
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

/// <summary>
/// Request to create a new prompt profile.
/// </summary>
public class CreateProfileRequest
{
    /// <summary>Profile name (must be unique).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Profile description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the profile is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Profile configuration.</summary>
    public Domain.Entities.PromptProfileConfiguration? Configuration { get; set; }
}

/// <summary>
/// Request to rollback a profile to a specific version.
/// </summary>
public class RollbackRequest
{
    /// <summary>The target version number to rollback to.</summary>
    public int TargetVersion { get; set; }
}
