using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// V2-2 Assistant / Orchestrator endpoint.
///
/// POST /api/agent/assistant executes one assistant turn:
///   Request → Assistant orchestrator → context assembly (UnifiedAgentContext)
///     → prompt construction → provider-agnostic model execution → response.
///
/// The controller is thin: it validates the request boundary, resolves the
/// authenticated user from the server-side principal, delegates execution to
/// the Application assistant orchestrator, and maps typed failures to
/// appropriate HTTP responses. All coordination lives behind existing
/// abstractions.
/// </summary>
[ApiController]
[Route("api/agent")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAssistantOrchestrator _assistant;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(
        IAssistantOrchestrator assistant,
        ICurrentUser currentUser,
        ILogger<AssistantController> logger)
    {
        _assistant = assistant;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Executes one assistant turn with unified context assembly.
    /// </summary>
    [HttpPost("assistant")]
    public async Task<ActionResult<AssistantExecutionResult>> Execute(
        [FromBody] AssistantExecutionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Task))
        {
            return BadRequest(new
            {
                error = new
                {
                    message = "Task is required.",
                    type = "invalid_request_error",
                    code = "validation_error",
                    param = "task"
                }
            });
        }

        _logger.LogInformation(
            "V2 assistant request: assistant={AssistantId}, task={Task}, project={ProjectId}, workspace={WorkspaceId}, model={Model}, mode={Mode}",
            request.AssistantId ?? "(default)",
            Truncate(request.Task, 50),
            request.ProjectId?.ToString() ?? "(none)",
            request.WorkspaceId ?? "(none)",
            request.Model ?? "(default)",
            request.ExecutionMode);

        try
        {
            var result = await _assistant.ExecuteAsync(request, _currentUser.UserId, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "V2 assistant request rejected");
            return BadRequest(new
            {
                error = new
                {
                    message = ex.Message,
                    type = "invalid_request_error",
                    code = "validation_error"
                }
            });
        }
        catch (AgentNotFoundException ex)
        {
            _logger.LogWarning("V2 assistant: agent not found: {AgentId}", request.AssistantId);
            return NotFound(new
            {
                error = new
                {
                    message = ex.Message,
                    type = "agent_error",
                    code = ex.ErrorCode
                }
            });
        }
        catch (AgentDisabledException ex)
        {
            _logger.LogWarning("V2 assistant: agent disabled: {AgentId}", request.AssistantId);
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                error = new
                {
                    message = ex.Message,
                    type = "agent_error",
                    code = ex.ErrorCode
                }
            });
        }
        catch (AssistantModelException ex)
        {
            _logger.LogWarning(
                "V2 assistant model error: code={Code}, status={Status}",
                ex.ErrorCode, ex.StatusCode);
            return StatusCode(ex.StatusCode, new
            {
                error = new
                {
                    message = ex.Message,
                    type = "model_error",
                    code = ex.ErrorCode
                }
            });
        }
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}