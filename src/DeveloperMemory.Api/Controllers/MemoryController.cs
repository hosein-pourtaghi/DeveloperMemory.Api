using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(IMemoryService memoryService, ILogger<MemoryController> logger)
    {
        _memoryService = memoryService;
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

        try
        {
            var memory = await _memoryService.CreateAsync(request, ct);
            _logger.LogInformation("Memory created: {Id} - {Title}", memory.Id, memory.Title);
            return CreatedAtAction(nameof(GetById), new { id = memory.Id }, memory);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = new { message = ex.Message, code = ex.ErrorCode } });
        }
    }

    /// <summary>
    /// Get a memory entry by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> GetById(Guid id, CancellationToken ct)
    {
        var memory = await _memoryService.GetByIdAsync(id, ct);
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
            var results = await _memoryService.SearchAsync(query, scope, projectId, tags, ct);
            return Ok(results);
        }

        if (scope.HasValue)
        {
            var entries = await _memoryService.GetByScopeAsync(scope.Value, projectId, ct);
            return Ok(entries);
        }

        // Default: return all scopes
        var allEntries = new List<MemoryDto>();
        foreach (MemoryScope s in Enum.GetValues<MemoryScope>())
        {
            allEntries.AddRange(await _memoryService.GetByScopeAsync(s, projectId, ct));
        }
        return Ok(allEntries);
    }

    /// <summary>
    /// Update a memory entry.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> Update(Guid id, [FromBody] UpdateMemoryRequest request, CancellationToken ct)
    {
        try
        {
            var memory = await _memoryService.UpdateAsync(id, request, ct);
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
        var deleted = await _memoryService.DeleteAsync(id, ct);
        if (!deleted) return NotFound();
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
            var replacement = await _memoryService.SupersedeAsync(id, replacementRequest, ct);
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
        var stats = await _memoryService.GetStatsAsync(ct);
        return Ok(stats);
    }
}
