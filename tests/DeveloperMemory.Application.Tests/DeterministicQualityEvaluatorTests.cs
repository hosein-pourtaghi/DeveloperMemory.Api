using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public class DeterministicQualityEvaluatorTests
{
    private readonly IPromptQualityEvaluator _evaluator = new DeterministicPromptQualityEvaluator();

    [Fact]
    public void Evaluate_OptimizedPrompt_ScoresHigherThanEmpty()
    {
        var original = "Fix the database connection";
        var optimized = "--- SYSTEM INSTRUCTIONS ---\nFix the database\n--- RETRIEVED CONTEXT ---\nMemory\n--- USER REQUEST ---\nFix the database connection\n---";

        var score = _evaluator.Evaluate(original, optimized);

        Assert.True(score.Overall > 0.5);
    }

    [Fact]
    public void Evaluate_ConstraintPreservation_WithConstraints()
    {
        var intent = new IntentAnalysisResult
        {
            ExplicitConstraints = ["PostgreSQL", "EF Core"]
        };

        var optimized = "Use PostgreSQL with EF Core for database access";

        var score = _evaluator.Evaluate("test", optimized, intent);

        Assert.True(score.ConstraintPreservation >= 0.9);
    }

    [Fact]
    public void Evaluate_ConstraintViolation_LowScore()
    {
        var intent = new IntentAnalysisResult
        {
            ExplicitConstraints = ["PostgreSQL"]
        };

        var optimized = "Use MySQL for database";

        var score = _evaluator.Evaluate("test", optimized, intent);

        Assert.True(score.ConstraintPreservation < 0.5);
    }

    [Fact]
    public void Evaluate_TokenOveruse_LowEfficiency()
    {
        var score = _evaluator.Evaluate("short", new string('x', 20000), tokenBudget: 100);

        Assert.True(score.TokenEfficiency < 0.5);
    }

    [Fact]
    public void Evaluate_SecurityWithBoundaries_HighScore()
    {
        var optimized = "--- RETRIEVED CONTEXT ---\nMemory content\n--- USER REQUEST ---\nTask";

        var score = _evaluator.Evaluate("test", optimized);

        Assert.True(score.SecurityValidation >= 0.8);
    }
}
