using DeveloperMemory.Application.Services;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeveloperMemory.Application.Tests;

public sealed class PromptProcessingHistoryServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsOnlyMatchingOwner()
    {
        await using var context = CreateContext();
        var ownerA = new PromptProcessingRecord { Id = Guid.NewGuid(), UserId = "owner-a", CorrelationId = "a" };
        var ownerB = new PromptProcessingRecord { Id = Guid.NewGuid(), UserId = "owner-b", CorrelationId = "b" };
        context.PromptProcessingRecords.AddRange(ownerA, ownerB);
        await context.SaveChangesAsync();
        var service = new PromptProcessingHistoryService(new PromptProcessingRecordRepository(context, NullLogger<PromptProcessingRecordRepository>.Instance));

        Assert.NotNull(await service.GetByIdAsync(ownerA.Id, "owner-a"));
        Assert.Null(await service.GetByIdAsync(ownerB.Id, "owner-a"));
    }

    [Fact]
    public async Task MissingOwner_FailsClosed()
    {
        await using var context = CreateContext();
        var record = new PromptProcessingRecord { Id = Guid.NewGuid(), UserId = "owner-a" };
        context.PromptProcessingRecords.Add(record);
        await context.SaveChangesAsync();
        var service = new PromptProcessingHistoryService(new PromptProcessingRecordRepository(context, NullLogger<PromptProcessingRecordRepository>.Instance));

        Assert.Empty(await service.GetRecentAsync(string.Empty));
        Assert.Null(await service.GetByIdAsync(record.Id, string.Empty));
    }

    [Fact]
    public async Task GetRecentAsync_IsOwnerScopedAndBounded()
    {
        await using var context = CreateContext();
        context.PromptProcessingRecords.AddRange(
            Enumerable.Range(1, 4).Select(i => new PromptProcessingRecord { Id = Guid.NewGuid(), UserId = "owner-a", CreatedAt = DateTime.UtcNow.AddMinutes(-i) }).Concat(
                [new PromptProcessingRecord { Id = Guid.NewGuid(), UserId = "owner-b" }]));
        await context.SaveChangesAsync();
        var service = new PromptProcessingHistoryService(new PromptProcessingRecordRepository(context, NullLogger<PromptProcessingRecordRepository>.Instance));

        var records = await service.GetRecentAsync("owner-a", 2);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal("owner-a", record.UserId));
    }

    private static DeveloperMemoryDbContext CreateContext()
    {
        return new DeveloperMemoryDbContext(new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }
}
