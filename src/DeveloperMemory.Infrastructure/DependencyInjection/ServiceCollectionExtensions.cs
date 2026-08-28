using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Infrastructure.Configuration;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeveloperMemory.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeveloperMemoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core DbContext
        var useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");

        if (useInMemory)
        {
            services.AddDbContext<DeveloperMemoryDbContext>(options =>
                options.UseInMemoryDatabase("DeveloperMemory_InMemory"));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found. " +
                    "Set it in appsettings.json or via environment variable.");

            services.AddDbContext<DeveloperMemoryDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(DeveloperMemoryDbContext).Assembly.FullName);
                }));
        }

        // Repositories
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        // Application services
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IPromptProcessingHistoryService, PromptProcessingHistoryService>();

        // Phase 3: Retrieval pipeline
        services.AddScoped<KeywordRetrievalProvider>();
        services.AddScoped<IRetrievalRanker, RelevanceRanker>();
        services.AddScoped<IContextBudgeter, CharacterContextBudgeter>();
        services.AddScoped<IMemoryRetrievalService, MemoryRetrievalService>();

        // Phase 4: Prompt Intelligence Engine
        services.AddScoped<IPromptAnalyzer, DeterministicPromptAnalyzer>();
        services.AddScoped<IConstraintResolver, ConstraintResolver>();
        services.AddScoped<IMemoryContextAssembler, MemoryContextAssembler>();
        services.AddScoped<IPromptComposer, DeterministicPromptComposer>();
        services.AddScoped<IPromptOptimizer, DeterministicPromptOptimizer>();
        services.AddScoped<IPromptIntelligenceEngine, PromptIntelligenceEngine>();

        // Phase 5: Memory Intelligence
        services.AddScoped<IMemoryIngestionService, MemoryIngestionService>();
        services.AddScoped<IMemoryConflictDetector, MemoryConflictDetector>();
        services.AddScoped<IMemoryRanker, MemoryRanker>();
        services.AddScoped<IMemoryExtractionStrategy, DeterministicExtractionStrategy>();

        // Phase 6 & 7: Semantic Memory Layer
        // Configuration-based provider selection
        var embeddingOptions = new EmbeddingOptions();
        configuration.GetSection(EmbeddingOptions.SectionName).Bind(embeddingOptions);
        services.Configure<EmbeddingOptions>(configuration.GetSection(EmbeddingOptions.SectionName));

        // Embedding provider — in-memory for testing, OpenAI-compatible for production
        if (embeddingOptions.Enabled && !string.IsNullOrEmpty(embeddingOptions.BaseUrl))
        {
            services.AddHttpClient<IEmbeddingProvider, Persistence.OpenAICompatibleEmbeddingProvider>(client =>
            {
                client.BaseAddress = new Uri(embeddingOptions.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(embeddingOptions.TimeoutSeconds);
            });
        }
        else
        {
            services.AddSingleton<IEmbeddingProvider, InMemoryEmbeddingProvider>();
        }

        // Vector store — in-memory for testing, PostgreSQL/pgvector for production
        if (!useInMemory && embeddingOptions.Enabled)
        {
            services.AddScoped<IVectorStore, Persistence.PostgresVectorStore>();
        }
        else
        {
            services.AddSingleton<IVectorStore, InMemoryVectorStore>();
        }

        // Embedding services
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IEmbeddingRebuildService, EmbeddingRebuildService>();

        // Embedding cache
        if (embeddingOptions.CacheEnabled)
        {
            services.AddSingleton<IEmbeddingCache, InMemoryEmbeddingCache>();
        }

        // Semantic/hybrid retrieval providers
        services.AddScoped<SemanticRetrievalProvider>();
        services.AddScoped<HybridRetrievalProvider>();
        services.AddScoped<IRetrievalProviderResolver, ConfiguredRetrievalProviderResolver>();
        services.AddScoped<IMemoryRetrievalProvider>(sp => sp.GetRequiredService<KeywordRetrievalProvider>());

        // Phase 8: LLM-Assisted Memory Intelligence
        var memoryIntelligenceOptions = new MemoryIntelligenceOptions();
        configuration.GetSection(MemoryIntelligenceOptions.SectionName).Bind(memoryIntelligenceOptions);
        services.Configure<MemoryIntelligenceOptions>(configuration.GetSection(MemoryIntelligenceOptions.SectionName));

        // Memory policy engine
        services.AddScoped<IMemoryPolicy, MemoryPolicyEngine>();

        // Deterministic extraction (always available)
        services.AddScoped<DeterministicExtractionStrategy>();

        // LLM extraction (optional, requires configuration)
        if (memoryIntelligenceOptions.IsAvailable)
        {
            services.AddHttpClient("MemoryExtraction", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(memoryIntelligenceOptions.ExtractionTimeoutSeconds);
            });
            services.AddScoped<LlmMemoryExtractionStrategy>();
        }
        else
        {
            services.AddSingleton<LlmMemoryExtractionStrategy>(sp => null!);
        }

        // Extraction orchestrator
        services.AddScoped<IExtractionOrchestrator, ExtractionOrchestrator>();

        // LLM conflict detection (wraps deterministic)
        services.AddScoped<MemoryConflictDetector>();
        services.AddScoped<IMemoryConflictDetector>(sp =>
        {
            var deterministic = sp.GetRequiredService<MemoryConflictDetector>();
            if (memoryIntelligenceOptions.IsAvailable)
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MemoryIntelligenceOptions>>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LlmConflictDetector>>();
                return new LlmConflictDetector(deterministic, httpClientFactory, options, logger);
            }
            return deterministic;
        });

        // Phase 9: Prompt Intelligence & Context Orchestration
        services.AddScoped<DeterministicIntentAnalyzer>();
        services.AddScoped<IProjectContextProvider, ProjectContextProvider>();
        services.AddScoped<IContextOrchestrator, ContextOrchestrator>();
        services.AddScoped<PromptConstructionEngine>();
        services.AddScoped<DeterministicPromptOptimizer>();

        // Phase 10: Hybrid Prompt Intelligence
        // Intent analysis — deterministic always available, LLM optional
        services.AddScoped<IIntentResolver, IntentResolver>();
        services.AddSingleton<IntentResolutionPolicy>();

        if (memoryIntelligenceOptions.IsAvailable)
        {
            services.AddScoped<IIntentAnalyzer>(sp =>
            {
                var deterministic = sp.GetRequiredService<DeterministicIntentAnalyzer>();
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MemoryIntelligenceOptions>>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LlmIntentAnalyzer>>();
                var llm = new LlmIntentAnalyzer(httpClientFactory, options, logger);
                var resolver = sp.GetRequiredService<IIntentResolver>();
                var hybridLogger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DeveloperMemory.Application.Services.HybridIntentAnalyzer>>();
                return new HybridIntentAnalyzer(deterministic, llm, resolver, hybridLogger);
            });
        }
        else
        {
            services.AddScoped<IIntentAnalyzer, DeterministicIntentAnalyzer>();
        }

        // LLM prompt optimizer (optional)
        services.AddScoped<LlmPromptOptimizer>();
        services.AddScoped<PromptValidator>();

        // Prompt profiles
        services.AddSingleton<IPromptProfileProvider, PromptProfileProvider>();

        // Phase 11: Persistent Prompt Intelligence
        var promptIntelligencePersistence = configuration.GetValue<bool>("PromptIntelligence:PersistenceEnabled", true);

        if (promptIntelligencePersistence && !useInMemory)
        {
            services.AddScoped<IPromptProfileProvider, PromptProfileRepository>();
            services.AddScoped<IPromptIntelligenceAudit, PromptIntelligenceAudit>();
            services.AddScoped<IPromptHistoryRetentionService, PromptHistoryRetentionService>();
            services.AddScoped<PromptProcessingRecordRepository>();
            services.AddScoped<IPromptQualityEvaluator, DeterministicPromptQualityEvaluator>();
        }
        else
        {
            // Fallback: in-memory audit for testing
            services.AddSingleton<IPromptIntelligenceAudit, InMemoryPromptAudit>();
            services.AddSingleton<IPromptQualityEvaluator, DeterministicPromptQualityEvaluator>();
            services.AddScoped<PromptProcessingRecordRepository>();
        }

        // Phase 12: Prompt Intelligence Evaluation, Experimentation & Observability
        var promptIntelligenceOptions = new Configuration.PromptIntelligenceOptions();
        configuration.GetSection(Configuration.PromptIntelligenceOptions.SectionName).Bind(promptIntelligenceOptions);
        services.Configure<Configuration.PromptIntelligenceOptions>(
            configuration.GetSection(Configuration.PromptIntelligenceOptions.SectionName));

        // Quality evaluation pipeline
        services.AddScoped<HybridQualityEvaluationPipeline>();
        services.AddScoped<IPromptCandidateSelector, PromptCandidateSelector>();

        // LLM quality evaluator (optional)
        if (promptIntelligenceOptions.LlmEvaluation.IsAvailable)
        {
            services.AddHttpClient("LlmEvaluation", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(promptIntelligenceOptions.LlmEvaluation.TimeoutSeconds);
            });
            services.AddScoped<ILlmPromptQualityEvaluator, LlmPromptQualityEvaluator>();
        }
        else
        {
            services.AddSingleton<ILlmPromptQualityEvaluator>(sp => null!);
        }

        // Experiment service
        services.AddScoped<IExperimentService, ExperimentService>();

        // Metrics
        services.AddSingleton<IPromptIntelligenceMetrics, InMemoryPromptMetrics>();

        // Background history retention worker (only when persistence is available)
        if (promptIntelligencePersistence && !useInMemory && promptIntelligenceOptions.HistoryRetention.Enabled)
        {
            services.AddHostedService<PromptHistoryRetentionWorker>();
        }

        return services;
    }
}
