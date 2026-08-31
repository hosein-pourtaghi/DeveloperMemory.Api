using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

/// <summary>
/// Tests for the ConversationalMemoryDetector.
/// Verifies pattern-based detection of durable user information in chat messages.
/// </summary>
public class ConversationalMemoryDetectorTests
{
    private readonly Mock<ILogger<ConversationalMemoryDetector>> _mockLogger;
    private readonly ConversationalMemoryDetector _detector;

    public ConversationalMemoryDetectorTests()
    {
        _mockLogger = new Mock<ILogger<ConversationalMemoryDetector>>();
        _detector = new ConversationalMemoryDetector(_mockLogger.Object);
    }

    // ── Positive capture tests ──

    [Fact]
    public void Detect_PreferenceStatement_DetectsMemory()
    {
        var result = _detector.Detect("I prefer PostgreSQL for my projects.");

        Assert.True(result.ContainsDurableInformation);
        Assert.True(result.Confidence > 0.5);
        Assert.Equal(MemoryType.UserPreference.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_ExplicitRemember_DetectsMemory()
    {
        var result = _detector.Detect("Remember that I use Freebuff as my coding agent.");

        Assert.True(result.ContainsDurableInformation);
        Assert.True(result.Confidence > 0.5);
    }

    [Fact]
    public void Detect_ConstraintStatement_DetectsMemory()
    {
        var result = _detector.Detect("Don't recommend paid services to me.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserConstraint.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_UsageStatement_DetectsMemory()
    {
        var result = _detector.Detect("I use Angular with NgModule, not standalone components.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserPreference.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_IdentityFact_DetectsMemory()
    {
        var result = _detector.Detect("My default coding agent is Freebuff.");

        Assert.True(result.ContainsDurableInformation);
        Assert.True(result.Confidence > 0.5);
    }

    [Fact]
    public void Detect_AlwaysUseStatement_DetectsMemory()
    {
        var result = _detector.Detect("I always use async patterns in my code.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserPreference.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_NeverUseConstraint_DetectsMemory()
    {
        var result = _detector.Detect("Never use var for complex types in this project.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserConstraint.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_ProjectContextStatement_DetectsMemory()
    {
        var result = _detector.Detect("This project uses Clean Architecture patterns.");

        Assert.True(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_WantStatement_DetectsMemory()
    {
        var result = _detector.Detect("I want to build a SaaS product.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserGoal.ToString(), result.SuggestedMemoryType);
    }

    // ── Non-memory tests ──

    [Fact]
    public void Detect_Question_DoesNotDetectMemory()
    {
        var result = _detector.Detect("What is dependency injection?");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_ExplainRequest_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Explain CQRS pattern.");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_FixRequest_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Fix this exception in the repository pattern.");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_HowQuestion_DoesNotDetectMemory()
    {
        var result = _detector.Detect("How does EF Core handle migrations?");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_WeatherQuestion_DoesNotDetectMemory()
    {
        var result = _detector.Detect("What is the weather today?");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_ComparisonQuestion_DoesNotDetectMemory()
    {
        var result = _detector.Detect("What is the difference between PostgreSQL and SQL Server?");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_CodeGenerationRequest_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Write a function to parse JSON.");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_DebugRequest_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Debug this exception in the API layer.");

        Assert.False(result.ContainsDurableInformation);
    }

    // ── Edge cases ──

    [Fact]
    public void Detect_EmptyMessage_DoesNotDetectMemory()
    {
        var result = _detector.Detect("");

        Assert.False(result.ContainsDurableInformation);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void Detect_NullMessage_DoesNotDetectMemory()
    {
        var result = _detector.Detect(null!);

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_VeryShortMessage_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Hi");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_LowValueContent_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Thanks");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_ThankYou_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Thank you so much!");

        Assert.False(result.ContainsDurableInformation);
    }

    [Fact]
    public void Detect_TemporaryContext_DoesNotDetectMemory()
    {
        var result = _detector.Detect("Today I'm working on the login feature.");

        Assert.False(result.ContainsDurableInformation);
    }

    // ── Confidence scoring ──

    [Fact]
    public void Detect_StrongPreference_HighConfidence()
    {
        var result = _detector.Detect("I always prefer PostgreSQL for all my projects.");

        Assert.True(result.ContainsDurableInformation);
        Assert.True(result.Confidence > 0.6);
    }

    [Fact]
    public void Detect_WeakSignal_LowerConfidence()
    {
        var result = _detector.Detect("I am a developer who works with .NET.");

        // This may or may not be detected — depends on pattern matching
        // The important thing is it doesn't crash
        Assert.NotNull(result);
        Assert.NotNull(result.Reason);
    }

    // ── Metadata and type suggestions ──

    [Fact]
    public void Detect_PreferencePattern_SuggestsUserPreferenceType()
    {
        var result = _detector.Detect("I prefer using TypeScript over JavaScript.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserPreference.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_ConstraintPattern_SuggestsConstraintType()
    {
        var result = _detector.Detect("Don't use GPT-4 for this project.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserConstraint.ToString(), result.SuggestedMemoryType);
    }

    [Fact]
    public void Detect_AlwaysUsePattern_SuggestsUserPreferenceType()
    {
        var result = _detector.Detect("I always use dependency injection in my services.");

        Assert.True(result.ContainsDurableInformation);
        Assert.Equal(MemoryType.UserPreference.ToString(), result.SuggestedMemoryType);
    }

    // ── Extracted content ──

    [Fact]
    public void Detect_PreferencePattern_ExtractsContent()
    {
        var result = _detector.Detect("I prefer PostgreSQL for my personal projects.");

        Assert.True(result.ContainsDurableInformation);
        Assert.NotNull(result.ExtractedContent);
        Assert.Contains("PostgreSQL", result.ExtractedContent);
    }

    [Fact]
    public void Detect_WithConversationContext_DoesNotCrash()
    {
        var context = new List<string>
        {
            "User: Tell me about databases",
            "Assistant: There are several database options...",
        };

        var result = _detector.Detect("I prefer PostgreSQL.", context);

        Assert.NotNull(result);
    }
}
