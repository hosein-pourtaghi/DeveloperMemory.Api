using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.DTOs;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DeveloperMemory.Api.Infrastructure.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// Single authoritative intelligence path:
///   Request → PromptIntelligenceEngine → PromptPackage → PromptBuilder → Provider
/// 
/// The gateway does NOT maintain a separate direct retrieval fallback.
/// All degradation is owned by the Prompt Intelligence Engine.
/// </summary>
[ApiController]
[Route("v1")]
public class OpenAIChatCompletionController : ControllerBase
{
    private readonly FreeLlmApiClient _providerClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly KnowledgeService _knowledgeService;
    private readonly ProfileService _profileService;
    private readonly IPromptIntelligenceEngine _intelligenceEngine;
    private readonly RequestLogger _requestLogger;
    private readonly ModelSelectionSettings _modelSelection;
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
        IPromptIntelligenceEngine intelligenceEngine,
        RequestLogger requestLogger,
        IOptions<ModelSelectionSettings> modelSelection,
        ILogger<OpenAIChatCompletionController> logger)
    {
        _providerClient = providerClient;
        _promptBuilder = promptBuilder;
        _knowledgeService = knowledgeService;
        _profileService = profileService;
        _intelligenceEngine = intelligenceEngine;
        _requestLogger = requestLogger;
        _modelSelection = modelSelection.Value;
        _logger = logger;
    }

    [HttpPost("chat/completions")]
    public async Task ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.Messages == null || request.Messages.Count == 0)
        {
            await WriteErrorResponse(HttpContext, StatusCodes.Status400BadRequest,
                "messages must be a non-empty array", "invalid_request_error", "messages");
            return;
        }

        if (!_providerClient.IsConfigured)
        {
            await WriteErrorResponse(HttpContext, StatusCodes.Status503ServiceUnavailable,
                "Downstream LLM provider is not configured. Set AppSettings:FreeLlmApi:BaseUrl.",
                "server_error", "configuration");
            return;
        }

        var isStreaming = request.Stream == true;
        var incomingTokens = TokenEstimator.EstimateRequestTokens(request);

        // ── Step 1: Log incoming request ──
        await _requestLogger.LogRequestAsync(
            "INCOMING",
            request,
            incomingTokens: incomingTokens,
            isStreaming: isStreaming);

        try
        {
            // ── Step 2: Detect mode and select model ──
            var mode = ModeDetector.DetectMode(request);
            string selectedModel;

            if (_modelSelection.AutoSelectModel)
            {
                selectedModel = mode switch
                {
                    ModeDetector.TaskMode.Plan => _modelSelection.PlanModel,
                    ModeDetector.TaskMode.Build => _modelSelection.BuildModel,
                    _ => _providerClient.ResolveModel(request.Model)
                };
            }
            else
            {
                selectedModel = _providerClient.ResolveModel(request.Model);
            }

            request.Model = selectedModel;

            _logger.LogInformation(
                "Mode detected: {Mode} | Selected model: {Model} | AutoSelect: {AutoSelect}",
                mode, selectedModel, _modelSelection.AutoSelectModel);

            // ── Step 3: Load knowledge and profiles ──
            var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user");
            var searchQuery = lastUserMessage?.Content;

            var profiles = await _profileService.LoadProfilesAsync();
            var searchResults = !string.IsNullOrWhiteSpace(searchQuery)
                ? _knowledgeService.SearchDocuments(searchQuery, request.Project, request.Tags)
                : new List<SearchResult>();

            // ── Step 4: Prompt Intelligence Engine (single authoritative path) ──
            // Parse context from request
            Guid? projectGuid = null;
            if (!string.IsNullOrWhiteSpace(request.Project) &&
                Guid.TryParse(request.Project, out var parsed))
            {
                projectGuid = parsed;
            }

            var promptPackage = await _intelligenceEngine.ProcessAsync(
                searchQuery ?? string.Empty,
                request.User ?? "anonymous",
                projectGuid,
                request.WorkspaceId,
                contextTokenBudget: 4000,
                ct: cancellationToken);

            _logger.LogInformation(
                "Intelligence: status={Status}, intent={Intent}, task={TaskType}, " +
                "memories={Refined}/{Candidate}, constraints={Constraints}, " +
                "{Duration}ms, warnings={Warnings}",
                promptPackage.Status,
                promptPackage.Analysis.Intent,
                promptPackage.Analysis.TaskType,
                promptPackage.Metadata.RefinedMemoryCount,
                promptPackage.Metadata.CandidateMemoryCount,
                promptPackage.Metadata.ConstraintsResolved,
                promptPackage.Metadata.TotalDurationMs,
                promptPackage.Warnings.Count);

            // ── Step 5: Build enriched request ──
            // The PromptPackage.OptimizedPrompt is the single source of intelligence context.
            // No fallback path exists. Degradation is handled inside the engine.
            var enrichedRequest = _promptBuilder.BuildEnrichedRequest(
                request, profiles, searchResults,
                intelligenceContext: promptPackage.OptimizedPrompt);
            var enrichedTokens = TokenEstimator.EstimateRequestTokens(enrichedRequest);

            // ── Step 6: Log enriched request ──
            await _requestLogger.LogRequestAsync(
                "ENRICHED",
                enrichedRequest,
                selectedModel: selectedModel,
                incomingTokens: incomingTokens,
                enrichedTokens: enrichedTokens,
                isStreaming: isStreaming);

            // ── Step 7: Forward to downstream provider ──
            var startTime = DateTime.UtcNow;

            if (isStreaming)
            {
                await HandleStreamingRequest(enrichedRequest, cancellationToken);
            }
            else
            {
                await HandleNonStreamingRequest(enrichedRequest, selectedModel, incomingTokens, enrichedTokens, cancellationToken);
            }

            var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "Request completed | status={Status} | mode={Mode} | model={Model} | incoming={Incoming} | enriched={Enriched} | latency={Latency}ms",
                promptPackage.Status, mode, selectedModel, incomingTokens, enrichedTokens, latencyMs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Chat completion request was cancelled by client");
        }
        catch (DownstreamProviderException ex)
        {
            _logger.LogError(ex, "Downstream provider error for chat completion");
            var (statusCode, errorType) = MapProviderError(ex.StatusCode);
            await WriteErrorResponse(HttpContext, statusCode, ex.RawErrorContent, errorType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing chat completion request");
            await WriteErrorResponse(HttpContext, StatusCodes.Status500InternalServerError,
                "An internal error occurred while processing the request", "server_error");
        }
    }

    private async Task HandleNonStreamingRequest(
        OpenAIChatCompletionRequest enrichedRequest,
        string selectedModel,
        int incomingTokens,
        int enrichedTokens,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var response = await _providerClient.SendCompletionAsync(enrichedRequest, cancellationToken);
        var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        if (string.IsNullOrEmpty(response.Model))
        {
            response.Model = selectedModel;
        }

        var responseTokens = TokenEstimator.EstimateResponseTokens(response);
        var providerTokens = response.Usage?.TotalTokens;

        await _requestLogger.LogRequestAsync(
            "RESPONSE",
            enrichedRequest,
            selectedModel: selectedModel,
            incomingTokens: incomingTokens,
            enrichedTokens: enrichedTokens,
            responseTokens: responseTokens,
            providerTokens: providerTokens,
            latencyMs: latencyMs,
            isStreaming: false);

        _logger.LogWarning(
            "TokenSummary: incoming=~{Incoming} | enriched=~{Enriched} | response=~{Response} | provider={Provider} | enrichment_overhead=~{Overhead} tokens",
            incomingTokens, enrichedTokens, responseTokens, providerTokens, enrichedTokens - incomingTokens);

        Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(Response.Body, response, JsonOptions, cancellationToken);
    }

    private async Task HandleStreamingRequest(
        OpenAIChatCompletionRequest enrichedRequest, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        using var providerResponse = await _providerClient.SendStreamingCompletionAsync(enrichedRequest, cancellationToken);
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

                if (line.StartsWith("data: [DONE]"))
                    break;
            }
        }
        finally
        {
            await writer.FlushAsync(CancellationToken.None);
        }
    }

    // ── Model Endpoints ──

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

    // ── Error Helpers ──

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
