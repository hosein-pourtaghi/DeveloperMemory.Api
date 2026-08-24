using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
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

        return services;
    }
}
