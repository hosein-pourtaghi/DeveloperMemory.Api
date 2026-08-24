using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Xunit;

namespace DeveloperMemory.Api.Tests.Services;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    private static OpenAIChatCompletionRequest CreateRequest(string systemContent, string userContent = "Hello")
    {
        return new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<Message>
            {
                new Message { Role = "system", Content = systemContent },
                new Message { Role = "user", Content = userContent }
            }
        };
    }

    private static DeveloperProfile CreateProfile(string name = "Test Dev", string role = "Backend Dev")
    {
        return new DeveloperProfile
        {
            Id = Guid.NewGuid(),
            Name = name,
            Role = role,
            Skills = new List<string> { "C#", "ASP.NET", "TypeScript" },
            Experience = "5 years",
            Bio = "Full-stack developer focused on .NET and web technologies."
        };
    }

    private static SearchResult CreateSearchResult(string title = "Test Doc", double score = 0.8)
    {
        return new SearchResult
        {
            Id = Guid.NewGuid(),
            Title = title,
            Content = "This is test knowledge content about coding standards.",
            Score = score,
            FilePath = "/test/doc.md"
        };
    }

    [Fact]
    public void BuildEnrichedRequest_NoProfilesNoSearch_ReturnsOriginal()
    {
        var request = CreateRequest("You are helpful.");
        var result = _builder.BuildEnrichedRequest(request, new List<DeveloperProfile>(), new List<SearchResult>());
        Assert.Same(request, result);
    }

    [Fact]
    public void BuildEnrichedRequest_WithProfiles_AppendsContextToSystemMessage()
    {
        var request = CreateRequest("You are helpful.");
        var profiles = new List<DeveloperProfile> { CreateProfile() };

        var result = _builder.BuildEnrichedRequest(request, profiles, new List<SearchResult>());

        Assert.NotSame(request, result);
        var systemMessage = result.Messages.First(m => m.Role == "system");
        Assert.Contains("DeveloperMemory Context", systemMessage.Content);
        Assert.Contains("Test Dev", systemMessage.Content);
        Assert.Contains("Backend Dev", systemMessage.Content);
    }

    [Fact]
    public void BuildEnrichedRequest_WithKnowledge_AppendsContextToSystemMessage()
    {
        var request = CreateRequest("You are helpful.");
        var searchResults = new List<SearchResult> { CreateSearchResult() };

        var result = _builder.BuildEnrichedRequest(request, new List<DeveloperProfile>(), searchResults);

        var systemMessage = result.Messages.First(m => m.Role == "system");
        Assert.Contains("Relevant Knowledge", systemMessage.Content);
        Assert.Contains("Test Doc", systemMessage.Content);
    }

    [Fact]
    public void BuildEnrichedRequest_PreservesOriginalSystemMessage()
    {
        var originalSystem = "You are a coding assistant with specific rules.";
        var request = CreateRequest(originalSystem);

        var result = _builder.BuildEnrichedRequest(
            request,
            new List<DeveloperProfile> { CreateProfile() },
            new List<SearchResult>());

        var systemMessage = result.Messages.First(m => m.Role == "system");
        Assert.Contains(originalSystem, systemMessage.Content);
    }

    [Fact]
    public void BuildEnrichedRequest_PreservesUserMessages()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<Message>
            {
                new Message { Role = "system", Content = "You are helpful." },
                new Message { Role = "user", Content = "What is C#?" },
                new Message { Role = "assistant", Content = "C# is a programming language." },
                new Message { Role = "user", Content = "Tell me more." }
            }
        };

        var result = _builder.BuildEnrichedRequest(
            request,
            new List<DeveloperProfile> { CreateProfile() },
            new List<SearchResult>());

        // Should have same number of messages
        Assert.Equal(request.Messages.Count, result.Messages.Count);

        // User messages should be preserved exactly
        Assert.Equal("What is C#?", result.Messages[1].Content);
        Assert.Equal("Tell me more.", result.Messages[3].Content);
    }

    [Fact]
    public void BuildEnrichedRequest_NoSystemMessage_InjectsNewSystemMessage()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<Message>
            {
                new Message { Role = "user", Content = "Hello" }
            }
        };

        var result = _builder.BuildEnrichedRequest(
            request,
            new List<DeveloperProfile> { CreateProfile() },
            new List<SearchResult>());

        // Should now have 2 messages (injected system + original user)
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("system", result.Messages[0].Role);
        Assert.Contains("DeveloperMemory Context", result.Messages[0].Content);
        Assert.Equal("user", result.Messages[1].Role);
    }

    [Fact]
    public void BuildEnrichedRequest_KnowledgeLimitedToFiveResults()
    {
        var request = CreateRequest("You are helpful.");
        var searchResults = Enumerable.Range(1, 10)
            .Select(i => CreateSearchResult($"Doc {i}", 1.0 - i * 0.05))
            .ToList();

        var result = _builder.BuildEnrichedRequest(
            request,
            new List<DeveloperProfile>(),
            searchResults);

        var systemMessage = result.Messages.First(m => m.Role == "system");
        Assert.Contains("5 additional results omitted", systemMessage.Content);
    }

    [Fact]
    public void BuildEnrichedRequest_PreservesModelAndParameters()
    {
        var request = CreateRequest("You are helpful.");
        request.Temperature = 0.7;
        request.MaxTokens = 1000;
        request.Stream = true;

        var result = _builder.BuildEnrichedRequest(
            request,
            new List<DeveloperProfile> { CreateProfile() },
            new List<SearchResult>());

        Assert.Equal("gpt-4", result.Model);
        Assert.Equal(0.7, result.Temperature);
        Assert.Equal(1000, result.MaxTokens);
        Assert.True(result.Stream);
    }

    [Fact]
    public void BuildEnrichedRequest_ProfileSkillsIncludedInContext()
    {
        var request = CreateRequest("You are helpful.");
        var profile = CreateProfile();
        profile.Skills = new List<string> { "Rust", "Go", "Python" };

        var result = _builder.BuildEnrichedRequest(
            request,
            new List<DeveloperProfile> { profile },
            new List<SearchResult>());

        var systemMessage = result.Messages.First(m => m.Role == "system");
        Assert.Contains("Rust", systemMessage.Content);
        Assert.Contains("Go", systemMessage.Content);
        Assert.Contains("Python", systemMessage.Content);
    }
}
