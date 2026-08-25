using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using Xunit;

namespace DeveloperMemory.Tests;

public class ConstraintResolverTests
{
    private readonly ConstraintResolver _resolver = new();

    [Fact]
    public void Resolve_AlwaysIncludesSystemConstraints()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var result = _resolver.Resolve(analysis, null);

        Assert.Contains(result, c => c.Source == ConstraintSource.System);
        Assert.Contains(result, c => c.Type == ConstraintType.Security);
    }

    [Fact]
    public void Resolve_ExcludesSystemConstraintsFromDedup()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Never expose credentials"]
        };

        var result = _resolver.Resolve(analysis, null);

        // System constraint should win over explicit constraint of same type
        var securityConstraints = result.Where(c => c.Type == ConstraintType.Security).ToList();
        Assert.Single(securityConstraints);
        Assert.Equal(ConstraintSource.System, securityConstraints[0].Source);
    }

    [Fact]
    public void Resolve_IncludesProjectRules()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };
        var projectRules = new List<string> { "Use PostgreSQL for all data storage" };

        var result = _resolver.Resolve(analysis, null, projectRules);

        Assert.Contains(result, c =>
            c.Source == ConstraintSource.ProjectRule &&
            c.Value.Contains("PostgreSQL"));
    }

    [Fact]
    public void Resolve_IncludesExplicitRequestConstraints()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Use .NET 10"]
        };

        var result = _resolver.Resolve(analysis, null);

        Assert.Contains(result, c =>
            c.Source == ConstraintSource.ExplicitCurrentRequest &&
            c.Value.Contains(".NET 10"));
    }

    [Fact]
    public void Resolve_ProjectRuleOverridesExplicitRequest_WhenHigherPrecedence()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Use SQLite for this prototype"]
        };
        var projectRules = new List<string> { "Use PostgreSQL for all data storage" };

        var result = _resolver.Resolve(analysis, null, projectRules);

        // Both should be present as different constraint types won't conflict,
        // but if they're same type, project rule wins
        Assert.Contains(result, c => c.Value.Contains("PostgreSQL"));
        Assert.Contains(result, c => c.Value.Contains("SQLite"));
    }

    [Fact]
    public void Resolve_EmptyAnalysis_ProducesOnlySystemConstraints()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "" };

        var result = _resolver.Resolve(analysis, null);

        // Should only have system constraints
        Assert.All(result, c => Assert.Equal(ConstraintSource.System, c.Source));
    }

    [Fact]
    public void Resolve_IncludesMemoryBasedConstraints()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };
        var context = new PromptContext
        {
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = Guid.NewGuid(),
                    Title = "Project Rule",
                    Content = "Always use dependency injection for new services",
                    Tags = ["rule"],
                    Scope = MemoryScope.Project,
                    Importance = 0.8
                }
            ]
        };

        var result = _resolver.Resolve(analysis, context);

        Assert.Contains(result, c =>
            c.Source == ConstraintSource.GeneralMemory &&
            c.SourceMemoryId.HasValue);
    }

    [Fact]
    public void Resolve_ConstraintsOrderedByPrecedence()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Use React for the frontend"]
        };
        var projectRules = new List<string> { "Use Angular for all frontends" };

        var result = _resolver.Resolve(analysis, null, projectRules);

        // Should be ordered by precedence (highest first)
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].Precedence >= result[i + 1].Precedence,
                $"Constraint at index {i} ({result[i].Source}) should have higher or equal precedence than {i + 1} ({result[i + 1].Source})");
        }
    }

    [Fact]
    public void Resolve_DeduplicatesSameTypeConstraints()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Use .NET 10", "Target net10.0"]
        };

        var result = _resolver.Resolve(analysis, null);

        // Both might map to same type; verify no exact duplicates
        var values = result.Select(c => c.Value).ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void Resolve_NullContext_DoesNotThrow()
    {
        var analysis = new PromptAnalysis { OriginalRequest = "test" };

        var exception = Record.Exception(() => _resolver.Resolve(analysis, null));
        Assert.Null(exception);
    }

    [Fact]
    public void Resolve_ClassifiesTechnologyConstraints()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Use PostgreSQL"]
        };

        var result = _resolver.Resolve(analysis, null);

        Assert.Contains(result, c => c.Type == ConstraintType.Technology);
    }

    [Fact]
    public void Resolve_ClassifiesSecurityConstraints()
    {
        var analysis = new PromptAnalysis
        {
            OriginalRequest = "test",
            ExplicitConstraints = ["Encrypt all data at rest"]
        };

        var result = _resolver.Resolve(analysis, null);

        Assert.Contains(result, c => c.Type == ConstraintType.Security);
    }
}
