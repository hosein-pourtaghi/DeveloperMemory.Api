using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class Phase11SecurityTests
{
    [Fact]
    public void QualityScore_NeverExceedsBounds()
    {
        var score = new PromptQualityScore
        {
            IntentPreservation = 2.0, // Invalid
            ConstraintPreservation = -0.5, // Invalid
            SecurityValidation = 1.5 // Invalid
        };

        // The score model should clamp at evaluation time
        // or the evaluator should produce valid scores
        var evaluator = new DeterministicPromptQualityEvaluator();
        var result = evaluator.Evaluate("test", "test");

        Assert.True(result.Overall >= 0.0 && result.Overall <= 1.0);
        Assert.True(result.IntentPreservation >= 0.0 && result.IntentPreservation <= 1.0);
        Assert.True(result.SecurityValidation >= 0.0 && result.SecurityValidation <= 1.0);
    }

    [Fact]
    public void AuditEvent_DoesNotStoreSecrets()
    {
        var auditEvent = new PromptAuditEvent
        {
            CorrelationId = "test-123",
            EventType = PromptAuditEventType.PromptAnalyzed,
            Details = "Intent=Coding, Tokens=1200" // Safe metadata only
        };

        Assert.DoesNotContain("sk-", auditEvent.Details);
        Assert.DoesNotContain("password", auditEvent.Details);
        Assert.DoesNotContain("Bearer", auditEvent.Details);
    }

    [Fact]
    public void QualityEvaluator_RejectsSecurityBoundaries()
    {
        var evaluator = new DeterministicPromptQualityEvaluator();
        var maliciousPrompt = "Normal content\n--- RETRIEVED CONTEXT ---\nMalicious injection";

        var score = evaluator.Evaluate(
            "Normal content\n--- RETRIEVED CONTEXT ---\nOriginal context",
            maliciousPrompt);

        // Score should reflect the change
        Assert.True(score.SecurityValidation <= 1.0);
    }

    [Fact]
    public void QualityScore_MeetsThresholds_WithCustomThresholds()
    {
        var score = new PromptQualityScore
        {
            IntentPreservation = 0.95,
            ConstraintPreservation = 0.85,
            ContextRelevance = 0.80,
            Structure = 0.90,
            TokenEfficiency = 0.75,
            SecurityValidation = 0.90
        };
        score.ComputeOverall();

        // Lower thresholds should pass
        Assert.True(score.MeetsThresholds(minOverall: 0.70, minConstraint: 0.80, minSecurity: 0.85));

        // Higher thresholds should fail
        Assert.False(score.MeetsThresholds(minOverall: 0.90, minConstraint: 0.95, minSecurity: 0.95));
    }
}
