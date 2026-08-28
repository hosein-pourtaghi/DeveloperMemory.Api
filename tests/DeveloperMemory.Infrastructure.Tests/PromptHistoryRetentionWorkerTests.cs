using DeveloperMemory.Application.Contracts;
using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Configuration;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

public class PromptHistoryRetentionWorkerTests
{
    [Fact]
    public void Worker_Constructor_SetsDependencies()
    {
        var scopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var options = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<Infrastructure.Configuration.PromptIntelligenceOptions>>();
        var logger = new Mock<ILogger<PromptHistoryRetentionWorker>>();

        var worker = new PromptHistoryRetentionWorker(scopeFactory.Object, options.Object, logger.Object);

        Assert.NotNull(worker);
    }
}
