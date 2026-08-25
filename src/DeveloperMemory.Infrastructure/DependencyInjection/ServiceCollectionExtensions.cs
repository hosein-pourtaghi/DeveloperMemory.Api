using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Application.Services.PromptIntelligence;
using DeveloperMemory.Application.Services.Retrieval;
using DeveloperMemory.Domain.Interfaces;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Application services
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IProjectService, ProjectService>();

        // Phase 3: Retrieval pipeline
        services.AddScoped<IMemoryRetrievalProvider, KeywordRetrievalProvider>();
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

        return services;
    }
}
