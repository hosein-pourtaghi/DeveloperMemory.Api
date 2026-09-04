using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeveloperMemory.Api.Models;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// V2-4: Assistant API tests — execution modes + delegation
// ══════════════════════════════════════════════════════════════════════════════

public class AssistantDelegationApiTests : IClassFixture<E2EFactory>
{
    private readonly HttpClient _client;
    private readonly E2EFactory _factory;

    public AssistantDelegationApiTests(E2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── Direct mode (default) preserved ──

    [Fact]
    public async Task Execute_DefaultMode_DirectPathPreserved()
    {
        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(new { task = "simple" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stub response", body.GetProperty("response").GetString());
        Assert.Equal(0, body.GetProperty("execution").GetProperty("executionMode").GetInt32()); // Direct = 0
        // No delegated task executions were produced on the direct path.
        if (body.TryGetProperty("taskExecutions", out var tasks))
        {
            Assert.Equal(JsonValueKind.Null, tasks.ValueKind);
        }
    }

    // ── Decompose mode: model produces decomposition, subtasks execute ──

    [Fact]
    public async Task Execute_DecomposeMode_SubtasksExecuteAndAggregate()
    {
        // The stub gateway returns a fixed response; that response must contain
        // the decomposition JSON so both the decomposition call AND each
        // subtask call complete. We configure the gateway's response list:
        // 1 = decomposition JSON, 2+ = subtask responses.
        _factory.Gateway.ResponsesToSend =
        [
            new OpenAIChatCompletionResponse
            {
                Id = "chatcmpl-decomp",
                Model = "stub-model",
                Choices =
                [
                    new Choice
                    {
                        Message = new Message
                        {
                            Role = "assistant",
                            Content =
                                "{\"tasks\":[" +
                                "{\"task_id\":\"t1\",\"description\":\"Research the topic\",\"agent_id\":\"assistant\",\"depends_on\":[]}," +
                                "{\"task_id\":\"t2\",\"description\":\"Write the summary\",\"agent_id\":\"assistant\",\"depends_on\":[\"t1\"]}" +
                                "]}"
                        }
                    }
                ]
            },
            new OpenAIChatCompletionResponse
            {
                Id = "chatcmpl-t1",
                Model = "stub-model",
                Choices = [new Choice { Message = new Message { Role = "assistant", Content = "RESEARCH OUTPUT" } }]
            },
            new OpenAIChatCompletionResponse
            {
                Id = "chatcmpl-t2",
                Model = "stub-model",
                Choices = [new Choice { Message = new Message { Role = "assistant", Content = "SUMMARY OUTPUT" } }]
            }
        ];

        _factory.Gateway.Reset();

        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(new
        {
            task = "Delegate the research and write a summary",
            executionMode = "decompose"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Aggregated result includes both subtask outputs.
        Assert.Contains("RESEARCH OUTPUT", body.GetProperty("response").GetString());
        Assert.Contains("SUMMARY OUTPUT", body.GetProperty("response").GetString());
        Assert.Equal(1, body.GetProperty("execution").GetProperty("executionMode").GetInt32()); // Decompose = 1

        var executions = body.GetProperty("taskExecutions");
        Assert.Equal(2, executions.GetArrayLength());
        Assert.Equal("t1", executions[0].GetProperty("taskId").GetString());
        Assert.Equal("t2", executions[1].GetProperty("taskId").GetString());
        Assert.All(executions.EnumerateArray(),
            e => Assert.Equal(0, e.GetProperty("status").GetInt32())); // Succeeded
    }

    // ── Decompose mode with invalid model JSON falls back to direct ──

    [Fact]
    public async Task Execute_DecomposeMode_InvalidDecompositionFallsBackToDirect()
    {
        _factory.Gateway.ResponsesToSend =
        [
            new OpenAIChatCompletionResponse
            {
                Id = "chatcmpl-bad",
                Model = "stub-model",
                Choices = [new Choice { Message = new Message { Role = "assistant", Content = "not json at all" } }]
            },
            new OpenAIChatCompletionResponse
            {
                Id = "chatcmpl-direct",
                Model = "stub-model",
                Choices = [new Choice { Message = new Message { Role = "assistant", Content = "direct fallback" } }]
            }
        ];

        _factory.Gateway.Reset();

        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(new
        {
            task = "Delegate the research and write a summary",
            executionMode = "decompose"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Fallback to direct execution preserves V2-3 behavior.
        Assert.Equal(0, body.GetProperty("execution").GetProperty("executionMode").GetInt32()); // Direct
        Assert.Equal("direct fallback", body.GetProperty("response").GetString());
    }

    // ── Auto mode ──

    [Fact]
    public async Task Execute_AutoMode_GateDecides()
    {
        // "hello" is a simple request — gate false → direct.
        _factory.Gateway.Reset();

        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(new
        {
            task = "hello",
            executionMode = "auto"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("execution").GetProperty("executionMode").GetInt32()); // Direct
    }
}