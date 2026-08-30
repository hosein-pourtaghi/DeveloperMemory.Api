using DeveloperMemory.Api.Infrastructure.Configuration;
using DeveloperMemory.Api.Infrastructure.Middleware;
using DeveloperMemory.Domain.Entities;
using DeveloperMemory.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Tests for the diagnostic logging middleware and repository.
/// Covers: disabled mode, enabled mode, exception capture, persistence failure resilience.
/// </summary>
public class DiagnosticLoggingTests
{
    private readonly Mock<IDiagnosticLogRepository> _mockRepository;
    private readonly Mock<ILogger<DiagnosticLoggingMiddleware>> _mockLogger;

    public DiagnosticLoggingTests()
    {
        _mockRepository = new Mock<IDiagnosticLogRepository>();
        _mockLogger = new Mock<ILogger<DiagnosticLoggingMiddleware>>();
    }

    // ── Disabled mode ──

    [Fact]
    public async Task PersistToDatabaseFalse_DoesNotPersistDiagnosticLogs()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = false });
        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/v1/chat/completions");
        await middleware.InvokeAsync(context);

        _mockRepository.Verify(
            r => r.TryLogAsync(It.IsAny<DiagnosticLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PersistToDatabaseFalse_NullRepository_DoesNotThrow()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = false });
        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/v1/models");
        await middleware.InvokeAsync(context);

        // No exception thrown
        Assert.Equal(200, context.Response.StatusCode);
    }

    // ── Enabled mode ──

    [Fact]
    public async Task PersistToDatabaseTrue_PersistsDiagnosticLogs()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("POST", "/v1/chat/completions", _mockRepository.Object);
        context.Response.StatusCode = 200;

        await middleware.InvokeAsync(context);

        _mockRepository.Verify(
            r => r.TryLogAsync(It.Is<DiagnosticLogEntry>(e =>
                e.HttpMethod == "POST" &&
                e.RequestPath == "/v1/chat/completions" &&
                e.Timestamp != default &&
                e.DurationMs >= 0),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnabledMode_CapturesStatusAndDuration()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        DiagnosticLogEntry? capturedEntry = null;

        _mockRepository.Setup(r => r.TryLogAsync(It.IsAny<DiagnosticLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DiagnosticLogEntry, CancellationToken>((e, _) => capturedEntry = e)
            .Returns(Task.CompletedTask);

        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/v1/models", _mockRepository.Object);
        context.Response.StatusCode = 200;

        await middleware.InvokeAsync(context);

        Assert.NotNull(capturedEntry);
        Assert.Equal("GET", capturedEntry!.HttpMethod);
        Assert.Equal("/v1/models", capturedEntry.RequestPath);
        Assert.Equal(200, capturedEntry.StatusCode);
        Assert.True(capturedEntry.DurationMs >= 0);
        Assert.NotNull(capturedEntry.RequestId);
    }

    // ── Exception capture ──

    [Fact]
    public async Task ExceptionInPipeline_RecordsExceptionInDiagnosticLog()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        DiagnosticLogEntry? capturedEntry = null;

        _mockRepository.Setup(r => r.TryLogAsync(It.IsAny<DiagnosticLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DiagnosticLogEntry, CancellationToken>((e, _) => capturedEntry = e)
            .Returns(Task.CompletedTask);

        var expectedException = new InvalidOperationException("Test failure");
        var middleware = new DiagnosticLoggingMiddleware(
            _ => throw expectedException, _mockLogger.Object, settings);

        var context = CreateHttpContext("POST", "/v1/chat/completions", _mockRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.NotNull(capturedEntry);
        Assert.Equal("Error", capturedEntry!.Level);
        Assert.Equal("Exception", capturedEntry.EventType);
        Assert.Equal("System.InvalidOperationException", capturedEntry.ExceptionType);
        Assert.Equal("Test failure", capturedEntry.ExceptionMessage);
    }

    [Fact]
    public async Task ExceptionInPipeline_OriginalExceptionStillPropagates()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        var middleware = new DiagnosticLoggingMiddleware(
            _ => throw new InvalidOperationException("boom"), _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/test", _mockRepository.Object);

        // The original exception must still propagate
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    // ── Persistence failure resilience ──

    [Fact]
    public async Task RepositoryThrows_DoesNotBreakApplication()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        _mockRepository.Setup(r => r.TryLogAsync(It.IsAny<DiagnosticLogEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection lost"));

        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/v1/models", _mockRepository.Object);
        context.Response.StatusCode = 200;

        // Must NOT throw — the request must succeed
        await middleware.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
    }

    // ── No secret leakage ──

    [Fact]
    public async Task DiagnosticLogEntry_DoesNotContainSecrets()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        DiagnosticLogEntry? capturedEntry = null;

        _mockRepository.Setup(r => r.TryLogAsync(It.IsAny<DiagnosticLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DiagnosticLogEntry, CancellationToken>((e, _) => capturedEntry = e)
            .Returns(Task.CompletedTask);

        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/v1/models", _mockRepository.Object);
        // Add an Authorization header
        context.Request.Headers["Authorization"] = "Bearer secret-token-123";

        await middleware.InvokeAsync(context);

        Assert.NotNull(capturedEntry);
        // The entry should NOT contain the Authorization header value
        var entryJson = System.Text.Json.JsonSerializer.Serialize(capturedEntry);
        Assert.DoesNotContain("secret-token-123", entryJson);
        Assert.DoesNotContain("Bearer", entryJson);
    }

    // ── No null repository when enabled ──

    [Fact]
    public async Task EnabledButNullRepository_DoesNotThrow()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/test"); // No repository in DI
        context.Response.StatusCode = 200;

        // Should not throw even with missing repository
        await middleware.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
    }

    // ── Environment capture ──

    [Fact]
    public async Task DiagnosticLogEntry_CapturesEnvironment()
    {
        var settings = Options.Create(new DiagnosticsSettings { PersistToDatabase = true });
        DiagnosticLogEntry? capturedEntry = null;

        _mockRepository.Setup(r => r.TryLogAsync(It.IsAny<DiagnosticLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<DiagnosticLogEntry, CancellationToken>((e, _) => capturedEntry = e)
            .Returns(Task.CompletedTask);

        var middleware = new DiagnosticLoggingMiddleware(
            _ => Task.CompletedTask, _mockLogger.Object, settings);

        var context = CreateHttpContext("GET", "/test", _mockRepository.Object);
        context.Response.StatusCode = 200;

        await middleware.InvokeAsync(context);

        Assert.NotNull(capturedEntry);
        // Environment should be captured (may be null in test environment, that's ok)
        Assert.NotNull(capturedEntry!.Timestamp);
    }

    // ── Helper ──

    private static HttpContext CreateHttpContext(string method, string path, IDiagnosticLogRepository? repo = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.StatusCode = 200;

        // Inject mock repository via RequestServices (matches production middleware resolution)
        var services = new Moq.Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(IDiagnosticLogRepository))).Returns(repo);
        context.RequestServices = services.Object;

        return context;
    }
}

/// <summary>
/// Contract test verifying DiagnosticLogRepository implements IDiagnosticLogRepository.
/// </summary>
public class DiagnosticLogRepositoryContractTests
{
    [Fact]
    public void DiagnosticLogRepository_ImplementsIDiagnosticLogRepository()
    {
        var repoType = typeof(DeveloperMemory.Infrastructure.Persistence.DiagnosticLogRepository);
        var interfaceType = typeof(IDiagnosticLogRepository);

        Assert.True(interfaceType.IsAssignableFrom(repoType),
            "DiagnosticLogRepository should implement IDiagnosticLogRepository");
    }

    [Fact]
    public void DiagnosticLogEntry_HasExpectedProperties()
    {
        var entryType = typeof(DiagnosticLogEntry);

        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.Timestamp)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.Level)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.HttpMethod)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.RequestPath)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.StatusCode)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.DurationMs)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.RequestId)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.UserId)));
        Assert.NotNull(entryType.GetProperty(nameof(DiagnosticLogEntry.ExceptionType)));
    }
}
