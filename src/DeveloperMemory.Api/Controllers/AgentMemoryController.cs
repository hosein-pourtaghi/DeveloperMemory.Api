using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using DeveloperMemory.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// Agent-accessible memory API. Provides application-level memory operations
/// that future agents (coding agents, MCP adapters, etc.) can consume.
///
/// All operations go through existing application services and respect
/// authorization, ownership, scope, and data classification rules.
///
/// Agents must NOT bypass domain rules or repositories.
/// </summary>
[ApiController]
[Route("api/agent/memory")]
[Authorize]
public class AgentMemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;
    private readonly IProjectService _projectService;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AgentMemoryController> _logger;

    public AgentMemoryController(
        IMemoryService memoryService,
        IProjectService projectService,
        ICurrentUser currentUser,
        ILogger<AgentMemoryController> logger)
    {
        _memoryService = memoryService;
        _projectService = projectService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Search memories by query. Returns authorized memories only.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<MemoryDto>>> Search(
        [FromQuery] string query,
        [FromQuery] MemoryScope? scope = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] List<string>? tags = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = new { message = "Query is required." } });

        _logger.LogInformation("Agent memory search: query={Query}, scope={Scope}, projectId={ProjectId}",
            query, scope?.ToString() ?? "(any)", projectId?.ToString() ?? "(any)");

        var results = await _memoryService.SearchAsync(
            query, _currentUser.UserId, scope, projectId, tags, ct);

        return Ok(results);
    }

    /// <summary>
    /// Get a specific memory by ID. Returns null if not authorized.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> GetById(Guid id, CancellationToken ct)
    {
        var memory = await _memoryService.GetByIdAsync(id, _currentUser.UserId, ct);
        if (memory == null) return NotFound();
        return Ok(memory);
    }

    /// <summary>
    /// Get memories by scope.
    /// </summary>
    [HttpGet("by-scope/{scope}")]
    public async Task<ActionResult<List<MemoryDto>>> GetByScope(
        MemoryScope scope,
        [FromQuery] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var results = await _memoryService.GetByScopeAsync(scope, _currentUser.UserId, projectId, ct);
        return Ok(results);
    }

    /// <summary>
    /// Create a new memory. Agent must supply content and classification.
    /// Ownership and authorization are derived from the authenticated principal.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MemoryDto>> Create(
        [FromBody] CreateMemoryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = new { message = "Content is required." } });

        _logger.LogInformation("Agent memory create: type={Type}, scope={Scope}",
            request.MemoryType, request.Scope);

        try
        {
            var created = await _memoryService.CreateAsync(request, _currentUser.UserId, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = new { message = ex.Message, code = ex.ErrorCode } });
        }
    }

    /// <summary>
    /// Update an existing memory. Only the owner can update.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> Update(
        Guid id,
        [FromBody] UpdateMemoryRequest request,
        CancellationToken ct)
    {
        try
        {
            var updated = await _memoryService.UpdateAsync(id, request, _currentUser.UserId, ct);
            return Ok(updated);
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
    /// Supersede an existing memory with a new one.
    /// The old memory is marked as Superseded; the new memory references it.
    /// </summary>
    [HttpPost("{id:guid}/supersede")]
    public async Task<ActionResult<MemoryDto>> Supersede(
        Guid id,
        [FromBody] CreateMemoryRequest replacementRequest,
        CancellationToken ct)
    {
        try
        {
            var replacement = await _memoryService.SupersedeAsync(
                id, replacementRequest, _currentUser.UserId, ct);
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
    /// Soft-delete a memory. Only the owner can delete.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _memoryService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Get memory statistics for the authenticated user.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<MemoryStatsDto>> GetStats(CancellationToken ct)
    {
        var stats = await _memoryService.GetStatsAsync(_currentUser.UserId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get project context by project ID.
    /// </summary>
    [HttpGet("~/api/agent/project/{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetProject(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(id, ct);
        if (project == null) return NotFound();
        return Ok(project);
    }

    /// <summary>
    /// List all projects the authenticated user has access to.
    /// </summary>
    [HttpGet("~/api/agent/projects")]
    public async Task<ActionResult<List<ProjectDto>>> ListProjects(CancellationToken ct)
    {
        var projects = await _projectService.GetAllAsync(ct);
        return Ok(projects);
    }
}
