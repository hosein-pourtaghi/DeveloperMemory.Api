using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Application.Services;

/// <summary>
/// Dedicated V2 context assembly mechanism.
///
/// Pipeline:
///   Runtime Request (UnifiedContextRequest)
///     → Capture Runtime Context (request, conversation, identity, explicit instructions)
///     → Memory Retrieval (existing pipeline: scope → privacy → lifecycle → ranking → budget)
///     → Duplicate Suppression (deterministic, provenance-preserving)
///     → Persistent Project Knowledge (IProjectContextProvider, active project only)
///     → UnifiedAgentContext (Runtime | Persistent | Assembly report)
///
/// Deterministic — never calls an LLM. Provider/model agnostic. Does not
/// persist anything: runtime context is never persisted, and persistent
/// intelligence is only read through existing services.
/// </summary>
public class ContextAssemblyService : IContextAssemblyService
{
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly IProjectContextProvider _projectContextProvider;
    private readonly IAgentContextProvider _agentContextProvider;
    private readonly ILogger<ContextAssemblyService> _logger;

    public ContextAssemblyService(
        IMemoryRetrievalService retrievalService,
        IProjectContextProvider projectContextProvider,
        IAgentContextProvider agentContextProvider,
        ILogger<ContextAssemblyService> logger)
    {
        _retrievalService = retrievalService;
        _projectContextProvider = projectContextProvider;
        _agentContextProvider = agentContextProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UnifiedAgentContext> AssembleAsync(
        UnifiedContextRequest request,
        string ownerId,
        CancellationToken ct = default)
    {
        var assembly = new ContextAssemblyReport
        {
            MaximumResults = request.MaxResults,
            TokenBudget = request.ContextTokenBudget
        };
        var warnings = new List<string>();

        // ── Stage 1: Capture runtime context ──
        // The request/task, conversation, active identities, and explicit
        // instructions are runtime-only. They are never persisted and never
        // merged into the persistent partition.
        var query = !string.IsNullOrWhiteSpace(request.Query)
            ? request.Query
            : request.Task.Trim();

        var (agentId, agentType) = ResolveAgentIdentity(request, warnings);

        var runtime = new RuntimeContext
        {
            Request = request.Task,
            Query = query,
            OwnerId = ownerId,
            UserId = ownerId,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            AgentId = agentId,
            AgentType = agentType,
            ConversationHistory = request.ConversationHistory ?? [],
            ExplicitInstructions = request.Constraints ?? [],
            Tags = request.Tags ?? []
        };

        // Empty request — no assembly is possible. Return an empty context
        // (runtime captured, no persistent intelligence) rather than failing.
        if (string.IsNullOrWhiteSpace(request.Task))
        {
            warnings.Add("Task is empty; assembled without persistent intelligence");
            assembly.Warnings = warnings;
            return new UnifiedAgentContext
            {
                Runtime = runtime,
                Persistent = new PersistentContext(),
                Assembly = assembly
            };
        }

        // ── Stage 2: Retrieve relevant persistent intelligence ──
        // Delegates to the existing retrieval pipeline so memory scopes,
        // lifecycle states, privacy/isolation, ranking, and budgeting behave
        // exactly as in V1. The assembler never bypasses these boundaries.
        RetrievedMemoriesResult retrievalResult;
        try
        {
            var retrievalRequest = new RetrievalRequest
            {
                OwnerId = ownerId,
                UserId = ownerId,
                ProjectId = runtime.ProjectId,
                WorkspaceId = runtime.WorkspaceId,
                Query = query,
                MaximumResults = request.MaxResults,
                ContextTokenBudget = request.ContextTokenBudget
            };

            retrievalResult = await _retrievalService.RetrieveAsync(retrievalRequest, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "V2 context assembly: memory retrieval failed; continuing without memory");
            warnings.Add("Memory retrieval unavailable; assembled without memories");
            retrievalResult = new RetrievedMemoriesResult
            {
                Memories = [],
                Metadata = new RetrievalMetadata()
            };
        }

        assembly.EligibleScopes = retrievalResult.Metadata.EligibleScopes;
        assembly.CandidatesConsidered = retrievalResult.Metadata.CandidateCount;
        assembly.EligibleCount = retrievalResult.Metadata.EligibleCount;
        assembly.SelectedCount = retrievalResult.Metadata.SelectedCount;
        assembly.EstimatedTokensUsed = retrievalResult.Metadata.EstimatedTokensUsed;

        // ── Stage 3: Deterministic duplicate suppression ──
        // The retrieval pipeline ranks and budgets; this stage additionally
        // suppresses near-duplicate memories so the unified context does not
        // carry repeated information. Provenance is preserved by reporting the
        // suppressed memory ids and keeping the higher-value variant.
        var (memories, suppressedIds) = SuppressDuplicates(retrievalResult.Memories);
        assembly.DuplicatesSuppressed = suppressedIds.Count;
        assembly.SuppressedMemoryIds = suppressedIds;

        // ── Stage 4: Persistent project knowledge ──
        // Included only for the explicitly active project — never for another
        // project. Project knowledge is read from the existing provider and is
        // never confused with runtime context.
        ProjectContext? projectKnowledge = null;
        if (runtime.ProjectId.HasValue)
        {
            try
            {
                projectKnowledge = await _projectContextProvider.GetContextAsync(
                    runtime.ProjectId.Value, runtime.WorkspaceId, ct);
                assembly.ProjectKnowledgeIncluded = projectKnowledge != null;
                if (projectKnowledge == null)
                {
                    warnings.Add($"No project knowledge available for project {runtime.ProjectId}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "V2 context assembly: project knowledge unavailable for {ProjectId}", runtime.ProjectId);
                warnings.Add("Project knowledge unavailable");
            }
        }

        assembly.Warnings = warnings;

        var persistent = new PersistentContext
        {
            Memories = memories,
            ProjectKnowledge = projectKnowledge
        };

        _logger.LogInformation(
            "V2 context assembled: project={ProjectId}, workspace={WorkspaceId}, " +
            "memories={Selected}, duplicatesSuppressed={Duplicates}, " +
            "projectKnowledge={HasKnowledge}, tokens={Tokens}, empty={IsEmpty}",
            runtime.ProjectId?.ToString() ?? "(none)",
            runtime.WorkspaceId ?? "(none)",
            persistent.Memories.Count, assembly.DuplicatesSuppressed,
            assembly.ProjectKnowledgeIncluded, assembly.EstimatedTokensUsed,
            persistent.IsEmpty);

        return new UnifiedAgentContext
        {
            Runtime = runtime,
            Persistent = persistent,
            Assembly = assembly
        };
    }

    /// <summary>
    /// Resolves optional agent identity. When an AgentId is provided without an
    /// explicit AgentType, the existing deterministic provider classifies it.
    /// Without an AgentId the context remains agent-agnostic.
    /// </summary>
    private (string? AgentId, AgentType? AgentType) ResolveAgentIdentity(
        UnifiedContextRequest request, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return (null, null);
        }

        if (request.AgentType.HasValue)
        {
            return (request.AgentId, request.AgentType);
        }

        try
        {
            var resolved = _agentContextProvider.Resolve(new AgentContextRequest
            {
                AgentId = request.AgentId,
                Task = request.Task
            });
            return (request.AgentId, resolved.AgentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent type inference failed for agent {AgentId}; keeping untyped", request.AgentId);
            warnings.Add("Agent type inference unavailable");
            return (request.AgentId, null);
        }
    }

    /// <summary>
    /// Suppresses near-duplicate memories deterministically. Memories with
    /// identical content (ordinal, case-insensitive) are duplicates; the
    /// higher-value variant (importance, then recency) is kept. Suppressed
    /// ids are returned so provenance is never silently lost.
    /// </summary>
    private static (List<RetrievedMemory> Kept, List<Guid> Suppressed) SuppressDuplicates(
        List<RetrievedMemory> memories)
    {
        if (memories.Count == 0)
        {
            return (memories, []);
        }

        var kept = new List<RetrievedMemory>();
        var suppressed = new List<Guid>();

        foreach (var memory in memories)
        {
            var duplicate = kept.FirstOrDefault(k =>
                !k.MemoryId.Equals(memory.MemoryId) &&
                string.Equals(k.Content.Trim(), memory.Content.Trim(), StringComparison.OrdinalIgnoreCase));

            if (duplicate == null)
            {
                kept.Add(memory);
                continue;
            }

            // Keep the higher-value variant: importance first, then recency.
            if (IsHigherValue(memory, duplicate))
            {
                kept.Remove(duplicate);
                kept.Add(memory);
                suppressed.Add(duplicate.MemoryId);
            }
            else
            {
                suppressed.Add(memory.MemoryId);
            }
        }

        return (kept, suppressed);
    }

    private static bool IsHigherValue(RetrievedMemory candidate, RetrievedMemory current)
    {
        if (candidate.Importance != current.Importance)
        {
            return candidate.Importance > current.Importance;
        }

        return candidate.UpdatedAt > current.UpdatedAt;
    }
}
