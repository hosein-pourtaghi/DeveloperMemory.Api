using DeveloperMemory.Api.Models;
using System.IO;

namespace DeveloperMemory.Api.Abstractions;

/// <summary>
/// Provider-independent abstraction for sending model requests and receiving model responses.
/// Implementations handle the specifics of communicating with a particular LLM provider
/// (e.g., OpenAI-compatible APIs, local models, or other providers).
/// </summary>
/// <remarks>
/// This interface represents the architectural boundary between the application's gateway
/// logic and provider-specific implementations. Consumers depend on this abstraction rather
/// than on concrete provider clients, enabling provider replacement without core logic changes.
/// </remarks>
public interface IModelGateway
{
    /// <summary>
    /// Resolves the model to use for a request.
    /// Priority: per-request override > configured default > "auto".
    /// </summary>
    /// <param name="requestModel">The model requested by the caller, or null for default.</param>
    /// <returns>The resolved model identifier.</returns>
    string ResolveModel(string? requestModel);

    /// <summary>
    /// Checks whether the downstream provider is configured and available.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sends a chat completion request to the downstream provider (non-streaming).
    /// The request should already be enriched with context.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider's chat completion response.</returns>
    /// <exception cref="DownstreamProviderException">Thrown when the provider returns an error.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not configured.</exception>
    Task<OpenAIChatCompletionResponse> SendCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a streaming chat completion request to the downstream provider.
    /// Returns a readable stream of SSE (Server-Sent Events) data.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A readable stream of SSE data from the provider.</returns>
    /// <exception cref="DownstreamProviderException">Thrown when the provider returns an error.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not configured.</exception>
    Task<Stream> SendStreamingCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the list of available model identifiers from the upstream provider.
    /// Returns an empty list on failure (does not throw).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of model identifiers, or empty on failure.</returns>
    Task<List<string>> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches details for a specific model from the upstream provider.
    /// Returns null if not found or on failure.
    /// </summary>
    /// <param name="modelId">The model identifier to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model details, or null if not found.</returns>
    Task<OpenAIModel?> GetModelAsync(string modelId, CancellationToken cancellationToken = default);
}
