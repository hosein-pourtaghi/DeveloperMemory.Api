using Xunit;
using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Unit tests for ModeDetector covering all supported detection behavior and edge cases.
/// ModeDetector is a static class that analyzes system prompt content to detect
/// plan vs build mode from Cline-style requests.
/// </summary>
public class ModeDetectorTests
{
    // ── Build Mode Detection ───────────────────────────────────────────────

    [Fact]
    public void DetectMode_BuildMode_WithExecuteCommand()
    {
        var request = CreateRequestWithSystemContent("You can execute_command to run tasks.");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Build, mode);
    }

    [Fact]
    public void DetectMode_BuildMode_WithWriteToFile()
    {
        var request = CreateRequestWithSystemContent("Use write_to_file to create new files.");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Build, mode);
    }

    [Fact]
    public void DetectMode_BuildMode_WithReplaceInFile()
    {
        var request = CreateRequestWithSystemContent("Use replace_in_file to modify existing code.");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Build, mode);
    }

    [Fact]
    public void DetectMode_BuildMode_WhenBothToolAndPlanIndicators()
    {
        // When both are present, Build takes priority (tool execution phase)
        var request = CreateRequestWithSystemContent(
            "## Plan\nStep 1: execute_command to test");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Build, mode);
    }

    // ── Plan Mode Detection ────────────────────────────────────────────────

    [Fact]
    public void DetectMode_PlanMode_WithTaskHeader()
    {
        var request = CreateRequestWithSystemContent("# TASK: Analyze the codebase");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Plan, mode);
    }

    [Fact]
    public void DetectMode_PlanMode_WithChecklist()
    {
        var request = CreateRequestWithSystemContent("Checklist:\n- [ ] Review code\n- [ ] Write tests");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Plan, mode);
    }

    [Fact]
    public void DetectMode_PlanMode_WithTaskProgress()
    {
        var request = CreateRequestWithSystemContent("task_progress: analyzing requirements");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Plan, mode);
    }

    [Fact]
    public void DetectMode_PlanMode_WithPlanHeader()
    {
        var request = CreateRequestWithSystemContent("## Plan\n1. Understand requirements\n2. Design solution");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Plan, mode);
    }

    [Fact]
    public void DetectMode_PlanMode_WithGoal()
    {
        var request = CreateRequestWithSystemContent("Goal: Refactor the authentication module");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Plan, mode);
    }

    // ── Unknown Mode Detection ─────────────────────────────────────────────

    [Fact]
    public void DetectMode_Unknown_WithNoIndicators()
    {
        var request = CreateRequestWithSystemContent("You are a helpful assistant.");

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_Unknown_WithEmptySystemMessage()
    {
        var request = CreateRequestWithSystemContent(string.Empty);

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    // ── Edge Cases ─────────────────────────────────────────────────────────

    [Fact]
    public void DetectMode_Unknown_WithNullRequest()
    {
        var mode = ModeDetector.DetectMode(null!);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_Unknown_WithNullMessages()
    {
        var request = new OpenAIChatCompletionRequest { Messages = null! };

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_Unknown_WithEmptyMessages()
    {
        var request = new OpenAIChatCompletionRequest { Messages = [] };

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_Unknown_WithNoSystemMessage()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Messages = [new Message { Role = "user", Content = "Hello" }]
        };

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_Unknown_WithSystemMessageButNullContent()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Messages = [new Message { Role = "system", Content = null }]
        };

        var mode = ModeDetector.DetectMode(request);

        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_BuildMode_CaseSensitive()
    {
        // The detection is case-sensitive for tool names
        var request = CreateRequestWithSystemContent("Use EXECUTE_COMMAND to run.");

        var mode = ModeDetector.DetectMode(request);

        // "EXECUTE_COMMAND" is not the same as "execute_command"
        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_PlanMode_OnlyFirstSystemMessage()
    {
        // Only the first system message is checked
        var request = new OpenAIChatCompletionRequest
        {
            Messages =
            [
                new Message { Role = "system", Content = "You are helpful." },
                new Message { Role = "system", Content = "# TASK: do something" }
            ]
        };

        var mode = ModeDetector.DetectMode(request);

        // The first system message has no indicators
        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    [Fact]
    public void DetectMode_IgnoresUserAndAssistantMessages()
    {
        var request = new OpenAIChatCompletionRequest
        {
            Messages =
            [
                new Message { Role = "system", Content = "You are helpful." },
                new Message { Role = "user", Content = "execute_command please" },
                new Message { Role = "assistant", Content = "# TASK: plan this" }
            ]
        };

        var mode = ModeDetector.DetectMode(request);

        // Only system message is checked; user/assistant content is ignored
        Assert.Equal(ModeDetector.TaskMode.Unknown, mode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static OpenAIChatCompletionRequest CreateRequestWithSystemContent(string content)
    {
        return new OpenAIChatCompletionRequest
        {
            Model = "test-model",
            Messages =
            [
                new Message { Role = "system", Content = content },
                new Message { Role = "user", Content = "Hello" }
            ]
        };
    }
}
