namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// Provider-agnostic model execution boundary for the Assistant orchestration
/// layer (Application). Implementations adapt an existing provider gateway
/// (e.g. the API layer's IModelGateway) to this neutral chat-exchange contract.
///
/// The Assistant orchestrator depends on this abstraction — never on a
/// concrete provider client, a specific vendor, a specific model, or HTTP
/// details. Changing the underlying model/provider requires only swapping
/// the implementation registered for this port.
/// </summary>
public interface IAssistantModelExecutor
{
    /// <summary>Whether the downstream model provider is configured and available.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Executes a chat exchange against the configured provider (non-streaming).
    /// </summary>
    /// <param name="request">Neutral chat exchange (system/user/assistant messages + options).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider's response mapped to neutral types.</returns>
    /// <exception cref="AssistantModelException">When the provider cannot serve the request.</exception>
    Task<AssistantModelResponse> ExecuteAsync(
        AssistantModelRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Neutral, provider-independent chat completion request produced by the
/// Assistant from the assembled UnifiedAgentContext.
/// </summary>
public class AssistantModelRequest
{
    /// <summary>Optional model preference. The executor resolves the provider default when absent.</summary>
    public string? Model { get; set; }

    /// <summary>The chat exchange (system context, conversation, user request).</summary>
    public List<AssistantChatMessage> Messages { get; set; } = [];

    /// <summary>Optional sampling temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Optional maximum completion tokens.</summary>
    public int? MaxTokens { get; set; }
}

/// <summary>
/// A single message in the neutral assistant chat exchange.
/// Role is one of "system", "user", or "assistant".
/// </summary>
public class AssistantChatMessage
{
    /// <summary>Message role: "system", "user", or "assistant".</summary>
    public string Role { get; set; } = "user";

    /// <summary>Message content.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Neutral, provider-independent model response.
/// </summary>
public class AssistantModelResponse
{
    /// <summary>The assistant's response text.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>The model that produced the response.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Finish reason, when provided by the provider.</summary>
    public string? FinishReason { get; set; }

    /// <summary>Prompt tokens, when reported by the provider.</summary>
    public int? PromptTokens { get; set; }

    /// <summary>Completion tokens, when reported by the provider.</summary>
    public int? CompletionTokens { get; set; }

    /// <summary>Total tokens, when reported by the provider.</summary>
    public int? TotalTokens { get; set; }
}