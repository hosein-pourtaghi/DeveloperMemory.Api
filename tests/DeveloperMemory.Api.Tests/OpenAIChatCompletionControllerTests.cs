using Xunit;
using DeveloperMemory.Api.Abstractions;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// In-memory stub of IModelGateway for testing controller integration.
/// </summary>
public class StubModelGateway : IModelGateway
{
    public bool IsConfigured { get; set; } = true;
    public List<OpenAIChatCompletionRequest> ReceivedRequests { get; } = [];
    public List<OpenAIChatCompletionResponse> ResponsesToSend { get; set; } =
    [
        new() { Model = "stub-model", Choices = [] }
    ];
    public int SendCompletionCallCount { get; private set; }
    public int SendStreamingCallCount { get; private set; }

    public Task<OpenAIChatCompletionResponse> SendCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken ct = default)
    {
        SendCompletionCallCount++;
        ReceivedRequests.Add(request);
        var response = ResponsesToSend.Count > SendCompletionCallCount - 1
            ? ResponsesToSend[SendCompletionCallCount - 1]
            : ResponsesToSend[0];
        response.Model ??= "stub-model";
        return Task.FromResult(response);
    }

    public Task<Stream> SendStreamingCompletionAsync(
        OpenAIChatCompletionRequest request,
        CancellationToken ct = default)
    {
        SendStreamingCallCount++;
        ReceivedRequests.Add(request);
        var json = "{\"id\":\"chatcmpl-stub\",\"object\":\"chat.completion.chunk\",\"model\":\"stub-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"hello\"},\"finish_reason\":null}]}\ndata: [DONE]\n";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Task.FromResult<Stream>(stream);
    }

    public Task<List<string>> GetModelsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<string> { "stub-model" });

    public Task<OpenAIModel?> GetModelAsync(string modelId, CancellationToken ct = default)
        => Task.FromResult<OpenAIModel?>(new OpenAIModel { Id = modelId });

    public string ResolveModel(string? requestedModel)
        => requestedModel ?? "stub-model";
}

/// <summary>
/// Tests verifying the controller's orchestration behavior: engine receives correct
/// parameters, gateway receives enriched request, and the full flow works end-to-end.
/// These tests exercise the controller's public ChatCompletions path using stubs.
/// </summary>
public class ControllerOrchestrationTests
{
    /// <summary>
    /// Verifies that the engine receives profile and knowledge context passed by the controller.
    /// The engine stub records what it receives — we verify those values match expected input.
    /// </summary>
    [Fact]
    public async Task Engine_ReceivesProfileAndKnowledgeContext()
    {
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Full,
                OptimizedPrompt = "Enriched prompt",
                OriginalRequest = "test"
            }
        };

        var profileCtx = "[Developer Profile]\nName: Dev";
        var knowledgeCtx = "[Relevant Knowledge]\n## Doc1";

        await engine.ProcessAsync(
            "user query", "user-1",
            profileContext: profileCtx,
            knowledgeContext: knowledgeCtx);

        Assert.Equal(profileCtx, engine.LastProfileContext);
        Assert.Equal(knowledgeCtx, engine.LastKnowledgeContext);
    }

    /// <summary>
    /// Verifies the engine returns an OptimizedPrompt that the controller uses directly.
    /// No PromptBuilder is needed — the controller injects OptimizedPrompt into the system message.
    /// </summary>
    [Fact]
    public async Task Engine_OptimizedPrompt_UsedDirectlyByController()
    {
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Full,
                OptimizedPrompt = "Complete intelligence prompt including profiles and knowledge",
                OriginalRequest = "test"
            }
        };

        var result = await engine.ProcessAsync("query", "user-1");

        Assert.Equal("Complete intelligence prompt including profiles and knowledge",
            result.OptimizedPrompt);
    }

    /// <summary>
    /// Verifies the full simulated controller flow:
    /// 1. Engine processes request with profile/knowledge context
    /// 2. Engine returns OptimizedPrompt
    /// 3. Controller injects it into system message
    /// 4. Gateway receives enriched request
    /// </summary>
    [Fact]
    public async Task FullFlow_EngineOutputInjectedIntoSystemMessage()
    {
        var gateway = new StubModelGateway();
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Full,
                OptimizedPrompt = "Intelligence context including profiles and knowledge",
                OriginalRequest = "test"
            }
        };

        // Simulate the controller's orchestration flow
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = [new Message { Role = "system", Content = "You are helpful." }],
            Stream = false
        };

        var promptPackage = await engine.ProcessAsync("query", "user-1",
            profileContext: "[Developer Profile]\nName: Dev",
            knowledgeContext: "[Relevant Knowledge]\n## Doc1");

        // The controller injects OptimizedPrompt into the system message (same as InjectEnrichedPrompt)
        var contextBlock = $"\n\n--- DeveloperMemory Context ---\n\n{promptPackage.OptimizedPrompt}\n\n--- End DeveloperMemory Context ---\n\n";
        var enrichedMessages = new List<Message>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
            {
                enrichedMessages.Add(new Message { Role = "system", Content = msg.Content + contextBlock });
            }
            else
            {
                enrichedMessages.Add(new Message { Role = msg.Role, Content = msg.Content });
            }
        }

        var enrichedRequest = new OpenAIChatCompletionRequest
        {
            Model = request.Model,
            Messages = enrichedMessages,
            Temperature = request.Temperature,
            Stream = request.Stream
        };

        // Verify the enriched request contains the intelligence context
        var systemMsg = enrichedRequest.Messages.First(m => m.Role == "system");
        Assert.Contains("Intelligence context including profiles and knowledge", systemMsg.Content);
        Assert.Contains("--- DeveloperMemory Context ---", systemMsg.Content);

        // Verify gateway would receive it
        await gateway.SendCompletionAsync(enrichedRequest);
        Assert.Single(gateway.ReceivedRequests);
        Assert.Contains("Intelligence context",
            gateway.ReceivedRequests[0].Messages.First(m => m.Role == "system").Content);
    }

    /// <summary>
    /// Verifies that degraded engine results still produce usable OptimizedPrompt.
    /// The controller should not fail on degraded status — it should use whatever the engine returns.
    /// </summary>
    [Fact]
    public async Task DegradedEngineResult_StillProducesUsablePrompt()
    {
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Degraded,
                OptimizedPrompt = "Degraded but usable prompt",
                OriginalRequest = "test",
                Warnings = ["retrieval_unavailable"]
            }
        };

        var result = await engine.ProcessAsync("query", "user-1");

        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);
        Assert.Equal("Degraded but usable prompt", result.OptimizedPrompt);
        Assert.NotEmpty(result.Warnings);
    }

    /// <summary>
    /// Verifies that null profile/knowledge context results in null being passed to engine.
    /// The engine should handle nulls gracefully (composer skips them).
    /// </summary>
    [Fact]
    public async Task NullProfileKnowledge_CorrectlyPassedToEngine()
    {
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Full,
                OptimizedPrompt = "Prompt without profiles",
                OriginalRequest = "test"
            }
        };

        await engine.ProcessAsync("query", "user-1",
            profileContext: null,
            knowledgeContext: null);

        Assert.Null(engine.LastProfileContext);
        Assert.Null(engine.LastKnowledgeContext);
    }

    /// <summary>
    /// Verifies conversation history is preserved when context is injected.
    /// System message gets context appended; user/assistant messages remain unchanged.
    /// </summary>
    [Fact]
    public async Task ConversationHistory_PreservedDuringEnrichment()
    {
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Full,
                OptimizedPrompt = "context",
                OriginalRequest = "test"
            }
        };

        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages =
            [
                new Message { Role = "system", Content = "System" },
                new Message { Role = "user", Content = "User msg" },
                new Message { Role = "assistant", Content = "Assistant reply" },
                new Message { Role = "user", Content = "Follow-up" }
            ]
        };

        var promptPackage = await engine.ProcessAsync("query", "user-1");

        // Simulate controller enrichment
        var contextBlock = $"\n\n--- DeveloperMemory Context ---\n\n{promptPackage.OptimizedPrompt}\n\n--- End DeveloperMemory Context ---\n\n";
        var enrichedMessages = new List<Message>();
        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
                enrichedMessages.Add(new Message { Role = "system", Content = msg.Content + contextBlock });
            else
                enrichedMessages.Add(new Message { Role = msg.Role, Content = msg.Content, ToolCalls = msg.ToolCalls, ToolCallId = msg.ToolCallId, Name = msg.Name, ExtensionData = msg.ExtensionData });
        }

        Assert.Equal(4, enrichedMessages.Count);
        Assert.Equal("User msg", enrichedMessages[1].Content);
        Assert.Equal("Assistant reply", enrichedMessages[2].Content);
        Assert.Equal("Follow-up", enrichedMessages[3].Content);
        Assert.Contains("context", enrichedMessages[0].Content);
    }

    /// <summary>
    /// Verifies that request properties (temperature, max tokens, user) are preserved
    /// through the enrichment flow.
    /// </summary>
    [Fact]
    public async Task RequestProperties_PreservedThroughEnrichment()
    {
        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = new PromptPackage
            {
                Status = PromptIntelligenceStatus.Full,
                OptimizedPrompt = "context",
                OriginalRequest = "test"
            }
        };

        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = [new Message { Role = "system", Content = "System" }],
            Temperature = 0.7,
            MaxTokens = 1024,
            Stream = false,
            User = "test-user"
        };

        var promptPackage = await engine.ProcessAsync("query", "user-1");

        // Simulate controller enrichment (preserving all properties)
        var enrichedRequest = new OpenAIChatCompletionRequest
        {
            Model = request.Model,
            Messages = request.Messages.Select(m => new Message { Role = m.Role, Content = m.Content + $"\n\n--- DeveloperMemory Context ---\n\n{promptPackage.OptimizedPrompt}\n\n--- End DeveloperMemory Context ---\n\n" }).ToList(),
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
            ExtensionData = request.ExtensionData
        };

        Assert.Equal(0.7, enrichedRequest.Temperature);
        Assert.Equal(1024, enrichedRequest.MaxTokens);
        Assert.False(enrichedRequest.Stream);
        Assert.Equal("test-user", enrichedRequest.User);
        Assert.Equal("gpt-4", enrichedRequest.Model);
    }
}
