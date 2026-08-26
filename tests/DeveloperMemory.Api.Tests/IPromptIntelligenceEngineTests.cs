using Xunit;
using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// In-memory stub of IPromptIntelligenceEngine for testing consumers.
/// Verifies that the abstraction can be depended upon and substituted.
/// </summary>
public class StubPromptIntelligenceEngine : IPromptIntelligenceEngine
{
    public string? LastUserRequest { get; private set; }
    public string? LastUserId { get; private set; }
    public Guid? LastProjectId { get; private set; }
    public string? LastWorkspaceId { get; private set; }
    public int LastTokenBudget { get; private set; }
    public string? LastProfileContext { get; private set; }
    public string? LastKnowledgeContext { get; private set; }
    public PromptPackage ResultToReturn { get; set; } = new()
    {
        Status = PromptIntelligenceStatus.Full,
        OptimizedPrompt = "Stub optimized prompt",
        OriginalRequest = string.Empty
    };

    public Task<PromptPackage> ProcessAsync(
        string userRequest,
        string userId,
        Guid? projectId = null,
        string? workspaceId = null,
        int contextTokenBudget = 4000,
        string? profileContext = null,
        string? knowledgeContext = null,
        CancellationToken ct = default)
    {
        LastUserRequest = userRequest;
        LastUserId = userId;
        LastProjectId = projectId;
        LastWorkspaceId = workspaceId;
        LastTokenBudget = contextTokenBudget;
        LastProfileContext = profileContext;
        LastKnowledgeContext = knowledgeContext;

        ResultToReturn.OriginalRequest = userRequest;
        return Task.FromResult(ResultToReturn);
    }

    public PromptPackage ProcessWithContext(string userRequest, PromptContext context)
    {
        LastUserRequest = userRequest;

        return new PromptPackage
        {
            OriginalRequest = userRequest,
            Status = PromptIntelligenceStatus.Full,
            OptimizedPrompt = "Stub from context"
        };
    }
}

/// <summary>
/// Behavioral tests verifying the IPromptIntelligenceEngine abstraction
/// works correctly through a stub implementation.
/// </summary>
public class IPromptIntelligenceEngineBehaviorTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsPromptPackage()
    {
        var engine = new StubPromptIntelligenceEngine();

        var result = await engine.ProcessAsync("test request", "user-1");

        Assert.NotNull(result);
        Assert.Equal(PromptIntelligenceStatus.Full, result.Status);
    }

    [Fact]
    public async Task ProcessAsync_RecordsParameters()
    {
        var engine = new StubPromptIntelligenceEngine();
        var projectId = Guid.NewGuid();

        await engine.ProcessAsync("request", "user-1", projectId, "ws-1", 8000);

        Assert.Equal("request", engine.LastUserRequest);
        Assert.Equal("user-1", engine.LastUserId);
        Assert.Equal(projectId, engine.LastProjectId);
        Assert.Equal("ws-1", engine.LastWorkspaceId);
        Assert.Equal(8000, engine.LastTokenBudget);
    }

    [Fact]
    public async Task ProcessAsync_PreservesOriginalRequest()
    {
        var engine = new StubPromptIntelligenceEngine();

        var result = await engine.ProcessAsync("my request", "user-1");

        Assert.Equal("my request", result.OriginalRequest);
    }

    [Fact]
    public async Task ProcessAsync_CanReturnCustomPackage()
    {
        var customPackage = new PromptPackage
        {
            Status = PromptIntelligenceStatus.Degraded,
            OptimizedPrompt = "Degraded prompt",
            Warnings = ["retrieval_unavailable"]
        };

        var engine = new StubPromptIntelligenceEngine
        {
            ResultToReturn = customPackage
        };

        var result = await engine.ProcessAsync("request", "user-1");

        Assert.Equal(PromptIntelligenceStatus.Degraded, result.Status);
        Assert.Contains("retrieval_unavailable", result.Warnings);
        Assert.Equal("Degraded prompt", result.OptimizedPrompt);
    }

    [Fact]
    public async Task ProcessAsync_DefaultParametersWork()
    {
        var engine = new StubPromptIntelligenceEngine();

        var result = await engine.ProcessAsync("request", "user-1");

        Assert.Null(engine.LastProjectId);
        Assert.Null(engine.LastWorkspaceId);
        Assert.Equal(4000, engine.LastTokenBudget);
    }

    [Fact]
    public void ProcessWithContext_ReturnsPromptPackage()
    {
        var engine = new StubPromptIntelligenceEngine();
        var context = new PromptContext();

        var result = engine.ProcessWithContext("test request", context);

        Assert.NotNull(result);
        Assert.Equal("test request", result.OriginalRequest);
        Assert.Equal(PromptIntelligenceStatus.Full, result.Status);
    }

    [Fact]
    public void ProcessWithContext_RecordsUserRequest()
    {
        var engine = new StubPromptIntelligenceEngine();
        var context = new PromptContext();

        engine.ProcessWithContext("my request", context);

        Assert.Equal("my request", engine.LastUserRequest);
    }
}

/// <summary>
/// Contract tests verifying IPromptIntelligenceEngine interface structure.
/// These tests verify the interface exists and has the expected shape without
/// requiring actual implementation execution.
/// </summary>
public class IPromptIntelligenceEngineContractTests
{
    [Fact]
    public void IPromptIntelligenceEngine_HasProcessAsyncMethod()
    {
        var interfaceType = typeof(IPromptIntelligenceEngine);
        var method = interfaceType.GetMethod(nameof(IPromptIntelligenceEngine.ProcessAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<PromptPackage>), method!.ReturnType);
    }

    [Fact]
    public void IPromptIntelligenceEngine_HasProcessWithContextMethod()
    {
        var interfaceType = typeof(IPromptIntelligenceEngine);
        var method = interfaceType.GetMethod(nameof(IPromptIntelligenceEngine.ProcessWithContext));

        Assert.NotNull(method);
        Assert.Equal(typeof(PromptPackage), method!.ReturnType);
    }

    [Fact]
    public void IPromptIntelligenceEngine_DoesNotExposeInfrastructureTypes()
    {
        var interfaceType = typeof(IPromptIntelligenceEngine);

        foreach (var method in interfaceType.GetMethods())
        {
            foreach (var param in method.GetParameters())
            {
                Assert.False(param.ParameterType.Name.Contains("DbContext"),
                    $"Parameter {param.Name} should not expose DbContext");
                Assert.False(param.ParameterType.Name.Contains("HttpClient"),
                    $"Parameter {param.Name} should not expose HttpClient");
                Assert.False(param.ParameterType.Name.Contains("HttpResponseMessage"),
                    $"Parameter {param.Name} should not expose HttpResponseMessage");
                Assert.False(param.ParameterType.Name.Contains("Npgsql"),
                    $"Parameter {param.Name} should not expose Npgsql types");
            }
        }
    }

    [Fact]
    public void PromptIntelligenceEngine_ImplementsIPromptIntelligenceEngine()
    {
        var engineType = typeof(Services.PromptIntelligence.PromptIntelligenceEngine);
        var interfaceType = typeof(IPromptIntelligenceEngine);

        Assert.True(interfaceType.IsAssignableFrom(engineType),
            "PromptIntelligenceEngine should implement IPromptIntelligenceEngine");
    }

    [Fact]
    public void StubPromptIntelligenceEngine_ImplementsIPromptIntelligenceEngine()
    {
        var stubType = typeof(StubPromptIntelligenceEngine);
        var interfaceType = typeof(IPromptIntelligenceEngine);

        Assert.True(interfaceType.IsAssignableFrom(stubType),
            "StubPromptIntelligenceEngine should implement IPromptIntelligenceEngine");
    }

    [Fact]
    public void PromptPackage_HasExpectedProperties()
    {
        var packageType = typeof(PromptPackage);

        Assert.NotNull(packageType.GetProperty(nameof(PromptPackage.OriginalRequest)));
        Assert.NotNull(packageType.GetProperty(nameof(PromptPackage.OptimizedPrompt)));
        Assert.NotNull(packageType.GetProperty(nameof(PromptPackage.Status)));
        Assert.NotNull(packageType.GetProperty(nameof(PromptPackage.Warnings)));
        Assert.NotNull(packageType.GetProperty(nameof(PromptPackage.Analysis)));
    }

    [Fact]
    public void PromptPackage_Status_DefaultsToFull()
    {
        var package = new PromptPackage();
        Assert.Equal(PromptIntelligenceStatus.Full, package.Status);
    }

    [Fact]
    public void PromptPackage_Warnings_DefaultsToEmpty()
    {
        var package = new PromptPackage();
        Assert.NotNull(package.Warnings);
        Assert.Empty(package.Warnings);
    }

    [Fact]
    public void PromptPackage_OriginalRequestPreserved_DefaultsToTrue()
    {
        var package = new PromptPackage();
        Assert.True(package.OriginalRequestPreserved);
    }
}
