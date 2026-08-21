using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("v1")]
public class OpenAIChatCompletionController : ControllerBase
{
    private readonly FreeLlmApiClient _providerClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly KnowledgeService _knowledgeService;
    private readonly ProfileService _profileService;
    private readonly ILogger<OpenAIChatCompletionController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAIChatCompletionController(
        FreeLlmApiClient providerClient,
        PromptBuilder promptBuilder,
        KnowledgeService knowledgeService,
        ProfileService profileService,
        ILogger<OpenAIChatCompletionController> logger)
    {
        _providerClient = providerClient;
        _promptBuilder = promptBuilder;
        _knowledgeService = knowledgeService;
        _profileService = profileService;
        _logger = logger;
    }

    /// <summary>
    /// POST /v1/chat/completions — OpenAI-compatible chat completion endpoint.
    /// Supports both streaming and non-streaming responses.
    /// Enriches requests with DeveloperMemory context (profiles + knowledge) before forwarding.
    /// </summary>
    [HttpPost("chat/completions")]
    public async Task ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        // Validate basic request
        if (request == null || request.Messages == null || request.Messages.Count == 0)
        {
            await WriteErrorResponse(HttpContext, StatusCodes.Status400BadRequest,
                "messages must be a non-empty array", "invalid_request_error", "messages");
            return;
        }

        // Check if provider is configured
        if (!_providerClient.IsConfigured)
        {
            await WriteErrorResponse(HttpContext, StatusCodes.Status503ServiceUnavailable,
                "Downstream LLM provider is not configured. Set AppSettings:FreeLlmApi:BaseUrl.",
                "server_error", "configuration");
            return;
        }

        try
        {
            // ── Step 1: Extract context for memory retrieval ──
            var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user");
            var searchQuery = lastUserMessage?.Content;

            // ── Step 2: Load developer profile and search knowledge ──
            var profiles = await _profileService.LoadProfilesAsync();
            var searchResults = !string.IsNullOrWhiteSpace(searchQuery)
                ? _knowledgeService.SearchDocuments(searchQuery, request.Project, request.Tags)
                : new List<SearchResult>();

            // ── Step 3: Build enriched request (preserves conversation history) ──
            var enrichedRequest = _promptBuilder.BuildEnrichedRequest(request, profiles, searchResults);

            // ── Step 4: Forward to downstream provider ──
            if (request.Stream == true)
            {
                await HandleStreamingRequest(enrichedRequest, cancellationToken);
            }
            else
            {
                await HandleNonStreamingRequest(enrichedRequest, cancellationToken);
            }
        }
        catch (DownstreamProviderException ex)
        {
            _logger.LogError(ex, "Downstream provider error for chat completion");
            var (statusCode, errorType) = MapProviderError(ex.StatusCode);
            await WriteErrorResponse(HttpContext, statusCode, ex.RawErrorContent, errorType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — this is normal, don't log as error
            _logger.LogDebug("Chat completion request was cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing chat completion request");
            await WriteErrorResponse(HttpContext, StatusCodes.Status500InternalServerError,
                "An internal error occurred while processing the request", "server_error");
        }
    }

    // ── Non-Streaming Handler ──────────────────────────────────────────────

    private async Task HandleNonStreamingRequest(
        OpenAIChatCompletionRequest enrichedRequest, CancellationToken cancellationToken)
    {
        var response = await _providerClient.SendCompletionAsync(enrichedRequest, cancellationToken);

        // Ensure response has the model field set
        if (string.IsNullOrEmpty(response.Model))
        {
            response.Model = enrichedRequest.Model ?? "unknown";
        }

        Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(Response.Body, response, JsonOptions, cancellationToken);
    }

    // ── Streaming Handler ──────────────────────────────────────────────────

    private async Task HandleStreamingRequest(
        OpenAIChatCompletionRequest enrichedRequest, CancellationToken cancellationToken)
    {
        // Set SSE headers
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        using var providerResponse = await _providerClient.SendStreamingCompletionAsync(enrichedRequest, cancellationToken);

        // Stream the response body directly to the client
        var providerStream = await providerResponse.Content.ReadAsStreamAsync(cancellationToken);
        var writer = new StreamWriter(Response.Body, encoding: System.Text.Encoding.UTF8, bufferSize: 8192, leaveOpen: true);

        try
        {
            using var reader = new StreamReader(providerStream);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;

                await writer.WriteLineAsync(line);
                await writer.FlushAsync(cancellationToken);

                // If the line is "data: [DONE]", we're done
                if (line.StartsWith("data: [DONE]"))
                    break;
            }
        }
        finally
        {
            await writer.FlushAsync(CancellationToken.None);
        }
    }

    // ── Model Endpoints ────────────────────────────────────────────────────

    /// <summary>
    /// GET /v1/models — List available models from the upstream provider.
    /// Falls back to a default model list if the upstream provider is unavailable.
    /// </summary>
    [HttpGet("models")]
    public async Task<ActionResult<OpenAIModelListResponse>> GetModels(CancellationToken cancellationToken)
    {
        try
        {
            var upstreamModels = await _providerClient.GetModelsAsync(cancellationToken);
            if (upstreamModels.Count > 0)
            {
                var modelList = new OpenAIModelListResponse
                {
                    Data = upstreamModels.Select(m => new OpenAIModel
                    {
                        Id = m,
                        Object = "model",
                        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        OwnedBy = "upstream-provider"
                    }).ToList()
                };
                return Ok(modelList);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch models from upstream provider");
        }

        // Fallback: return at least the configured default model
        var defaultModel = _providerClient.ResolveModel(null);
        var fallbackList = new OpenAIModelListResponse
        {
            Data =
            [
                new OpenAIModel
                {
                    Id = defaultModel,
                    Object = "model",
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    OwnedBy = "developer-memory"
                }
            ]
        };
        return Ok(fallbackList);
    }

    /// <summary>
    /// GET /v1/models/{modelId} — Retrieve details for a specific model.
    /// </summary>
    [HttpGet("models/{modelId}")]
    public async Task<ActionResult<OpenAIModel>> GetModel(string modelId, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _providerClient.GetModelAsync(modelId, cancellationToken);
            if (model != null)
            {
                return Ok(model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching model {ModelId}", modelId);
        }

        return NotFound(new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = $"Model '{modelId}' was not found",
                Type = "invalid_request_error",
                Code = "model_not_found",
                Param = "model"
            }
        });
    }

    // ── Error Helpers ──────────────────────────────────────────────────────

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string message, string errorType, string? param = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new OpenAIErrorResponse
        {
            Error = new OpenAIError
            {
                Message = message,
                Type = errorType,
                Param = param
            }
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, errorResponse, JsonOptions);
    }

    private static (int statusCode, string errorType) MapProviderError(System.Net.HttpStatusCode providerStatus)
    {
        return providerStatus switch
        {
            System.Net.HttpStatusCode.Unauthorized => (StatusCodes.Status401Unauthorized, "authentication_error"),
            System.Net.HttpStatusCode.Forbidden => (StatusCodes.Status403Forbidden, "permission_error"),
            System.Net.HttpStatusCode.NotFound => (StatusCodes.Status404NotFound, "invalid_request_error"),
            System.Net.HttpStatusCode.TooManyRequests => (StatusCodes.Status429TooManyRequests, "rate_limit_error"),
            System.Net.HttpStatusCode.RequestTimeout => (StatusCodes.Status504GatewayTimeout, "timeout_error"),
            _ => (StatusCodes.Status502BadGateway, "upstream_error")
        };
    }
}
