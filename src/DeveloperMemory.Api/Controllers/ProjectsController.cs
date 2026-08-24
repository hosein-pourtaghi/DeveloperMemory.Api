using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new project.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = new { message = "Project name is required.", code = "validation_error" } });
        }

        var project = await _projectService.CreateAsync(request, ct);
        _logger.LogInformation("Project created: {Id} - {Name}", project.Id, project.Name);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>
    /// List all projects.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetAll(CancellationToken ct)
    {
        var projects = await _projectService.GetAllAsync(ct);
        return Ok(projects);
    }

    /// <summary>
    /// Get a project by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(id, ct);
        if (project == null) return NotFound();
        return Ok(project);
    }

    /// <summary>
    /// Update a project.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        try
        {
            var project = await _projectService.UpdateAsync(id, request, ct);
            return Ok(project);
        }
        catch (ProjectNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Delete a project.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _projectService.DeleteAsync(id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
