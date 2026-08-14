using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeveloperMemory.Api.Controllers;

[ApiController]
[Route("v1")]
public class OpenAIChatCompletionController : ControllerBase
{
    private readonly FreeLlmApiClient _freeLlmApiClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly KnowledgeService _knowledgeService;
    private readonly ProfileService _profileService;
    private readonly ILogger<OpenAIChatCompletionController> _logger;

    public OpenAIChatCompletionController(
        FreeLlmApiClient freeLlmApiClient,
        PromptBuilder promptBuilder,
        KnowledgeService knowledgeService,
        ProfileService profileService,
        ILogger<OpenAIChatCompletionController> logger)
    {
        _freeLlmApiClient = freeLlmApiClient;
        _promptBuilder = promptBuilder;
        _knowledgeService = knowledgeService;
        _profileService = profileService;
        _logger = logger;
    }

    [HttpPost("chat/completions")]
    public async Task<ActionResult<OpenAIChatCompletionResponse>> ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.Messages == null || request.Messages.Count == 0)
        {
            return BadRequest(new OpenAIChatCompletionResponse
            {
                Id = System.Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request?.Model ?? "unknown",
                Choices = new List<Choice>(),
                Usage = new Usage()
            });
        }

        try
        {
            // Extract the last user message for knowledge search
            var lastUserMessage = request.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage == null || string.IsNullOrWhiteSpace(lastUserMessage.Content))
            {
                return BadRequest(new OpenAIChatCompletionResponse
                {
                    Id = System.Guid.NewGuid().ToString(),
                    Object = "chat.completion",
                    Created = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Model = request.Model ?? "unknown",
                    Choices = new List<Choice>(),
                    Usage = new Usage()
                });
            }

            // Map OpenAI request to internal request
            var internalRequest = new PromptRequest
            {
                Query = lastUserMessage.Content,
                Project = request.Project,
                Tags = request.Tags,
                ProfileId = request.ProfileId,
                SystemPrompt = request.Messages.FirstOrDefault(m => m.Role == "system")?.Content ?? "You are a helpful assistant."
            };

            // Load profiles and documents
            var profiles = await _profileService.LoadProfilesAsync();
            var documents = await _knowledgeService.LoadDocumentsAsync();

            // Search documents using the user query
            var searchResults = _knowledgeService.SearchDocuments(
                internalRequest.Query,
                internalRequest.Project,
                internalRequest.Tags);

            // Build prompt with knowledge injection
            var prompt = _promptBuilder.BuildPrompt(internalRequest, profiles, searchResults);

            // Send to FreeLlm API
            var response = await _freeLlmApiClient.SendPromptAsync(prompt);

            // Map response to OpenAI format
            var openAIResponse = new OpenAIChatCompletionResponse
            {
                Id = System.Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? "gpt-3.5-turbo",
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new Message
                        {
                            Role = "assistant",
                            Content = response
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = null,
                    CompletionTokens = null,
                    TotalTokens = null
                }
            };

            return Ok(openAIResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OpenAI chat completion request");
            return StatusCode(500, new OpenAIChatCompletionResponse
            {
                Id = System.Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? "unknown",
                Choices = new List<Choice>(),
                Usage = new Usage()
            });
        }
    }

    [HttpGet("models")]
    public async Task<ActionResult<OpenAIModelListResponse>> GetModels()
    {
        try
        {
            // Try to get models from upstream API
            var upstreamModels = await _freeLlmApiClient.GetModelsAsync();
            if (upstreamModels != null && upstreamModels.Any())
            {
                var modelList = new OpenAIModelListResponse
                {
                    Data = upstreamModels.Select(m => new OpenAIModel
                    {
                        Id = m,
                        Object = "model",
                        Created = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        OwnedBy = "Upstream Provider"
                    }).ToList()
                };
                return Ok(modelList);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch models from upstream API, using defaults");
        }

        // Fallback to default models
        var models = new OpenAIModelListResponse
        { 
        };

        return Ok(models);
    }
}