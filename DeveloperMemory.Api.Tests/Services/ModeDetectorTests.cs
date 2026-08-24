using DeveloperMemory.Api.Models;
using DeveloperMemory.Api.Services;
using Xunit;

namespace DeveloperMemory.Api.Tests.Services;

public class ModeDetectorTests
{
    private static OpenAIChatCompletionRequest CreateRequest(string? systemContent)
    {
        var request = new OpenAIChatCompletionRequest
        {
            Messages = new List<Message>()
        };

        if (systemContent != null)
        {
            request.Messages.Add(new Message { Role = "system", Content = systemContent });
        }

        return request;
    }

    [Fact]
    public void DetectMode_NullRequest_ReturnsUnknown()
    {
        var result = ModeDetector.DetectMode(null!);
        Assert.Equal(ModeDetector.TaskMode.Unknown, result);
    }

    [Fact]
    public void DetectMode_NullMessages_ReturnsUnknown()
    {
        var request = new OpenAIChatCompletionRequest { Messages = null! };
        var result = ModeDetector.DetectMode(request);
        Assert.Equal(ModeDetector.TaskMode.Unknown, result);
    }

    [Fact]
    public void DetectMode_NoSystemMessage_ReturnsUnknown()
    {
        var request = CreateRequest(null);
        request.Messages.Add(new Message { Role = "user", Content = "Hello" });
        var result = ModeDetector.DetectMode(request);
        Assert.Equal(ModeDetector.TaskMode.Unknown, result);
    }

    [Fact]
    public void DetectMode_EmptySystemMessage_ReturnsUnknown()
    {
        var result = ModeDetector.DetectMode(CreateRequest(""));
        Assert.Equal(ModeDetector.TaskMode.Unknown, result);
    }

    [Fact]
    public void DetectMode_BuildIndicators_ReturnsBuild()
    {
        var content = "You are an assistant. Use execute_command and write_to_file to complete tasks.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Build, result);
    }

    [Fact]
    public void DetectMode_PlanIndicators_ReturnsPlan()
    {
        var content = "You are an assistant. Create a ## Plan for the task.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Plan, result);
    }

    [Fact]
    public void DetectMode_BothIndicators_ReturnsBuild()
    {
        // When both plan and build indicators are present, default to Build
        var content = "You are an assistant. Use execute_command. Create a ## Plan for the task.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Build, result);
    }

    [Fact]
    public void DetectMode_UnrelatedContent_ReturnsUnknown()
    {
        var content = "You are a helpful coding assistant. Write good code.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Unknown, result);
    }

    [Fact]
    public void DetectMode_ChecklistIndicator_ReturnsPlan()
    {
        var content = "You are an assistant. Checklist: 1. Analyze 2. Plan 3. Implement";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Plan, result);
    }

    [Fact]
    public void DetectMode_ReplaceInFile_ReturnsBuild()
    {
        var content = "Use replace_in_file to make changes.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Build, result);
    }

    [Fact]
    public void DetectMode_GoalIndicator_ReturnsPlan()
    {
        var content = "Goal: Design the architecture for the new module.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Plan, result);
    }

    [Fact]
    public void DetectMode_TaskProgressIndicator_ReturnsPlan()
    {
        var content = "Current task_progress: analyzing requirements.";
        var result = ModeDetector.DetectMode(CreateRequest(content));
        Assert.Equal(ModeDetector.TaskMode.Plan, result);
    }
}
