using DeveloperMemory.Api.Abstractions;
using Microsoft.AspNetCore.Authorization;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using DeveloperMemory.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DeveloperMemory.Api.Infrastructure.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Controllers;

/// <summary>
/// Gateway controller for OpenAI-compatible chat completions.
/// 
/// Orchestration flow:
///   1. Validate request
///   2. Detect mode, select model
///   3. Load profiles + knowledge (file-based context, formatted as text)
///   4. Run IPromptIntelligenceEngine (analysis + memory retrieval + optimization + prompt assembly)
///   5. Inject enriched prompt into system message
///   6. Forward to provider via IModelGateway
/// 
/// The controller owns HTTP concerns, mode detection, model selection, logging,
/// and provider forwarding. All prompt context assembly is delegated to the engine.
/// </summary>
[ApiController]
[Route("v1")]
[Authorize]
public class OpenAIChatCompletionController : ControllerBase
{
    private readonly IModelGateway _modelGateway;
    private readonly KnowledgeService _knowledgeService;
    private readonly ProfileService _profileService;
    private readonly IPromptIntelligenceEngine _intelligenceEngine;
    private readonly IAgentContextProvider _agentContextProvider;
    private readonly RequestLogger _requestLogger;
    private readonly ModelSelectionSettings _modelSelection;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<OpenAIChatCompletionController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAIChatCompletionController(
        IModelGateway modelGateway,
        KnowledgeService knowledgeService,
        ProfileService profileService,
        IPromptIntelligenceEngine intelligenceEngine,
        IAgentContextProvider agentContextProvider,
        RequestLogger requestLogger,
        IOptions<ModelSelectionSettings> modelSelection,
        ICurrentUser currentUser,
        ILogger<OpenAIChatCompletionController> logger)
    {
        _modelGateway = modelGateway;
        _knowledgeService = knowledgeService;
        _profileService = profileService;
        _intelligenceEngine = intelligenceEngine;
        _agentContextProvider = agentContextProvider;
        _requestLogger = requestLogger;
        _modelSelection = modelSelection.Value;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpPost("chat/completions")]
    public async Task ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            await WriteErrorResponse(
                HttpContext,
                StatusCodes.Status400BadRequest,
                validationError.Value.Message,
                "invalid_request_error",
                validationError.Value.Param);
            return;
        }

        if (!_modelGateway.IsConfigured)
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

            // An explicit client model always wins. Automatic routing only applies
            // when the client omitted the model, preserving the configured default-model behavior.
            if (!string.IsNullOrWhiteSpace(request.Model))
            {
                selectedModel = request.Model;
            }
            else if (_modelSelection.AutoSelectModel)
            {
                selectedModel = mode switch
                {
                    ModeDetector.TaskMode.Plan => _modelSelection.PlanModel,
                    ModeDetector.TaskMode.Build => _modelSelection.BuildModel,
                    _ => _modelGateway.ResolveModel(null)
                };
            }
            else
            {
                selectedModel = _modelGateway.ResolveModel(null);
            }

            request.Model = selectedModel;

            _logger.LogInformation(
                "Mode detected: {Mode} | Selected model: {Model} | AutoSelect: {AutoSelect}",
                mode, selectedModel, _modelSelection.AutoSelectModel);

            // ── Step 3: Extract conversation history for intelligence ──
            // Open WebUI sends the full conversation in messages[].
            // Extract all user messages as conversation context for project resolution
            // and memory detection.
            var messages = request.Messages!;
            var conversationHistory = messages
                .Where(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m.Content!)
                .ToList();

            var lastUserMessage = messages.LastOrDefault(m => m.Role == "user");
            var searchQuery = lastUserMessage?.Content;

            var profiles = await _profileService.LoadProfilesAsync();
            var profileContext = FormatProfileContext(profiles);

            var searchResults = !string.IsNullOrWhiteSpace(searchQuery)
                ? _knowledgeService.SearchDocuments(searchQuery, request.Project, request.Tags)
                : new List<SearchResult>();
            var knowledgeContext = FormatKnowledgeContext(searchResults);

            // ── Step 4: Prompt Intelligence Engine (single authoritative path) ──
            // The engine handles analysis, memory retrieval, constraints, context assembly,
            // prompt composition, optimization, and now also includes profile/knowledge context.
            Guid? projectGuid = null;
            if (!string.IsNullOrWhiteSpace(request.Project) &&
                Guid.TryParse(request.Project, out var parsed))
            {
                projectGuid = parsed;
            }

            // ── Step 4a: Resolve agent context (Phase W integration) ──
            // When agent_id is provided in the request, resolve agent type and task intent
            // to enrich memory retrieval. Agent context does NOT bypass privacy/isolation.
            AgentContext? agentContext = null;
            if (!string.IsNullOrWhiteSpace(request.AgentId))
            {
                var agentRequest = new AgentContextRequest
                {
                    AgentId = request.AgentId,
                    Task = searchQuery,
                    ProjectId = projectGuid,
                    WorkspaceId = request.WorkspaceId,
                    Tags = request.Tags,
                    ConversationHistory = conversationHistory
                };

                // Apply agent_type hint if provided
                if (!string.IsNullOrWhiteSpace(request.AgentType) &&
                    Enum.TryParse<DeveloperMemory.Application.Contracts.AgentType>(request.AgentType, true, out var parsedAgentType))
                {
                    agentRequest.AgentType = parsedAgentType;
                }

                agentContext = _agentContextProvider.Resolve(agentRequest);

                _logger.LogInformation(
                    "Agent context resolved: agent={AgentId}, type={AgentType}, intent={Intent}, confidence={Confidence:F2}",
                    agentContext.AgentId, agentContext.AgentType, agentContext.TaskIntent, agentContext.Confidence);
            }

            var promptPackage = await _intelligenceEngine.ProcessAsync(
                searchQuery ?? string.Empty,
                _currentUser.UserId,
                projectGuid,
                request.WorkspaceId,
                contextTokenBudget: 4000,
                profileContext: profileContext,
                knowledgeContext: knowledgeContext,
                tags: request.Tags,
                conversationHistory: conversationHistory,
                agentContext: agentContext,
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
            // The PromptPackage.OptimizedPrompt is the single authoritative source of all context.
            // It includes intelligence-derived context, profiles, and knowledge.
            // Degradation is handled inside the engine.
            var enrichedRequest = InjectEnrichedPrompt(request, promptPackage.OptimizedPrompt);
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
        catch (Abstractions.DownstreamProviderException ex)
        {
            _logger.LogError(ex, "Downstream provider error for chat completion");
            var (statusCode, errorType, message) = MapProviderError(ex.StatusCode);
            await WriteErrorResponse(HttpContext, statusCode, message, errorType);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Downstream provider request timed out");
            await WriteErrorResponse(
                HttpContext,
                StatusCodes.Status504GatewayTimeout,
                "The request to the downstream provider timed out",
                "timeout_error");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Downstream provider request timed out");
            await WriteErrorResponse(
                HttpContext,
                StatusCodes.Status504GatewayTimeout,
                "The request to the downstream provider timed out",
                "timeout_error");
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
        var response = await _modelGateway.SendCompletionAsync(enrichedRequest, cancellationToken);
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

        await using var providerStream = await _modelGateway.SendStreamingCompletionAsync(enrichedRequest, cancellationToken);
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
            var upstreamModels = await _modelGateway.GetModelsAsync(cancellationToken);
            if (upstreamModels.Count > 0)
            {
                var modelList = new OpenAIModelListResponse
                {
                    Data = upstreamModels
                        .Where(model => !string.IsNullOrWhiteSpace(model))
                        .Distinct(StringComparer.Ordinal)
                        .Select(model => new OpenAIModel
                        {
                            Id = model,
                            Object = "model",
                            OwnedBy = "DeveloperMemory"
                        }).ToList()
                };

                if (modelList.Data.Count > 0)
                {
                    return Ok(modelList);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch models from upstream provider");
        }

        return StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = "No models are currently available from the downstream provider.",
                    Type = "server_error",
                    Code = "models_unavailable"
                }
            });
    }

    [HttpGet("models/{modelId}")]
    public async Task<ActionResult<OpenAIModel>> GetModel(string modelId, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _modelGateway.GetModelAsync(modelId, cancellationToken);
            if (model != null)
            {
                return Ok(model);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

    private static (int statusCode, string errorType, string message) MapProviderError(System.Net.HttpStatusCode providerStatus)
    {
        return providerStatus switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                (StatusCodes.Status502BadGateway, "upstream_error", "The downstream provider rejected the request."),
            System.Net.HttpStatusCode.Forbidden =>
                (StatusCodes.Status502BadGateway, "upstream_error", "The downstream provider rejected the request."),
            System.Net.HttpStatusCode.NotFound =>
                (StatusCodes.Status400BadRequest, "invalid_request_error", "The requested model or resource was not found."),
            System.Net.HttpStatusCode.TooManyRequests =>
                (StatusCodes.Status429TooManyRequests, "rate_limit_error", "The downstream provider rate limit was exceeded."),
            System.Net.HttpStatusCode.RequestTimeout =>
                (StatusCodes.Status504GatewayTimeout, "timeout_error", "The request to the downstream provider timed out."),
            _ =>
                (StatusCodes.Status502BadGateway, "upstream_error", "The downstream provider returned an error.")
        };
    }

    private static (string Message, string Param)? ValidateRequest(OpenAIChatCompletionRequest? request)
    {
        if (request?.Messages == null || request.Messages.Count == 0)
            return ("messages must be a non-empty array", "messages");

        if (request.Messages.Any(message => string.IsNullOrWhiteSpace(message.Role)))
            return ("each message must include a role", "messages");

        if (request.Messages.Any(message =>
                string.IsNullOrWhiteSpace(message.Content) &&
                (message.ToolCalls == null || message.ToolCalls.Count == 0)))
        {
            return ("each message must include content or tool_calls", "messages");
        }

        if (request.Temperature is < 0 or > 2)
            return ("temperature must be between 0 and 2", "temperature");

        if (request.TopP is < 0 or > 1)
            return ("top_p must be between 0 and 1", "top_p");

        if (request.N is <= 0)
            return ("n must be greater than 0", "n");

        if (request.MaxTokens is <= 0)
            return ("max_tokens must be greater than 0", "max_tokens");

        if (request.MaxCompletionTokens is <= 0)
            return ("max_completion_tokens must be greater than 0", "max_completion_tokens");

        if (request.MaxTokens.HasValue && request.MaxCompletionTokens.HasValue)
            return ("max_tokens and max_completion_tokens cannot both be set", "max_tokens");

        if (request.FrequencyPenalty is < -2 or > 2)
            return ("frequency_penalty must be between -2 and 2", "frequency_penalty");

        if (request.PresencePenalty is < -2 or > 2)
            return ("presence_penalty must be between -2 and 2", "presence_penalty");

        return null;
    }

    // ── Context formatting helpers ──

    /// <summary>
    /// Formats developer profiles as a text block for inclusion in the prompt.
    /// Profiles are static identity context — always included when available.
    /// </summary>
    private static string FormatProfileContext(List<DeveloperProfile> profiles)
    {
        if (profiles.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("[Developer Profile]");

        foreach (var profile in profiles)
        {
            sb.AppendLine($"Name: {profile.Name}");
            sb.AppendLine($"Role: {profile.Role}");
            if (profile.Skills.Count > 0)
                sb.AppendLine($"Skills: {string.Join(", ", profile.Skills)}");
            if (!string.IsNullOrWhiteSpace(profile.Experience))
                sb.AppendLine($"Experience: {profile.Experience}");
            if (!string.IsNullOrWhiteSpace(profile.Bio))
                sb.AppendLine($"Bio: {Truncate(profile.Bio, 500)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats knowledge search results as a text block for inclusion in the prompt.
    /// Only included when there are results matching the current query.
    /// </summary>
    private static string FormatKnowledgeContext(List<SearchResult> searchResults)
    {
        if (searchResults.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("[Relevant Knowledge]");

        var included = 0;
        foreach (var result in searchResults)
        {
            if (included >= 5) break;

            var contentPreview = Truncate(result.Content, 500);
            sb.AppendLine($"## {result.Title} (relevance: {result.Score:F2})");
            sb.AppendLine(contentPreview);
            sb.AppendLine();
            included++;
        }

        if (searchResults.Count > 5)
        {
            sb.AppendLine($"({searchResults.Count - 5} additional results omitted for brevity)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Injects the engine-produced enriched prompt into the OpenAI request's system message.
    /// The enriched prompt includes all intelligence context, profiles, and knowledge.
    /// </summary>
    private static OpenAIChatCompletionRequest InjectEnrichedPrompt(
        OpenAIChatCompletionRequest request, string enrichedContent)
    {
        if (string.IsNullOrWhiteSpace(enrichedContent))
            return request;

        var contextBlock = $"\n\n--- DeveloperMemory Context ---\n\n{enrichedContent}\n\n--- End DeveloperMemory Context ---\n\n";

        var enrichedMessages = new List<Message>();
        bool contextInjected = false;

        foreach (var message in request.Messages!)
        {
            if (message.Role == "system" && !contextInjected)
            {
                enrichedMessages.Add(new Message
                {
                    Role = "system",
                    Content = message.Content + contextBlock,
                    ExtensionData = message.ExtensionData
                });
                contextInjected = true;
            }
            else
            {
                enrichedMessages.Add(new Message
                {
                    Role = message.Role,
                    Content = message.Content,
                    ToolCalls = message.ToolCalls,
                    ToolCallId = message.ToolCallId,
                    Name = message.Name,
                    ExtensionData = message.ExtensionData
                });
            }
        }

        if (!contextInjected)
        {
            enrichedMessages.Insert(0, new Message
            {
                Role = "system",
                Content = $"You are a helpful assistant.{contextBlock}"
            });
        }

        return new OpenAIChatCompletionRequest
        {
            Model = request.Model,
            Messages = enrichedMessages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            N = request.N,
            Stream = request.Stream,
            Stop = request.Stop,
            MaxTokens = request.MaxTokens,
            MaxCompletionTokens = request.MaxCompletionTokens,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            User = request.User,
            StreamOptions = request.StreamOptions,
            Seed = request.Seed,
            Tools = request.Tools,
            ToolChoice = request.ToolChoice,
            LogitBias = request.LogitBias,
            // Preserve DeveloperMemory extension fields
            Project = request.Project,
            Tags = request.Tags,
            WorkspaceId = request.WorkspaceId,
            ProfileId = request.ProfileId,
            AgentId = request.AgentId,
            AgentType = request.AgentType,
            ExtensionData = request.ExtensionData
        };
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }
}
