using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Api.Services;

/// <summary>
/// API-layer adapter implementing the Application <see cref="IAssistantModelExecutor"/>
/// port over the existing provider-agnostic <see cref="IModelGateway"/>.
///
/// This is the ONLY place the Assistant flow touches provider DTOs. The
/// orchestrator (Application layer) depends on the neutral port; swapping the
/// underlying model/provider means swapping the IModelGateway registration in
/// Program.cs without touching Assistant orchestration logic.
///
/// Provider failures are translated into <see cref="AssistantModelException"/>
/// with client-safe codes/statuses — raw provider content is never forwarded.
/// </summary>
public class AssistantModelGatewayExecutor : IAssistantModelExecutor
{
    private readonly IModelGateway _modelGateway;
    private readonly ILogger<AssistantModelGatewayExecutor> _logger;

    public AssistantModelGatewayExecutor(
        IModelGateway modelGateway,
        ILogger<AssistantModelGatewayExecutor> logger)
    {
        _modelGateway = modelGateway;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsConfigured => _modelGateway.IsConfigured;

    /// <inheritdoc/>
    public async Task<AssistantModelResponse> ExecuteAsync(
        AssistantModelRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Resolve the model: explicit preference wins, otherwise the gateway
        // resolves its configured default. No routing is performed.
        var selectedModel = _modelGateway.ResolveModel(request.Model);

        var openAiRequest = new OpenAIChatCompletionRequest
        {
            Model = selectedModel,
            Messages = request.Messages
                .Select(m => new Message { Role = m.Role, Content = m.Content })
                .ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = false
        };

        try
        {
            var response = await _modelGateway.SendCompletionAsync(openAiRequest, ct);

            var content = response.Choices
                .FirstOrDefault(c => c.Message != null)
                ?.Message.Content ?? string.Empty;

            return new AssistantModelResponse
            {
                Content = content,
                Model = string.IsNullOrEmpty(response.Model) ? selectedModel : response.Model,
                FinishReason = response.Choices.FirstOrDefault()?.FinishReason,
                PromptTokens = response.Usage?.PromptTokens,
                CompletionTokens = response.Usage?.CompletionTokens,
                TotalTokens = response.Usage?.TotalTokens
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DownstreamProviderException ex)
        {
            throw MapProviderError(ex);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Assistant model request timed out");
            throw new AssistantModelException(
                "The request to the model provider timed out.",
                "model_timeout",
                504);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Assistant model request timed out");
            throw new AssistantModelException(
                "The request to the model provider timed out.",
                "model_timeout",
                504);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assistant model execution failed");
            throw new AssistantModelException(
                "The model provider could not complete the request.",
                "model_upstream_error",
                502);
        }
    }

    /// <summary>
    /// Translates a downstream provider status into a client-safe
    /// AssistantModelException, mirroring the V1 gateway's error mapping.
    /// </summary>
    private static AssistantModelException MapProviderError(DownstreamProviderException ex)
    {
        return ex.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => new AssistantModelException(
                "The model provider rate limit was exceeded.",
                "model_rate_limited",
                429),
            System.Net.HttpStatusCode.RequestTimeout => new AssistantModelException(
                "The request to the model provider timed out.",
                "model_timeout",
                504),
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                new AssistantModelException(
                    "The model provider rejected the request.",
                    "model_upstream_error",
                    502),
            _ => new AssistantModelException(
                "The model provider returned an error.",
                "model_upstream_error",
                502)
        };
    }
}