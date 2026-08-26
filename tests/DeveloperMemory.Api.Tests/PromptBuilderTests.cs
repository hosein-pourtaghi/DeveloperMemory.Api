using Xunit;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Unit tests for PromptBuilder covering prompt assembly behavior.
/// PromptBuilder enriches OpenAI requests with intelligence context, developer profiles,
/// and knowledge while preserving conversation history.
/// </summary>
public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    // ── No Context (Passthrough) ───────────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_ReturnsOriginal_WhenNoContext()
    {
        var request = CreateRequest("system", "user message");

        var result = _builder.BuildEnrichedRequest(request, [], []);

        // Should return the same request object when there's nothing to inject
        Assert.Same(request, result);
    }

    [Fact]
    public void BuildEnrichedRequest_ReturnsOriginal_WhenAllContextEmpty()
    {
        var request = CreateRequest("system", "user message");

        var result = _builder.BuildEnrichedRequest(
            request,
            profiles: [],
            searchResults: [],
            memories: null,
            intelligenceContext: null);

        Assert.Same(request, result);
    }

    // ── Intelligence Context Injection ─────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_InjectsIntelligenceContext()
    {
        var request = CreateRequest("You are helpful.", "Hello");

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "Custom intelligence context");

        Assert.NotSame(request, result);
        Assert.Contains("Custom intelligence context", result.Messages[0].Content);
        Assert.Contains("--- DeveloperMemory Context ---", result.Messages[0].Content);
        Assert.Contains("--- End DeveloperMemory Context ---", result.Messages[0].Content);
    }

    [Fact]
    public void BuildEnrichedRequest_IntelligenceContextAppearsBeforeEndMarker()
    {
        var request = CreateRequest("System prompt", "Hello");

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "Intelligence data");

        var content = result.Messages[0].Content!;
        var intelIndex = content.IndexOf("Intelligence data");
        var endIndex = content.IndexOf("--- End DeveloperMemory Context ---");

        Assert.True(intelIndex < endIndex,
            "Intelligence context should appear before the end marker");
    }

    // ── Profile Context Injection ───────────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_InjectsProfileContext()
    {
        var request = CreateRequest("System", "Hello");
        var profiles = new List<DeveloperProfile>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Alice",
                Role = "Senior Developer",
                Skills = ["C#", "Azure"],
                Experience = "10 years",
                Bio = "Full-stack developer"
            }
        };

        var result = _builder.BuildEnrichedRequest(request, profiles, []);

        Assert.Contains("[Developer Profile]", result.Messages[0].Content);
        Assert.Contains("Name: Alice", result.Messages[0].Content);
        Assert.Contains("Role: Senior Developer", result.Messages[0].Content);
        Assert.Contains("Skills: C#, Azure", result.Messages[0].Content);
        Assert.Contains("Experience: 10 years", result.Messages[0].Content);
    }

    [Fact]
    public void BuildEnrichedRequest_MultipleProfiles_AllIncluded()
    {
        var request = CreateRequest("System", "Hello");
        var profiles = new List<DeveloperProfile>
        {
            new() { Id = Guid.NewGuid(), Name = "Alice", Role = "Dev", Skills = [], Experience = "", Bio = "" },
            new() { Id = Guid.NewGuid(), Name = "Bob", Role = "QA", Skills = [], Experience = "", Bio = "" }
        };

        var result = _builder.BuildEnrichedRequest(request, profiles, []);

        Assert.Contains("Name: Alice", result.Messages[0].Content);
        Assert.Contains("Name: Bob", result.Messages[0].Content);
    }

    // ── Knowledge Context Injection ────────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_InjectsKnowledgeContext()
    {
        var request = CreateRequest("System", "Hello");
        var results = new List<SearchResult>
        {
            new() { Id = Guid.NewGuid(), Title = "API Design", Content = "REST best practices", Score = 0.95 }
        };

        var result = _builder.BuildEnrichedRequest(request, [], results);

        Assert.Contains("[Relevant Knowledge]", result.Messages[0].Content);
        Assert.Contains("## API Design (relevance: 0.95)", result.Messages[0].Content);
        Assert.Contains("REST best practices", result.Messages[0].Content);
    }

    [Fact]
    public void BuildEnrichedRequest_KnowledgeLimitedToFive()
    {
        var request = CreateRequest("System", "Hello");
        var results = new List<SearchResult>
        {
            new() { Id = Guid.NewGuid(), Title = "R1", Content = "C1", Score = 0.9 },
            new() { Id = Guid.NewGuid(), Title = "R2", Content = "C2", Score = 0.8 },
            new() { Id = Guid.NewGuid(), Title = "R3", Content = "C3", Score = 0.7 },
            new() { Id = Guid.NewGuid(), Title = "R4", Content = "C4", Score = 0.6 },
            new() { Id = Guid.NewGuid(), Title = "R5", Content = "C5", Score = 0.5 },
            new() { Id = Guid.NewGuid(), Title = "R6", Content = "C6", Score = 0.4 },
            new() { Id = Guid.NewGuid(), Title = "R7", Content = "C7", Score = 0.3 }
        };

        var result = _builder.BuildEnrichedRequest(request, [], results);

        Assert.Contains("## R1", result.Messages[0].Content);
        Assert.Contains("## R5", result.Messages[0].Content);
        Assert.DoesNotContain("## R6", result.Messages[0].Content);
        Assert.Contains("2 additional results omitted", result.Messages[0].Content);
    }

    // ── Conversation History Preservation ───────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_PreservesConversationHistory()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages =
            [
                new Message { Role = "system", Content = "You are helpful." },
                new Message { Role = "user", Content = "Hello" },
                new Message { Role = "assistant", Content = "Hi there!" },
                new Message { Role = "user", Content = "How are you?" }
            ]
        };

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "Context");

        // Should have same number of messages
        Assert.Equal(request.Messages.Count, result.Messages.Count);

        // System message is modified (context appended)
        Assert.Contains("Context", result.Messages[0].Content);

        // User and assistant messages are preserved exactly
        Assert.Equal("Hello", result.Messages[1].Content);
        Assert.Equal("Hi there!", result.Messages[2].Content);
        Assert.Equal("How are you?", result.Messages[3].Content);
    }

    [Fact]
    public void BuildEnrichedRequest_PreservesMessageRoles()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages =
            [
                new Message { Role = "system", Content = "System" },
                new Message { Role = "user", Content = "User" },
                new Message { Role = "assistant", Content = "Assistant" }
            ]
        };

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "ctx");

        Assert.Equal("system", result.Messages[0].Role);
        Assert.Equal("user", result.Messages[1].Role);
        Assert.Equal("assistant", result.Messages[2].Role);
    }

    // ── No System Message ──────────────────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_CreatesSystemMessage_WhenNoneExists()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "Context");

        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("system", result.Messages[0].Role);
        Assert.Contains("Context", result.Messages[0].Content);
        Assert.Equal("Hello", result.Messages[1].Content);
    }

    // ── Request Properties Preservation ─────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_PreservesModel()
    {
        var request = CreateRequest("System", "Hello");
        request.Model = "gpt-4o";

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "ctx");

        Assert.Equal("gpt-4o", result.Model);
    }

    [Fact]
    public void BuildEnrichedRequest_PreservesStreamFlag()
    {
        var request = CreateRequest("System", "Hello");
        request.Stream = true;

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "ctx");

        Assert.True(result.Stream);
    }

    [Fact]
    public void BuildEnrichedRequest_PreservesTemperature()
    {
        var request = CreateRequest("System", "Hello");
        request.Temperature = 0.7;

        var result = _builder.BuildEnrichedRequest(
            request, [], [], intelligenceContext: "ctx");

        Assert.Equal(0.7, result.Temperature);
    }

    // ── Combined Context ───────────────────────────────────────────────────

    [Fact]
    public void BuildEnrichedRequest_CombinesAllContextSources()
    {
        var request = CreateRequest("Original system", "Hello");
        var profiles = new List<DeveloperProfile>
        {
            new() { Id = Guid.NewGuid(), Name = "Dev", Role = "Engineer", Skills = [], Experience = "", Bio = "" }
        };
        var results = new List<SearchResult>
        {
            new() { Id = Guid.NewGuid(), Title = "Doc", Content = "Content", Score = 0.9 }
        };

        var result = _builder.BuildEnrichedRequest(
            request, profiles, results, intelligenceContext: "Intelligence");

        var content = result.Messages[0].Content!;
        Assert.Contains("Original system", content);
        Assert.Contains("Intelligence", content);
        Assert.Contains("[Developer Profile]", content);
        Assert.Contains("[Relevant Knowledge]", content);
    }

    // ── Legacy BuildPrompt ─────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_IncludesProfileAndKnowledge()
    {
        var promptRequest = new PromptRequest
        {
            SystemPrompt = "You are helpful.",
            Query = "test query",
            ProfileId = Guid.NewGuid().ToString()
        };
        var profiles = new List<DeveloperProfile>
        {
            new()
            {
                Id = Guid.Parse(promptRequest.ProfileId!),
                Name = "Test",
                Role = "Dev",
                Skills = ["C#"],
                Experience = "5 years",
                Bio = "Developer"
            }
        };
        var results = new List<SearchResult>
        {
            new() { Id = Guid.NewGuid(), Title = "Guide", Content = "Guide content", Score = 0.8 }
        };

        var prompt = _builder.BuildPrompt(promptRequest, profiles, results);

        Assert.Contains("You are helpful.", prompt);
        Assert.Contains("Test", prompt);
        Assert.Contains("C#", prompt);
        Assert.Contains("Guide", prompt);
        Assert.Contains("test query", prompt);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static OpenAIChatCompletionRequest CreateRequest(string systemContent, string userContent)
    {
        return new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages =
            [
                new Message { Role = "system", Content = systemContent },
                new Message { Role = "user", Content = userContent }
            ]
        };
    }
}
