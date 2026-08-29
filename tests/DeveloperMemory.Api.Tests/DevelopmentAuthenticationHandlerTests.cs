using System.Security.Claims;
using System.Text.Encodings.Web;
using DeveloperMemory.Api.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeveloperMemory.Api.Tests;

public class DevelopmentAuthenticationHandlerTests
{
    [Fact]
    public async Task EnabledInDevelopment_ReturnsFixedAuthenticatedIdentity()
    {
        var result = await AuthenticateAsync(true, "Development");

        result.Succeeded.Should().BeTrue();
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
        result.Principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("fixed-owner");
        result.Principal.FindFirstValue("development_bypass").Should().Be("true");
    }

    [Fact]
    public async Task DisabledInDevelopment_DoesNotAuthenticate()
    {
        var result = await AuthenticateAsync(false, "Development");

        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task EnabledOutsideDevelopment_DoesNotAuthenticate()
    {
        var result = await AuthenticateAsync(true, "Production");

        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatedRequests_ReturnIdenticalIdentity()
    {
        var first = await AuthenticateAsync(true, "Development");
        var second = await AuthenticateAsync(true, "Development");

        first.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)
            .Should().Be(second.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        first.Principal.FindFirstValue(ClaimTypes.Name)
            .Should().Be(second.Principal.FindFirstValue(ClaimTypes.Name));
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(bool bypass, string environmentName)
    {
        var settings = Options.Create(new ApiKeySettings
        {
            DevelopmentBypass = bypass,
            DevelopmentOwnerId = "fixed-owner",
            DevelopmentOwnerDisplayName = "Fixed Owner"
        });
        var environment = new TestHostEnvironment(environmentName);
        var options = new TestOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        var context = new DefaultHttpContext();
        var handler = new DevelopmentAuthenticationHandler(
            options,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            settings,
            environment);
        await handler.InitializeAsync(new AuthenticationScheme("Development", null, typeof(DevelopmentAuthenticationHandler)), context);
        return await handler.AuthenticateAsync();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string? ApplicationId { get; set; }
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
