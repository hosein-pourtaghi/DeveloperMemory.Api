using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

public class PromptHistoryRetentionWorkerTests
{
    [Fact]
    public void Worker_Constructor_SetsDependencies()
    {
        var retentionService = new Mock<IPromptHistoryRetentionService>();
        var options = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<Infrastructure.Configuration.PromptIntelligenceOptions>>();
        var logger = new Mock<ILogger<PromptHistoryRetentionWorker>>();

        var services = new ServiceCollection();
        services.AddScoped(_ => retentionService.Object);
        using var provider = services.BuildServiceProvider();

        var worker = new PromptHistoryRetentionWorker(provider.GetRequiredService<IServiceScopeFactory>(), options.Object, logger.Object);

        Assert.NotNull(worker);
    }
}
