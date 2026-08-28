using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

public sealed class RetrievalActivationTests
{
    [Theory]
    [InlineData(RetrievalMode.Lexical, "keyword")]
    [InlineData(RetrievalMode.Semantic, "semantic")]
    [InlineData(RetrievalMode.Hybrid, "hybrid")]
    public void Resolver_SelectsRequestedAvailableProvider(RetrievalMode mode, string expected)
    {
        Assert.Equal(expected, ResolveWithFakes(mode, semanticAvailable: true).ProviderName);
    }

    [Fact]
    public void Resolver_AutoSelectsHybridWhenSemanticAvailable()
    {
        Assert.Equal("hybrid", ResolveWithFakes(RetrievalMode.Auto, semanticAvailable: true).ProviderName);
    }

    [Theory]
    [InlineData(RetrievalMode.Auto)]
    [InlineData(RetrievalMode.Semantic)]
    [InlineData(RetrievalMode.Hybrid)]
    public void Resolver_FallsBackToKeywordWhenSemanticUnavailable(RetrievalMode mode)
    {
        Assert.Equal("keyword", ResolveWithFakes(mode, semanticAvailable: false).ProviderName);
    }

    [Fact]
    public void CandidateMerge_DeduplicatesAndPreservesSemanticScore()
    {
        var id = Guid.NewGuid();
        var lexical = new[] { new RetrievalCandidate { Memory = Memory(id) } };
        var semantic = new[] { new RetrievalCandidate { Memory = Memory(id), SemanticScore = 0.95 },
            new RetrievalCandidate { Memory = Memory(Guid.NewGuid()), SemanticScore = 0.4 } };

        var merged = MergeCandidates(lexical, semantic);

        Assert.Equal(2, merged.Count);
        Assert.Equal(0.95, merged.Single(c => c.Memory.Id == id).SemanticScore);
    }

    private static List<RetrievalCandidate> MergeCandidates(
        IEnumerable<RetrievalCandidate> lexical,
        IEnumerable<RetrievalCandidate> semantic)
    {
        var byId = new Dictionary<Guid, RetrievalCandidate>();
        foreach (var candidate in lexical) byId.TryAdd(candidate.Memory.Id, candidate);
        foreach (var candidate in semantic)
        {
            if (byId.TryGetValue(candidate.Memory.Id, out var existing))
            {
                existing.SemanticScore = candidate.SemanticScore ?? existing.SemanticScore;
            }
            else
            {
                byId.Add(candidate.Memory.Id, candidate);
            }
        }
        return byId.Values.ToList();
    }

    private static IMemoryRetrievalProvider ResolveWithFakes(RetrievalMode mode, bool semanticAvailable)
    {
        var keyword = new StubProvider("keyword", true);
        var semantic = new StubProvider("semantic", semanticAvailable);
        var hybrid = new StubProvider("hybrid", semanticAvailable);
        var requested = mode == RetrievalMode.Auto
            ? (semanticAvailable ? hybrid : keyword)
            : mode switch
            {
                RetrievalMode.Semantic when semanticAvailable => semantic,
                RetrievalMode.Hybrid when semanticAvailable => hybrid,
                _ => keyword
            };
        return requested;
    }

    private static MemoryEntry Memory(Guid id) => new()
    {
        Id = id, Title = "Test", Content = "content", OwnerId = "owner", State = MemoryState.Active
    };

    private sealed class StubProvider(string name, bool available) : IMemoryRetrievalProvider
    {
        public string ProviderName => name;
        public bool IsAvailable => available;
        public Task<List<MemoryEntry>> GetCandidatesAsync(RetrievalRequest request, CancellationToken ct = default) => Task.FromResult(new List<MemoryEntry>());
    }
}
