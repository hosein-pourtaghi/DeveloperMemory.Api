using DeveloperMemory.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;
    private readonly IMemoryRetrievalService _retrievalService;
    private readonly IMemoryIngestionService _ingestionService;
    private readonly IMemoryRanker _memoryRanker;
    private readonly IEmbeddingService _embeddingService;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(
        IMemoryService memoryService,
        IMemoryRetrievalService retrievalService,
        IMemoryIngestionService ingestionService,
        IMemoryRanker memoryRanker,
        IEmbeddingService embeddingService,
        ICurrentUser currentUser,
        ILogger<MemoryController> logger)
    {
        _memoryService = memoryService;
        _retrievalService = retrievalService;
        _ingestionService = ingestionService;
        _memoryRanker = memoryRanker;
        _embeddingService = embeddingService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Create a new memory entry.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MemoryDto>> Create([FromBody] CreateMemoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = new { message = "Title and content are required.", code = "validation_error" } });
        }

        if (request.Content.Length > 10000)
        {
            return BadRequest(new { error = new { message = "Content exceeds maximum length of 10000 characters.", code = "validation_error" } });
        }

        try
        {
            var memory = await _memoryService.CreateAsync(request, _currentUser.UserId, ct);

            // Generate embedding asynchronously (fire-and-forget for now)
            if (_embeddingService.IsSemanticAvailable)
            {
                try
                {
                    await _embeddingService.GenerateAndStoreAsync(
                        memory.Id, $"{request.Title} {request.Content}", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Embedding generation failed for new memory {Id}", memory.Id);
                }
            }

            _logger.LogInformation("Memory created: {Id} - {Title}", memory.Id, memory.Title);
            return CreatedAtAction(nameof(GetById), new { id = memory.Id }, memory);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = new { message = ex.Message, code = ex.ErrorCode } });
        }
    }

    /// <summary>
    /// Ingest a memory through the intelligent ingestion pipeline.
    /// </summary>
    [HttpPost("ingest")]
    public async Task<ActionResult<MemoryIngestionResult>> Ingest([FromBody] MemoryIngestionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = new { message = "Content is required.", code = "validation_error" } });
        }

        var result = await _ingestionService.IngestAsync(request, _currentUser.UserId, ct);

        // Generate embedding for new memories
        if (result.WasPersisted && result.Memory != null && _embeddingService.IsSemanticAvailable)
        {
            try
            {
                await _embeddingService.GenerateAndStoreAsync(
                    result.Memory.Id, $"{result.Memory.Title} {result.Memory.Content}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding generation failed for ingested memory {Id}", result.Memory.Id);
            }
        }

        return result.Outcome switch
        {
            MemoryIngestionOutcome.Created => CreatedAtAction(
                nameof(GetById), new { id = result.Memory!.Id }, result),
            MemoryIngestionOutcome.IgnoredDuplicate => Ok(result),
            MemoryIngestionOutcome.RequiresReview => Ok(result),
            MemoryIngestionOutcome.SupersededExisting => Ok(result),
            MemoryIngestionOutcome.Rejected => BadRequest(new { error = new { message = result.Reason, code = "rejected" } }),
            _ => Ok(result)
        };
    }

    /// <summary>
    /// Get a memory entry by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> GetById(Guid id, CancellationToken ct)
    {
        var memory = await _memoryService.GetByIdAsync(id, _currentUser.UserId, ct);
        if (memory == null) return NotFound();
        return Ok(memory);
    }

    /// <summary>
    /// Search or list memory entries with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<MemoryDto>>> Search(
        [FromQuery] string? query,
        [FromQuery] MemoryScope? scope,
        [FromQuery] Guid? projectId,
        [FromQuery] List<string>? tags,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            var results = await _memoryService.SearchAsync(query, _currentUser.UserId, scope, projectId, tags, ct);
            return Ok(results);
        }

        if (scope.HasValue)
        {
            var entries = await _memoryService.GetByScopeAsync(scope.Value, _currentUser.UserId, projectId, ct);
            return Ok(entries);
        }

        var allEntries = new List<MemoryDto>();
        foreach (MemoryScope s in Enum.GetValues<MemoryScope>())
        {
            allEntries.AddRange(await _memoryService.GetByScopeAsync(s, _currentUser.UserId, projectId, ct));
        }
        return Ok(allEntries);
    }

    /// <summary>
    /// Structured memory query with retrieval mode selection (Lexical/Semantic/Hybrid/Auto).
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<QueryMemoryResult>> Query(
        [FromBody] QueryMemoryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = new { message = "Query is required.", code = "validation_error" } });
        }

        var maxResults = Math.Clamp(request.MaxResults, 1, 100);

        var retrievalRequest = new RetrievalRequest
        {
            OwnerId = _currentUser.UserId,
            UserId = _currentUser.UserId,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            Query = request.Query,
            MaximumResults = maxResults * 3,
            ContextTokenBudget = 100000
        };

        var retrievalResult = await _retrievalService.RetrieveAsync(retrievalRequest, ct);

        var rankedDtos = retrievalResult.Memories
            .Where(m => request.States == null || request.States.Contains(m.State))
            .Where(m => request.MemoryTypes == null || request.MemoryTypes.Contains(m.MemoryType))
            .Where(m => m.RelevanceScore >= request.MinRelevanceScore)
            .Take(maxResults)
            .Select(m => new RankedMemoryDto
            {
                Memory = new MemoryDto
                {
                    Id = m.MemoryId,
                    Title = m.Title,
                    Content = m.Content,
                    Scope = m.Scope,
                    State = m.State,
                    MemoryType = MemoryType.Other,
                    Classification = m.Classification,
                    ProjectId = m.ProjectId,
                    Source = m.Source,
                    Tags = m.Tags,
                    CreatedAt = m.UpdatedAt,
                    UpdatedAt = m.UpdatedAt,
                    Importance = m.Importance
                },
                RelevanceScore = m.RelevanceScore,
                Reason = m.EligibilityReason
            })
            .ToList();

        return Ok(new QueryMemoryResult
        {
            Memories = rankedDtos,
            TotalCandidates = retrievalResult.Metadata.CandidateCount,
            ReturnedCount = rankedDtos.Count
        });
    }

    /// <summary>
    /// Rebuild the embedding for a specific memory.
    /// </summary>
    [HttpPost("{id:guid}/embedding/rebuild")]
    public async Task<ActionResult<object>> RebuildEmbedding(Guid id, CancellationToken ct)
    {
        var memory = await _memoryService.GetByIdAsync(id, _currentUser.UserId, ct);
        if (memory == null) return NotFound();

        if (!_embeddingService.IsSemanticAvailable)
        {
            return BadRequest(new { error = new { message = "Semantic provider is not available.", code = "semantic_unavailable" } });
        }

        var text = $"{memory.Title} {memory.Content}";
        var result = await _embeddingService.RebuildAsync(id, text, ct);

        return Ok(new
        {
            memoryId = id,
            success = result.Success,
            errorMessage = result.ErrorMessage,
            dimensions = result.Embedding?.Dimensions ?? 0,
            durationMs = result.GenerationDurationMs
        });
    }

    /// <summary>
    /// Update a memory entry.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> Update(Guid id, [FromBody] UpdateMemoryRequest request, CancellationToken ct)
    {
        try
        {
            var memory = await _memoryService.UpdateAsync(id, request, _currentUser.UserId, ct);

            // Rebuild embedding if content changed
            if (request.Content != null && _embeddingService.IsSemanticAvailable)
            {
                try
                {
                    await _embeddingService.RebuildAsync(
                        id, $"{memory.Title} {request.Content}", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Embedding rebuild failed for updated memory {Id}", id);
                }
            }

            return Ok(memory);
        }
        catch (MemoryNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = new { message = ex.Message, code = ex.ErrorCode } });
        }
    }

    /// <summary>
    /// Soft-delete a memory entry (sets state to Deleted).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _memoryService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!deleted) return NotFound();

        // Delete embedding
        if (_embeddingService.IsSemanticAvailable)
        {
            try
            {
                await _embeddingService.DeleteAsync(id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding deletion failed for memory {Id}", id);
            }
        }

        return NoContent();
    }

    /// <summary>
    /// Mark a memory as superseded by a new replacement memory.
    /// </summary>
    [HttpPost("{id:guid}/supersede")]
    public async Task<ActionResult<MemoryDto>> Supersede(Guid id, [FromBody] CreateMemoryRequest replacementRequest, CancellationToken ct)
    {
        try
        {
            var replacement = await _memoryService.SupersedeAsync(id, replacementRequest, _currentUser.UserId, ct);

            // Generate embedding for replacement
            if (_embeddingService.IsSemanticAvailable)
            {
                try
                {
                    await _embeddingService.GenerateAndStoreAsync(
                        replacement.Id, $"{replacement.Title} {replacement.Content}", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Embedding generation failed for superseding memory {Id}", replacement.Id);
                }
            }

            _logger.LogInformation("Memory {Id} superseded by {ReplacementId}", id, replacement.Id);
            return Ok(replacement);
        }
        catch (MemoryNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = new { message = ex.Message, code = ex.ErrorCode } });
        }
    }

    /// <summary>
    /// Trigger expiration of all expired memory entries.
    /// </summary>
    [HttpPost("expire")]
    public async Task<ActionResult<object>> Expire(CancellationToken ct)
    {
        var count = await _memoryService.ExpireAsync(ct);
        return Ok(new { expired = count, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Get memory statistics (counts by scope and state).
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<MemoryStatsDto>> GetStats(CancellationToken ct)
    {
        var stats = await _memoryService.GetStatsAsync(_currentUser.UserId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Analyze input and return memory candidates without persisting them.
    /// Useful for testing, debugging, and future review UI.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<object>> Analyze(
        [FromBody] MemoryExtractionRequest request,
        [FromQuery] string? mode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = new { message = "Content is required.", code = "validation_error" } });
        }

        var extractionMode = ExtractionMode.Auto;
        if (!string.IsNullOrEmpty(mode) && Enum.TryParse<ExtractionMode>(mode, true, out var parsed))
        {
            extractionMode = parsed;
        }

        var orchestrator = HttpContext.RequestServices.GetRequiredService<IExtractionOrchestrator>();
        var result = await orchestrator.ExtractAsync(request, extractionMode, ct);

        return Ok(new
        {
            candidates = result.Candidates,
            strategyUsed = result.StrategyUsed,
            llmUsed = result.LlmUsed,
            deterministicCount = result.DeterministicCount,
            llmCount = result.LlmCount,
            finalCount = result.FinalCount,
            warnings = result.Warnings
        });
    }

    /// <summary>
    /// Privacy-aware memory retrieval with ranking and context budgeting.
    /// </summary>
    [HttpPost("retrieve")]
    public async Task<ActionResult<RetrievedMemoriesResult>> Retrieve(
        [FromBody] RetrieveRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = new { message = "Query is required.", code = "validation_error" } });
        }

        const int maxAllowedResults = 100;
        const int maxAllowedBudget = 100000;
        var effectiveMaxResults = Math.Clamp(request.MaximumResults, 1, maxAllowedResults);
        var effectiveBudget = Math.Clamp(request.ContextTokenBudget, 0, maxAllowedBudget);

        var retrievalRequest = new RetrievalRequest
        {
            OwnerId = _currentUser.UserId,
            UserId = request.UserId,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            Query = request.Query,
            RequestedScopes = request.RequestedScopes,
            MaximumResults = effectiveMaxResults,
            ContextTokenBudget = effectiveBudget,
            RequiredCategories = request.RequiredCategories,
            ExcludedCategories = request.ExcludedCategories
        };

        var result = await _retrievalService.RetrieveAsync(retrievalRequest, ct);
        return Ok(result);
    }
}
