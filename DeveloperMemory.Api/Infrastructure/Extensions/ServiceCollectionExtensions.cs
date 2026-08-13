using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeveloperMemory.Api.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        
        services.AddHttpClient<FreeLlmApiClient>();
        
        return services;
    }
}