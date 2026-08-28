using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Domain.Enums;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeveloperMemory.Infrastructure.Persistence;

public sealed class ConfiguredRetrievalProviderResolver : IRetrievalProviderResolver
{
    private readonly KeywordRetrievalProvider _keyword;
    private readonly SemanticRetrievalProvider _semantic;
    private readonly HybridRetrievalProvider _hybrid;
    private readonly ILogger<ConfiguredRetrievalProviderResolver> _logger;

    public ConfiguredRetrievalProviderResolver(
        KeywordRetrievalProvider keyword,
        SemanticRetrievalProvider semantic,
        HybridRetrievalProvider hybrid,
        ILogger<ConfiguredRetrievalProviderResolver> logger)
    {
        _keyword = keyword;
        _semantic = semantic;
        _hybrid = hybrid;
        _logger = logger;
    }

    public IMemoryRetrievalProvider Resolve(RetrievalMode mode)
    {
        var requested = mode == RetrievalMode.Auto
            ? (_semantic.IsAvailable ? RetrievalMode.Hybrid : RetrievalMode.Lexical)
            : mode;

        IMemoryRetrievalProvider provider = requested switch
        {
            RetrievalMode.Semantic => _semantic,
            RetrievalMode.Hybrid => _hybrid,
            _ => _keyword
        };

        if (!provider.IsAvailable)
        {
            _logger.LogDebug("Retrieval provider {Provider} unavailable; using keyword fallback", provider.ProviderName);
            return _keyword;
        }

        return provider;
    }
}
