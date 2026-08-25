using System.Diagnostics;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services.PromptIntelligence;

/// <summary>
/// Central Prompt Intelligence Engine.
/// 
/// Transforms raw user requests into structured, provider-neutral PromptPackages
/// by orchestrating analysis, constraint resolution, memory context assembly,
/// prompt composition, and optimization.
/// 
/// The engine does NOT execute requests or call LLMs.
/// It prepares intelligence/context for downstream consumption.
/// 
/// Pipeline: Request → Analysis → Constraints → Context Assembly
///          → Composition → Optimization → PromptPackage
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
        CancellationToken ct = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var metadata = new PromptIntelligenceMetadata();

        try
        {
            // ── Stage 1: Prompt Analysis ──
            var analysisStopwatch = Stopwatch.StartNew();
            var analysis = _analyzer.Analyze(userRequest);
            analysisStopwatch.Stop();
            metadata.AnalysisDurationMs = analysisStopwatch.Elapsed.TotalMilliseconds;

            _logger.LogDebug(
                "Prompt analysis: intent={Intent}, task={TaskType}, keywords={Keywords}",
                analysis.Intent, analysis.TaskType, analysis.Keywords.Count);

            // ── Stage 2: Memory Retrieval (via centralized pipeline) ──
            var retrievalStopwatch = Stopwatch.StartNew();
            PromptContext? context = null;

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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory retrieval failed during intelligence processing; continuing without memories");
                context = new PromptContext
                {
                    OriginalQuery = userRequest,
                    UserId = userId,
                    ProjectId = projectId,
                    WorkspaceId = workspaceId
                };
            }

            retrievalStopwatch.Stop();

            // ── Stage 3: Constraint Resolution ──
            var constraintStopwatch = Stopwatch.StartNew();
            var constraints = _constraintResolver.Resolve(analysis, context);
            metadata.ConstraintsResolved = constraints.Count;
            constraintStopwatch.Stop();
            metadata.ConstraintDurationMs = constraintStopwatch.Elapsed.TotalMilliseconds;

            // ── Stage 4: Memory Context Assembly ──
            var assemblyStopwatch = Stopwatch.StartNew();
            var assemblyResult = _contextAssembler.Assemble(context!, analysis, constraints);
            metadata.RefinedMemoryCount = assemblyResult.Sections
                .Sum(s => s.Items.Count);
            metadata.DuplicatesRemoved = assemblyResult.DuplicatesRemoved;
            metadata.ConflictsDetected = assemblyResult.Contradictions.Count;
            metadata.ContextSectionCount = assemblyResult.Sections.Count;
            assemblyStopwatch.Stop();
            metadata.ContextAssemblyDurationMs = assemblyStopwatch.Elapsed.TotalMilliseconds;

            // ── Stage 5: Prompt Composition ──
            var compositionStopwatch = Stopwatch.StartNew();
            var composition = _composer.Compose(
                analysis, constraints, assemblyResult.Sections, userRequest);
            compositionStopwatch.Stop();
            metadata.CompositionDurationMs = compositionStopwatch.Elapsed.TotalMilliseconds;

            // ── Stage 6: Prompt Optimization ──
            var optimizationStopwatch = Stopwatch.StartNew();
            var optimizedPrompt = _optimizer.Optimize(composition.ComposedPrompt);
            optimizationStopwatch.Stop();
            metadata.OptimizationDurationMs = optimizationStopwatch.Elapsed.TotalMilliseconds;

            // ── Build PromptPackage ──
            totalStopwatch.Stop();
            metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
            metadata.FinalPromptLength = optimizedPrompt.Length;

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
                UserId = userId
            };
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
            _logger.LogError(ex, "Prompt intelligence processing failed after {Duration}ms", metadata.TotalDurationMs);
            throw;
        }
    }

    /// <inheritdoc/>
    public PromptPackage ProcessWithContext(string userRequest, PromptContext context)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var metadata = new PromptIntelligenceMetadata();

        // ── Stage 1: Prompt Analysis ──
        var analysisStopwatch = Stopwatch.StartNew();
        var analysis = _analyzer.Analyze(userRequest, context);
        analysisStopwatch.Stop();
        metadata.AnalysisDurationMs = analysisStopwatch.Elapsed.TotalMilliseconds;

        metadata.CandidateMemoryCount = context.RetrievedMemories.Count;

        // ── Stage 2: Constraint Resolution ──
        var constraintStopwatch = Stopwatch.StartNew();
        var constraints = _constraintResolver.Resolve(analysis, context);
        metadata.ConstraintsResolved = constraints.Count;
        constraintStopwatch.Stop();
        metadata.ConstraintDurationMs = constraintStopwatch.Elapsed.TotalMilliseconds;

        // ── Stage 3: Memory Context Assembly ──
        var assemblyStopwatch = Stopwatch.StartNew();
        var assemblyResult = _contextAssembler.Assemble(context, analysis, constraints);
        metadata.RefinedMemoryCount = assemblyResult.Sections.Sum(s => s.Items.Count);
        metadata.DuplicatesRemoved = assemblyResult.DuplicatesRemoved;
        metadata.ConflictsDetected = assemblyResult.Contradictions.Count;
        metadata.ContextSectionCount = assemblyResult.Sections.Count;
        assemblyStopwatch.Stop();
        metadata.ContextAssemblyDurationMs = assemblyStopwatch.Elapsed.TotalMilliseconds;

        // ── Stage 4: Prompt Composition ──
        var compositionStopwatch = Stopwatch.StartNew();
        var composition = _composer.Compose(analysis, constraints, assemblyResult.Sections, userRequest);
        compositionStopwatch.Stop();
        metadata.CompositionDurationMs = compositionStopwatch.Elapsed.TotalMilliseconds;

        // ── Stage 5: Prompt Optimization ──
        var optimizationStopwatch = Stopwatch.StartNew();
        var optimizedPrompt = _optimizer.Optimize(composition.ComposedPrompt);
        optimizationStopwatch.Stop();
        metadata.OptimizationDurationMs = optimizationStopwatch.Elapsed.TotalMilliseconds;

        // ── Build PromptPackage ──
        totalStopwatch.Stop();
        metadata.TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds;
        metadata.FinalPromptLength = optimizedPrompt.Length;

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
            UserId = context.UserId
        };
    }
}
