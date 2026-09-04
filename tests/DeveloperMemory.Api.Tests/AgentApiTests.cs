using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeveloperMemory.Api.Tests;

// ══════════════════════════════════════════════════════════════════════════════
// V2-3: Agent API contract tests (POST /api/agent/assistant + agent selection)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// E2EFactory variant that registers configured agents (enabled + disabled)
/// through the "Agents" configuration section.
/// </summary>
public class AgentConfigE2EFactory : E2EFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Agents:0:AgentId"] = "writer",
                ["Agents:Agents:0:Name"] = "Writer",
                ["Agents:Agents:0:Description"] = "Writes concise copy.",
                ["Agents:Agents:0:SystemInstructions"] = "You are the writer agent. Always write concise copy.",
                ["Agents:Agents:0:Enabled"] = "true",
                ["Agents:Agents:0:AgentType"] = "Documentation",

                ["Agents:Agents:1:AgentId"] = "retired",
                ["Agents:Agents:1:Name"] = "Retired",
                ["Agents:Agents:1:SystemInstructions"] = "This agent is retired.",
                ["Agents:Agents:1:Enabled"] = "false"
            });
        });
    }
}

public class AgentApiTests : IClassFixture<AgentConfigE2EFactory>
{
    private readonly HttpClient _client;
    private readonly AgentConfigE2EFactory _factory;

    public AgentApiTests(AgentConfigE2EFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static object BuildBody(string? assistantId = null) => new
    {
        task = "Summarize the team git convention",
        assistantId = assistantId
    };

    // ── Default vs explicit agent ──

    [Fact]
    public async Task Execute_NoAgentId_UsesDefaultAssistantPath()
    {
        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(BuildBody()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stub response", body.GetProperty("response").GetString());
        Assert.Null(body.GetProperty("execution").GetProperty("agentId").GetString());
    }

    [Fact]
    public async Task Execute_ExplicitConfiguredAgent_ReachesModelWithAgentInstructions()
    {
        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(BuildBody("writer")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("writer", body.GetProperty("execution").GetProperty("agentId").GetString());
        Assert.Equal("Writer", body.GetProperty("execution").GetProperty("agentName").GetString());

        // Agent instructions must reach the model abstraction in the system message.
        var forwarded = _factory.Gateway.CapturedRequests.Last();
        var system = forwarded.Messages.First(m => m.Role == "system").Content;
        Assert.Contains("--- Agent Instructions ---", system);
        Assert.Contains("You are the writer agent. Always write concise copy.", system);
    }

    // ── Unknown / disabled agents ──

    [Fact]
    public async Task Execute_UnknownAgent_Returns404()
    {
        var callsBefore = _factory.Gateway.CallCount;

        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(BuildBody("no-such-agent")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("agent_not_found", body.GetProperty("error").GetProperty("code").GetString());

        // No model call must happen for an unknown agent.
        Assert.Equal(callsBefore, _factory.Gateway.CallCount);
    }

    [Fact]
    public async Task Execute_DisabledAgent_Returns409()
    {
        var callsBefore = _factory.Gateway.CallCount;

        var response = await _client.PostAsync("/api/agent/assistant", JsonBody(BuildBody("retired")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("agent_disabled", body.GetProperty("error").GetProperty("code").GetString());

        // No model call must happen for a disabled agent.
        Assert.Equal(callsBefore, _factory.Gateway.CallCount);
    }

    // ── Unauthorized ──

    [Fact]
    public async Task Execute_WithoutAuthentication_Returns401()
    {
        var factory = new AssistantNoAuthE2EFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/agent/assistant", JsonBody(BuildBody("writer")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}