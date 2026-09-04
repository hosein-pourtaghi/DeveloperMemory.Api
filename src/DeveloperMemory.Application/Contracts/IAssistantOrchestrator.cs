using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Application.Contracts;

/// <summary>
/// V2-2 Assistant / Orchestrator boundary.
///
/// The Assistant is an ORCHESTRATOR: it coordinates existing capabilities
/// rather than reimplementing them.
///
/// Pipeline:
///   Receive request
///     → Validate request
///     → Resolve authenticated user (supplied by the API boundary)
///     → Assemble UnifiedAgentContext via IContextAssemblyService
///     → Build the model exchange from the assembled context
///       (assistant instructions | runtime context | persistent intelligence | user request)
///     → Execute through the provider-agnostic model abstraction
///     → Return a structured assistant result
///
/// The Assistant does NOT perform memory retrieval, ranking, lifecycle
/// filtering, privacy filtering, context assembly, prompt analysis, or
/// provider HTTP calls itself. Each responsibility remains behind its
/// existing abstraction.
/// </summary>
public interface IAssistantOrchestrator
{
    /// <summary>
    /// Executes one assistant turn for the given authenticated owner.
    /// </summary>
    /// <param name="request">Assistant execution request (runtime + assistant config + execution options).</param>
    /// <param name="ownerId">Server-resolved authenticated user identity. Never client-supplied.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The structured assistant result (response, assembled context, execution metadata).</returns>
    /// <exception cref="ArgumentException">When the request is invalid (empty task, bad limits).</exception>
    /// <exception cref="AssistantModelException">When the configured model provider cannot serve the request.</exception>
    Task<AssistantExecutionResult> ExecuteAsync(
        AssistantExecutionRequest request,
        string ownerId,
        CancellationToken ct = default);
}

/// <summary>
/// Assistant execution request.
///
/// Carries only what is needed to execute one assistant turn:
///   - the user request + runtime context (project/workspace, tags, constraints, conversation)
///   - optional assistant identity/configuration (assistant instructions, agent classification)
///   - optional execution options (model, temperature, token limits)
///
/// All fields map 1:1 onto the V2-1 context-assembly boundary; assistant
/// configuration is intentionally minimal and not persisted.
/// </summary>
public class AssistantExecutionRequest
{
    // ── User request & runtime context ──

    /// <summary>Current task/request text. Required.</summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>Optional explicit retrieval query. Derived from Task when absent.</summary>
    public string? Query { get; set; }

    /// <summary>Active project identity (null when not in a project context).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Active workspace identity (null when not in a workspace context).</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Tags for scoped retrieval.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Explicit constraints for this request (runtime instructions, not standing behavior).</summary>
    public List<string>? Constraints { get; set; }

    /// <summary>Conversation history for the current conversation.</summary>
    public List<string>? ConversationHistory { get; set; }

    // ── Assistant identity / configuration (minimal, not persisted) ──

    /// <summary>Optional assistant identifier (e.g. "assistant", "orchestrator", "cursor").</summary>
    public string? AssistantId { get; set; }

    /// <summary>Optional explicit agent type classification. Ignored when AssistantId is absent.</summary>
    public AgentType? AgentType { get; set; }

    /// <summary>
    /// Assistant instructions governing behavior (system prompt content).
    /// When absent, a conservative default assistant instruction set is used.
    /// These are instructions TO the assistant, distinct from runtime
    /// constraints (which describe the current request).
    /// </summary>
    public string? Instructions { get; set; }

    // ── Context policy / execution options ──

    /// <summary>Maximum memories to retrieve.</summary>
    public int MaxResults { get; set; } = 20;

    /// <summary>Token budget applied to retrieved memory context.</summary>
    public int ContextTokenBudget { get; set; } = 4000;

    /// <summary>Optional model preference. The provider abstraction resolves the default when absent.</summary>
    public string? Model { get; set; }

    /// <summary>Optional sampling temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Optional maximum completion tokens.</summary>
    public int? MaxTokens { get; set; }

    // ── Execution mode (V2-4) ──

    /// <summary>
    /// Execution mode: Direct (default, preserves V2-2/V2-3 behavior),
    /// Decompose (forced bounded decomposition), or Auto (decompose only when
    /// the deterministic gate signals it). Defaults to Direct.
    /// Binds case-insensitively from "direct" | "auto" | "decompose".
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(AssistantExecutionModeConverter))]
    public AssistantExecutionMode ExecutionMode { get; set; } = AssistantExecutionMode.Direct;
}

/// <summary>
/// Case-insensitive string binding for <see cref="AssistantExecutionMode"/>
/// on the API boundary ("direct" | "auto" | "decompose").
/// </summary>
public sealed class AssistantExecutionModeConverter : System.Text.Json.Serialization.JsonConverter<AssistantExecutionMode>
{
    public override AssistantExecutionMode Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Enum.TryParse<AssistantExecutionMode>(value, ignoreCase: true, out var mode)
            ? mode
            : AssistantExecutionMode.Direct;
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        AssistantExecutionMode value,
        System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Assistant execution modes (V2-4).
/// </summary>
public enum AssistantExecutionMode
{
    /// <summary>Execute the request directly with one agent (default; preserves V2-3 behavior).</summary>
    Direct,

    /// <summary>Decompose the request into bounded subtasks and execute them.</summary>
    Decompose,

    /// <summary>Decompose only when the deterministic gate signals complexity; otherwise direct.</summary>
    Auto
}

/// <summary>
/// Structured result of one assistant execution.
/// Contains the assistant response, the assembled context that was consumed,
/// and execution metadata (durations, token usage, degradation warnings).
/// </summary>
public class AssistantExecutionResult
{
    /// <summary>The assistant's response text.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>The model that produced the response.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Provider finish reason, when available.</summary>
    public string? FinishReason { get; set; }

    /// <summary>
    /// The UnifiedAgentContext that was assembled and consumed for this turn.
    /// Runtime and persistent partitions remain strictly separated:
    /// context.Runtime holds current-execution information only,
    /// context.Persistent holds durable intelligence with full provenance.
    /// </summary>
    public UnifiedAgentContext Context { get; set; } = new();

    /// <summary>Execution metadata (durations, warnings, degradation).</summary>
    public AssistantExecutionMetadata Execution { get; set; } = new();

    /// <summary>Overall assistant execution status.</summary>
    public AssistantExecutionStatus Status { get; set; } = AssistantExecutionStatus.Success;

    /// <summary>True when the request was executed with the model provider.</summary>
    public bool ModelCalled { get; set; }

    /// <summary>Delegated task execution records (V2-4), when the request was decomposed.</summary>
    public IReadOnlyList<TaskExecutionRecord>? TaskExecutions { get; set; }
}

/// <summary>
/// Execution metadata for one assistant turn.
/// Useful for diagnostics and observability without persisting prompt content.
/// </summary>
public class AssistantExecutionMetadata
{
    /// <summary>Resolved agent identifier, when an agent was selected.</summary>
    public string? AgentId { get; set; }

    /// <summary>Resolved agent name, when an agent was selected.</summary>
    public string? AgentName { get; set; }

    /// <summary>Execution mode actually used for this turn (V2-4).</summary>
    public AssistantExecutionMode ExecutionMode { get; set; } = AssistantExecutionMode.Direct;

    /// <summary>Total assistant execution duration (ms).</summary>
    public double TotalDurationMs { get; set; }

    /// <summary>Model/provider execution duration (ms), when the model was called.</summary>
    public double? ModelDurationMs { get; set; }

    /// <summary>Prompt tokens reported by the provider, when available.</summary>
    public int? PromptTokens { get; set; }

    /// <summary>Completion tokens reported by the provider, when available.</summary>
    public int? CompletionTokens { get; set; }

    /// <summary>Total tokens reported by the provider, when available.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>True when the assembled context was degraded (warnings present).</summary>
    public bool ContextDegraded { get; set; }

    /// <summary>
    /// Non-fatal warnings produced during execution (context assembly warnings).
    /// Safe for clients — never contains provider secrets or raw stack traces.
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Overall status of an assistant execution.
/// </summary>
public enum AssistantExecutionStatus
{
    /// <summary>Execution completed; the model produced a response.</summary>
    Success,

    /// <summary>Execution completed but with degradation (e.g. memory retrieval degraded).</summary>
    Degraded,

    /// <summary>Execution failed before producing a response.</summary>
    Failed
}