using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// Agent-aware context intelligence endpoint.
/// Provides context-enriched memory retrieval for AI agents.
/// 
/// This extends the existing Agent Memory API with:
///   - Agent identity and type resolution
///   - Task/intent classification
///   - Context-aware retrieval using Phase-S ranking
///   - Structured context assembly
///   - Instruction/constraint extraction
/// 
/// Existing Agent Memory API endpoints (CRUD) remain unchanged.
/// This controller adds context intelligence on top.
/// </summary>
[ApiController]
[Route("api/agent/context")]
[Authorize]
public class AgentContextController : ControllerBase
{
    private readonly IAgentContextService _contextService;
    private readonly IAgentContextProvider _contextProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AgentContextController> _logger;

    public AgentContextController(
        IAgentContextService contextService,
        IAgentContextProvider contextProvider,
        ICurrentUser currentUser,
        ILogger<AgentContextController> logger)
    {
        _contextService = contextService;
        _contextProvider = contextProvider;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve context-aware memories for an agent request.
    /// 
    /// The endpoint resolves agent identity, classifies task intent,
    /// and retrieves memories ranked by relevance to the agent's context.
    /// 
    /// All existing Phase-S lifecycle, scope, classification, and security
    /// filters are applied. Agent identity does NOT bypass any security rules.
    /// </summary>
    [HttpPost("retrieve")]
    public async Task<ActionResult<AgentContextResult>> RetrieveContext(
        [FromBody] AgentContextRetrievalRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return BadRequest(new { error = new { message = "AgentId is required.", code = "validation_error" } });
        }

        _logger.LogInformation(
            "Agent context retrieve: agent={AgentId}, type={AgentType}, task={Task}, project={ProjectId}",
            request.AgentId,
            request.AgentType?.ToString() ?? "(auto)",
            Truncate(request.Task, 50),
            request.ProjectId?.ToString() ?? "(none)");

        var result = await _contextService.RetrieveContextAsync(
            request, _currentUser.UserId, ct);

        return Ok(result);
    }

    /// <summary>
    /// Resolve agent context without performing retrieval.
    /// Useful for debugging context resolution or pre-checking context quality.
    /// </summary>
    [HttpPost("resolve")]
    public ActionResult<AgentContext> ResolveContext(
        [FromBody] AgentContextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            return BadRequest(new { error = new { message = "AgentId is required.", code = "validation_error" } });
        }

        var context = _contextProvider.Resolve(request);
        return Ok(context);
    }

    /// <summary>
    /// Get agent type classification for a given agent ID.
    /// Returns the inferred agent type and confidence.
    /// </summary>
    [HttpGet("agent-type")]
    public ActionResult<object> GetAgentType([FromQuery] string agentId, [FromQuery] string? task = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return BadRequest(new { error = new { message = "AgentId is required.", code = "validation_error" } });
        }

        var context = _contextProvider.Resolve(new AgentContextRequest
        {
            AgentId = agentId,
            Task = task
        });

        return Ok(new
        {
            agent_id = context.AgentId,
            agent_type = context.AgentType.ToString(),
            task_intent = context.TaskIntent.ToString(),
            confidence = context.Confidence,
            explanation = context.ResolutionExplanation
        });
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
