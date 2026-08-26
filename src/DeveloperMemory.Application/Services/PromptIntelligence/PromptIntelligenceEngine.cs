using System.Diagnostics;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Central Prompt Intelligence Engine.
/// 
/// Transforms raw user requests into structured, provider-neutral PromptPackages
/// by orchestrating analysis, constraint resolution, memory context assembly,
/// prompt composition, and optimization.
/// 
/// Degradation model:
///   - Each pipeline stage has its own recovery behavior
///   - Recoverable failures result in Degraded status with valid content
///   - Non-recoverable failures result in Failed status
///   - The original request is ALWAYS preserved
///   - Explicit constraints are preserved when safely available
///   - The engine NEVER bypasses privacy boundaries
/// 
/// The engine does NOT execute requests or call LLMs.
/// It prepares intelligence/context for downstream consumption.
/// </summary>
public class PromptIntelligenceEngine : IPromptIntelligenceEngine
{
    private readonly IPromptAnalyzer _analyzer;
    private readonly IConstraintResolver _constraintResolver;
    private readonly IMemoryContextAssembler _contextAssembler;
    private readonly IPromptComposer _composer;
    private readonly IPromptOptimizer _optimizer;
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly ILogger<PromptIntelligenceEngine> _logger;

    public PromptIntelligenceEngine(
        IPromptAnalyzer analyzer,
        IConstraintResolver constraintResolver,
        IMemoryContextAssembler contextAssembler,
        IPromptComposer composer,
        IPromptOptimizer optimizer,
        IMemoryRetrievalService retrievalService,
        ILogger<PromptIntelligenceEngine> logger)
    {
        _analyzer = analyzer;
        _constraintResolver = constraintResolver;
        _contextAssembler = contextAssembler;
        _composer = composer;
        _optimizer = optimizer;
        _retrievalService = retrievalService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PromptPackage> ProcessAsync(
        string userRequest,
        string userId,
        Guid? projectId = null,
        string? workspaceId = null,
        int contextTokenBudget = 4000,
        string? profileContext = null,
        string? knowledgeContext = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var totalStopwatch = Stopwatch.StartNew();
        var metadata = new PromptIntelligenceMetadata();
        var warnings = new List<string>();
        var degradationReasons = new List<string>();
        var status = PromptIntelligenceStatus.Full;
        PromptIntelligenceStage? failedStage = null;

        // ── Validate inputs ──
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return BuildFailedPackage("Request is empty", userRequest, userId, projectId, workspaceId, metadata);
        }

        // ── Stage 1: Prompt Analysis ──
        PromptAnalysis analysis;
        var analysisStopwatch = Stopwatch.StartNew();
        try
        {
            analysis = _analyzer.Analyze(userRequest);
            analysisStopwatch.Stop();
            metadata.AnalysisDurationMs = analysisStopwatch.Elapsed.TotalMilliseconds;

            _logger.LogDebug(
                "Prompt analysis: intent={Intent}, task={TaskType}, keywords={Keywords}",
                analysis.Intent, analysis.TaskType, analysis.Keywords.Count);
        }
        catch (OperationCanceledException)
        {
            throw; // Always propagate cancellation
        }
        catch (Exception ex)
        {
            analysisStopwatch.Stop();
            metadata.AnalysisDurationMs = analysisStopwatch.Elapsed.TotalMilliseconds;
            _logger.LogWarning(ex, "Analysis stage failed; using conservative fallback");
            analysis = CreateFallbackAnalysis(userRequest);
            status = PromptIntelligenceStatus.Degraded;
            failedStage = PromptIntelligenceStage.Analysis;
            warnings.Add("Analysis failed; using conservative fallback analysis");
            degradationReasons.Add("analysis_failed");
        }

        // ── Stage 2: Memory Retrieval (via centralized pipeline) ──
        PromptContext context;
        var retrievalStopwatch = Stopwatch.StartNew();
        try
        {
            var retrievalRequest = new RetrievalRequest
            {
                UserId = userId,
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                Query = userRequest,
                MaximumResults = 20,
                ContextTokenBudget = contextTokenBudget
            };

            context = await _retrievalService.BuildPromptContextAsync(retrievalRequest, ct);
            metadata.CandidateMemoryCount = context.RetrievedMemories.Count;
            retrievalStopwatch.Stop();
            metadata.ContextAssemblyDurationMs = retrievalStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            throw; // Always propagate cancellation
        }
        catch (Exception ex)
        {
            retrievalStopwatch.Stop();
            _logger.LogWarning(ex, "Retrieval stage failed; continuing without memory context");
            context = new PromptContext
            {
                OriginalQuery = userRequest,
                UserId = userId,
                ProjectId = projectId,
                WorkspaceId = workspaceId
            };
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.Retrieval;
            }
            warnings.Add("Memory retrieval failed; continuing without memory context");
            degradationReasons.Add("retrieval_unavailable");
        }

        // ── Stage 3: Constraint Resolution ──
        List<PromptConstraint> constraints;
        var constraintStopwatch = Stopwatch.StartNew();
        try
        {
            constraints = _constraintResolver.Resolve(analysis, context);
            metadata.ConstraintsResolved = constraints.Count;
            constraintStopwatch.Stop();
            metadata.ConstraintDurationMs = constraintStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            constraintStopwatch.Stop();
            _logger.LogWarning(ex, "Constraint resolution failed; preserving explicit constraints only");
            // Preserve explicit constraints from the analysis if available
            constraints = analysis.ExplicitConstraints.Select(c => new PromptConstraint
            {
                Type = ConstraintType.UserPreference,
                Value = c,
                Source = ConstraintSource.ExplicitCurrentRequest,
                Precedence = (int)ConstraintSource.ExplicitCurrentRequest
            }).ToList();
            metadata.ConstraintsResolved = constraints.Count;
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.ConstraintResolution;
            }
            warnings.Add("Constraint resolution failed; only explicit request constraints preserved");
            degradationReasons.Add("constraint_resolution_failed");
        }

        // ── Stage 4: Memory Context Assembly ──
        ContextAssemblyResult assemblyResult;
        var assemblyStopwatch = Stopwatch.StartNew();
        try
        {
            assemblyResult = _contextAssembler.Assemble(context, analysis, constraints);
            metadata.RefinedMemoryCount = assemblyResult.Sections.Sum(s => s.Items.Count);
            metadata.DuplicatesRemoved = assemblyResult.DuplicatesRemoved;
            metadata.ConflictsDetected = assemblyResult.Contradictions.Count;
            metadata.ContextSectionCount = assemblyResult.Sections.Count;
            assemblyStopwatch.Stop();
            metadata.ContextAssemblyDurationMs = assemblyStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            assemblyStopwatch.Stop();
            _logger.LogWarning(ex, "Context assembly failed; using empty context");
            assemblyResult = new ContextAssemblyResult();
            metadata.ContextSectionCount = 0;
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.ContextAssembly;
            }
            warnings.Add("Context assembly failed; using empty context sections");
            degradationReasons.Add("context_assembly_failed");
        }

        // ── Stage 5: Prompt Composition ──
        PromptCompositionResult composition;
        var compositionStopwatch = Stopwatch.StartNew();
        try
        {
            composition = _composer.Compose(
                analysis, constraints, assemblyResult.Sections, userRequest,
                profileContext, knowledgeContext);
            compositionStopwatch.Stop();
            metadata.CompositionDurationMs = compositionStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            compositionStopwatch.Stop();
            _logger.LogWarning(ex, "Composition failed; this is a non-recoverable failure");
            totalStopwatch.Stop();
            metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
            return BuildFailedPackage(
                $"Composition failed: {ex.Message}",
                userRequest, userId, projectId, workspaceId, metadata);
        }

        // ── Stage 6: Prompt Optimization (recoverable) ──
        string optimizedPrompt;
        var optimizationStopwatch = Stopwatch.StartNew();
        try
        {
            optimizedPrompt = _optimizer.Optimize(composition.ComposedPrompt);
            optimizationStopwatch.Stop();
            metadata.OptimizationDurationMs = optimizationStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            optimizationStopwatch.Stop();
            _logger.LogWarning(ex, "Optimization failed; using composed prompt directly");
            optimizedPrompt = composition.ComposedPrompt;
            metadata.OptimizationDurationMs = optimizationStopwatch.Elapsed.TotalMilliseconds;
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.Optimization;
            }
            warnings.Add("Optimization failed; using composed prompt directly");
            degradationReasons.Add("optimization_failed");
        }

        // ── Build PromptPackage ──
        totalStopwatch.Stop();
        metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
        metadata.FinalPromptLength = optimizedPrompt.Length;
        metadata.Status = status;
        metadata.FailedStage = failedStage;
        metadata.Warnings = warnings;
        metadata.DegradationReasons = degradationReasons;

        _logger.LogInformation(
            "Intelligence processing: status={Status}, duration={Duration}ms, stage={Stage}",
            status, metadata.TotalDurationMs, failedStage?.ToString() ?? "none");

        return new PromptPackage
        {
            OriginalRequest = userRequest,
            Analysis = analysis,
            Constraints = constraints,
            ContextSections = assemblyResult.Sections,
            Instructions = composition.Instructions,
            OptimizedPrompt = optimizedPrompt,
            RetrievalMetadata = context?.Metadata ?? new RetrievalMetadata(),
            Metadata = metadata,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            UserId = userId,
            Status = status,
            FailedStage = failedStage,
            Warnings = warnings,
            DegradationReasons = degradationReasons,
            OriginalRequestPreserved = true,
            ConstraintsPreserved = constraints.Count > 0
        };
    }

    /// <inheritdoc/>
    public PromptPackage ProcessWithContext(string userRequest, PromptContext context)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var metadata = new PromptIntelligenceMetadata();
        var warnings = new List<string>();
        var degradationReasons = new List<string>();
        var status = PromptIntelligenceStatus.Full;
        PromptIntelligenceStage? failedStage = null;

        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return BuildFailedPackage("Request is empty", userRequest, context.UserId, context.ProjectId, context.WorkspaceId, metadata);
        }

        // ── Stage 1: Prompt Analysis ──
        PromptAnalysis analysis;
        try
        {
            var analysisStopwatch = Stopwatch.StartNew();
            analysis = _analyzer.Analyze(userRequest, context);
            analysisStopwatch.Stop();
            metadata.AnalysisDurationMs = analysisStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Analysis stage failed; using conservative fallback");
            analysis = CreateFallbackAnalysis(userRequest);
            status = PromptIntelligenceStatus.Degraded;
            failedStage = PromptIntelligenceStage.Analysis;
            warnings.Add("Analysis failed; using conservative fallback analysis");
            degradationReasons.Add("analysis_failed");
        }

        metadata.CandidateMemoryCount = context.RetrievedMemories.Count;

        // ── Stage 2: Constraint Resolution ──
        List<PromptConstraint> constraints;
        try
        {
            var constraintStopwatch = Stopwatch.StartNew();
            constraints = _constraintResolver.Resolve(analysis, context);
            constraintStopwatch.Stop();
            metadata.ConstraintsResolved = constraints.Count;
            metadata.ConstraintDurationMs = constraintStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Constraint resolution failed; preserving explicit constraints only");
            constraints = analysis.ExplicitConstraints.Select(c => new PromptConstraint
            {
                Type = ConstraintType.UserPreference,
                Value = c,
                Source = ConstraintSource.ExplicitCurrentRequest,
                Precedence = (int)ConstraintSource.ExplicitCurrentRequest
            }).ToList();
            metadata.ConstraintsResolved = constraints.Count;
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.ConstraintResolution;
            }
            warnings.Add("Constraint resolution failed; only explicit request constraints preserved");
            degradationReasons.Add("constraint_resolution_failed");
        }

        // ── Stage 3: Memory Context Assembly ──
        ContextAssemblyResult assemblyResult;
        try
        {
            var assemblyStopwatch = Stopwatch.StartNew();
            assemblyResult = _contextAssembler.Assemble(context, analysis, constraints);
            assemblyStopwatch.Stop();
            metadata.RefinedMemoryCount = assemblyResult.Sections.Sum(s => s.Items.Count);
            metadata.DuplicatesRemoved = assemblyResult.DuplicatesRemoved;
            metadata.ConflictsDetected = assemblyResult.Contradictions.Count;
            metadata.ContextSectionCount = assemblyResult.Sections.Count;
            metadata.ContextAssemblyDurationMs = assemblyStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Context assembly failed; using empty context");
            assemblyResult = new ContextAssemblyResult();
            metadata.ContextSectionCount = 0;
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.ContextAssembly;
            }
            warnings.Add("Context assembly failed; using empty context sections");
            degradationReasons.Add("context_assembly_failed");
        }

        // ── Stage 4: Prompt Composition ──
        PromptCompositionResult composition;
        try
        {
            var compositionStopwatch = Stopwatch.StartNew();
            composition = _composer.Compose(analysis, constraints, assemblyResult.Sections, userRequest);
            compositionStopwatch.Stop();
            metadata.CompositionDurationMs = compositionStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Composition failed; this is a non-recoverable failure");
            totalStopwatch.Stop();
            metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
            return BuildFailedPackage(
                $"Composition failed: {ex.Message}",
                userRequest, context.UserId, context.ProjectId, context.WorkspaceId, metadata);
        }

        // ── Stage 5: Prompt Optimization (recoverable) ──
        string optimizedPrompt;
        try
        {
            var optimizationStopwatch = Stopwatch.StartNew();
            optimizedPrompt = _optimizer.Optimize(composition.ComposedPrompt);
            optimizationStopwatch.Stop();
            metadata.OptimizationDurationMs = optimizationStopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Optimization failed; using composed prompt directly");
            optimizedPrompt = composition.ComposedPrompt;
            if (status == PromptIntelligenceStatus.Full)
            {
                status = PromptIntelligenceStatus.Degraded;
                failedStage = PromptIntelligenceStage.Optimization;
            }
            warnings.Add("Optimization failed; using composed prompt directly");
            degradationReasons.Add("optimization_failed");
        }

        // ── Build PromptPackage ──
        totalStopwatch.Stop();
        metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
        metadata.FinalPromptLength = optimizedPrompt.Length;
        metadata.Status = status;
        metadata.FailedStage = failedStage;
        metadata.Warnings = warnings;
        metadata.DegradationReasons = degradationReasons;

        return new PromptPackage
        {
            OriginalRequest = userRequest,
            Analysis = analysis,
            Constraints = constraints,
            ContextSections = assemblyResult.Sections,
            Instructions = composition.Instructions,
            OptimizedPrompt = optimizedPrompt,
            RetrievalMetadata = context.Metadata,
            Metadata = metadata,
            ProjectId = context.ProjectId,
            WorkspaceId = context.WorkspaceId,
            UserId = context.UserId,
            Status = status,
            FailedStage = failedStage,
            Warnings = warnings,
            DegradationReasons = degradationReasons,
            OriginalRequestPreserved = true,
            ConstraintsPreserved = constraints.Count > 0
        };
    }

    private static PromptAnalysis CreateFallbackAnalysis(string userRequest)
    {
        return new PromptAnalysis
        {
            OriginalRequest = userRequest,
            Intent = IntentType.General,
            TaskType = TaskType.General,
            UserGoal = $"General task: {userRequest.Length > 100 ? userRequest[..100] + "..." : userRequest}"
        };
    }

    private static PromptPackage BuildFailedPackage(
        string reason,
        string userRequest,
        string userId,
        Guid? projectId,
        string? workspaceId,
        PromptIntelligenceMetadata metadata)
    {
        metadata.Status = PromptIntelligenceStatus.Failed;
        metadata.FailedStage = PromptIntelligenceStage.Composition;
        metadata.Warnings.Add(reason);

        // Even in failed state, preserve the original request exactly
        var fallback = CreateFallbackAnalysis(userRequest);

        return new PromptPackage
        {
            OriginalRequest = userRequest,
            Analysis = fallback,
            Constraints = [],
            ContextSections = [],
            Instructions = string.Empty,
            OptimizedPrompt = userRequest, // Pass through the raw request as last resort
            RetrievalMetadata = new RetrievalMetadata(),
            Metadata = metadata,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            UserId = userId,
            Status = PromptIntelligenceStatus.Failed,
            FailedStage = PromptIntelligenceStage.Composition,
            Warnings = [reason],
            DegradationReasons = ["non_recoverable_failure"],
            OriginalRequestPreserved = true,
            ConstraintsPreserved = false
        };
    }
}
