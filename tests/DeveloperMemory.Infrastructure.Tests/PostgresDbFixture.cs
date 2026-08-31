using DeveloperMemory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeveloperMemory.Infrastructure.Tests;

/// <summary>
/// xUnit collection that serializes all PostgreSQL integration tests.
/// The shared test database is reset per test class, so classes must not
/// run concurrently (a parallel EnsureDeleted/Migrate would race).
/// </summary>
[CollectionDefinition("Postgres", DisableParallelization = true)]
public class PostgresTestCollection : ICollectionFixture<PostgresDbFixture> { }

/// <summary>
/// Shared fixture that connects to the real local PostgreSQL instance.
/// The connection string is read from the DEVELOPERMEMORY_TEST_CONNECTION
/// environment variable, falling back to the standard local development
/// connection. Tests using this fixture NEVER fall back to EF InMemory —
/// if PostgreSQL is unreachable the fixture fails loudly.
/// </summary>
public sealed class PostgresDbFixture : IDisposable
{
    public const string ConnectionStringEnvVar = "DEVELOPERMEMORY_TEST_CONNECTION";

    public string ConnectionString { get; }

    public PostgresDbFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? "Host=localhost;Port=5432;Database=developermemory_test;Username=developer;Password=devpassword";

        // Verify connectivity immediately; fail fast with a clear message
        // rather than silently falling back to InMemory.
        using var probe = new Npgsql.NpgsqlConnection(ConnectionString);
        probe.Open();
    }

    /// <summary>
    /// Creates a fresh DbContext connected to the real PostgreSQL database.
    /// Each call returns a NEW context instance — the previous one is disposed
    /// by the caller — so tests genuinely exercise the persistence boundary
    /// (data written through one context is read back through another).
    /// </summary>
    public DeveloperMemoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DeveloperMemoryDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(DeveloperMemoryDbContext).Assembly.FullName);
            })
            .Options;

        return new DeveloperMemoryDbContext(options);
    }

    /// <summary>
    /// Resets the test database to a clean, migrated state.
    /// Safe to call per test class because the Postgres collection is serialized.
    /// </summary>
    public void ResetDatabase()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
        context.Database.Migrate();
    }

    public void Dispose() { }
}

/// <summary>
/// Base class for PostgreSQL integration tests.
/// Ensures a clean, migrated database before each test class runs.
/// </summary>
[Collection("Postgres")]
public abstract class PostgresTestBase : IDisposable
{
    protected readonly PostgresDbFixture Fixture;

    protected PostgresTestBase(PostgresDbFixture fixture)
    {
        Fixture = fixture;
        Fixture.ResetDatabase();
    }

    public void Dispose() { }
}
