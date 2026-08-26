using Xunit;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Tests verifying that the DeterministicPromptComposer correctly includes
/// profile and knowledge context in the composed prompt output.
/// </summary>
public class DeterministicPromptComposerProfileKnowledgeTests
{
    private readonly DeterministicPromptComposer _composer = new();

    [Fact]
    public void Compose_IncludesProfileContextWhenProvided()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.General,
            TaskType = TaskType.General
        };

        var result = _composer.Compose(
            analysis, [], [], "test request",
            profileContext: "[Developer Profile]\nName: Test Dev\n");

        Assert.Contains("[Developer Profile]", result.ComposedPrompt);
        Assert.Contains("Test Dev", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_IncludesKnowledgeContextWhenProvided()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.General,
            TaskType = TaskType.General
        };

        var result = _composer.Compose(
            analysis, [], [], "test request",
            knowledgeContext: "[Relevant Knowledge]\n## Doc1 (relevance: 0.80)\nContent\n");

        Assert.Contains("[Relevant Knowledge]", result.ComposedPrompt);
        Assert.Contains("Doc1", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_IncludesBothProfileAndKnowledge()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.General,
            TaskType = TaskType.General
        };

        var result = _composer.Compose(
            analysis, [], [], "test request",
            profileContext: "profile text",
            knowledgeContext: "knowledge text");

        Assert.Contains("profile text", result.ComposedPrompt);
        Assert.Contains("knowledge text", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_OmitsProfileContextWhenNull()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.General,
            TaskType = TaskType.General
        };

        var result = _composer.Compose(
            analysis, [], [], "test request",
            profileContext: null,
            knowledgeContext: null);

        Assert.DoesNotContain("[Developer Profile]", result.ComposedPrompt);
        Assert.DoesNotContain("[Relevant Knowledge]", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_OmitsProfileContextWhenEmpty()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.General,
            TaskType = TaskType.General
        };

        var result = _composer.Compose(
            analysis, [], [], "test request",
            profileContext: "",
            knowledgeContext: "   ");

        Assert.DoesNotContain("[Developer Profile]", result.ComposedPrompt);
        Assert.DoesNotContain("[Relevant Knowledge]", result.ComposedPrompt);
    }

    [Fact]
    public void Compose_ProfileAndKnowledgeAppearAfterIntelligenceSections()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            Intent = IntentType.General,
            TaskType = TaskType.General
        };

        var section = new ContextSection
        {
            SectionId = "test_section",
            Heading = "Test Section",
            Order = 10,
            Items = [new ContextItem { Content = "memory content" }]
        };

        var result = _composer.Compose(
            analysis, [], [section], "test request",
            profileContext: "PROFILE_HERE",
            knowledgeContext: "KNOWLEDGE_HERE");

        var intelligenceIdx = result.ComposedPrompt.IndexOf("Test Section", StringComparison.Ordinal);
        var profileIdx = result.ComposedPrompt.IndexOf("PROFILE_HERE", StringComparison.Ordinal);
        var knowledgeIdx = result.ComposedPrompt.IndexOf("KNOWLEDGE_HERE", StringComparison.Ordinal);

        Assert.True(intelligenceIdx < profileIdx, "Intelligence sections should appear before profile context");
        Assert.True(profileIdx < knowledgeIdx, "Profile context should appear before knowledge context");
    }
}

/// <summary>
/// Tests verifying the stub engine correctly records profile and knowledge context parameters.
/// </summary>
public class StubEngineProfileKnowledgeTests
{
    [Fact]
    public async Task ProcessAsync_RecordsProfileAndKnowledgeContext()
    {
        var engine = new StubPromptIntelligenceEngine();

        await engine.ProcessAsync(
            "test request", "user-1",
            profileContext: "[Developer Profile]\nName: Dev",
            knowledgeContext: "[Relevant Knowledge]\n## Doc1");

        Assert.Equal("[Developer Profile]\nName: Dev", engine.LastProfileContext);
        Assert.Equal("[Relevant Knowledge]\n## Doc1", engine.LastKnowledgeContext);
    }

    [Fact]
    public async Task ProcessAsync_NullProfileAndKnowledgeAreRecorded()
    {
        var engine = new StubPromptIntelligenceEngine();

        await engine.ProcessAsync("test request", "user-1");

        Assert.Null(engine.LastProfileContext);
        Assert.Null(engine.LastKnowledgeContext);
    }
}
