using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DeveloperMemory.Tests;

/// <summary>
/// Shared EF Core InMemory database fixture for tests.
/// Each test class gets a fresh database instance.
/// </summary>
public class InMemoryDbFixture : IDisposable
{
    public DeveloperMemoryDbContext Context { get; }

    public InMemoryDbFixture()
    {
        var options = new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new DeveloperMemoryDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
