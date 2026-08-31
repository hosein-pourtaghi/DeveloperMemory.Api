using DeveloperMemory.Api.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace DeveloperMemory.Api.Tests;

/// <summary>
/// Tests for per-identity rate-limit partitioning logic.
/// Verifies that authenticated users get separate rate-limit buckets
/// and unauthenticated requests fall back to IP-based partitioning.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public void AuthenticatedUser_GeneratesUserPartitionKey()
    {
        var context = new DefaultHttpContext();
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        }, "ApiKey"));
        context.User = claims;

        var partitionKey = GetPartitionKey(context);

        partitionKey.Should().Be("user:user-1");
    }

    [Fact]
    public void DifferentUsers_GetDifferentPartitionKeys()
    {
        var ctx1 = CreateUserContext("user-1");
        var ctx2 = CreateUserContext("user-2");

        var key1 = GetPartitionKey(ctx1);
        var key2 = GetPartitionKey(ctx2);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void SameUser_SamePartitionKey()
    {
        var ctx1 = CreateUserContext("user-1");
        var ctx2 = CreateUserContext("user-1");

        var key1 = GetPartitionKey(ctx1);
        var key2 = GetPartitionKey(ctx2);

        key1.Should().Be(key2);
    }

    [Fact]
    public void UnauthenticatedUser_GeneratesIpPartitionKey()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

        var partitionKey = GetPartitionKey(context);

        partitionKey.Should().StartWith("ip:");
    }

    [Fact]
    public void UnauthenticatedUsers_SameIp_SamePartitionKey()
    {
        var ctx1 = new DefaultHttpContext();
        ctx1.User = new ClaimsPrincipal(new ClaimsIdentity());

        var ctx2 = new DefaultHttpContext();
        ctx2.User = new ClaimsPrincipal(new ClaimsIdentity());

        // Both have null RemoteIpAddress
        var key1 = GetPartitionKey(ctx1);
        var key2 = GetPartitionKey(ctx2);

        key1.Should().Be(key2);
    }

    // ── Endpoint category path matching ──

    [Theory]
    [InlineData("/api/ApiKey", "km")]
    [InlineData("/api/ApiKey/create", "km")]
    [InlineData("/api/ApiKey/rotate/123", "km")]
    [InlineData("/api/ApiKey/revoke/123", "km")]
    [InlineData("/api/ApiKey/audit", "km")]
    public void KeyManagementEndpoints_UseKmCategory(string path, string expectedPrefix)
    {
        var category = GetEndpointCategory(path);
        category.Should().Be(expectedPrefix);
    }

    [Theory]
    [InlineData("/api/Memory/query", "ex")]
    [InlineData("/api/Memory/retrieve", "ex")]
    [InlineData("/api/Memory/analyze", "ex")]
    [InlineData("/api/Memory/1/embedding/rebuild", "ex")]
    [InlineData("/v1/chat/completions", "ex")]
    public void ExpensiveEndpoints_UseExCategory(string path, string expectedPrefix)
    {
        var category = GetEndpointCategory(path);
        category.Should().Be(expectedPrefix);
    }

    [Theory]
    [InlineData("/api/Memory", "gen")]
    [InlineData("/api/Memory/stats", "gen")]
    [InlineData("/api/Memory/123", "gen")]
    [InlineData("/health", "gen")]
    [InlineData("/api/Projects", "gen")]
    public void GeneralEndpoints_UseGenCategory(string path, string expectedPrefix)
    {
        var category = GetEndpointCategory(path);
        category.Should().Be(expectedPrefix);
    }

    // ── Helpers ──

    private static HttpContext CreateUserContext(string userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "ApiKey"));
        return context;
    }

    /// <summary>
    /// Mirrors the partition key logic from Program.cs for testing.
    /// </summary>
    private static string GetPartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }
        return $"ip:{httpContext.Connection?.RemoteIpAddress}";
    }

    /// <summary>
    /// Mirrors the endpoint category logic from Program.cs for testing.
    /// </summary>
    private static string GetEndpointCategory(string path)
    {
        if (path.StartsWith("/api/ApiKey", StringComparison.OrdinalIgnoreCase))
            return "km";

        if (path.Contains("/query", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/retrieve", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/analyze", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/embedding", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            return "ex";

        return "gen";
    }
}
