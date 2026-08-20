---
title: .NET Best Practices
project: DeveloperMemory
tags: dotnet, csharp, best-practices, performance
---

# .NET Best Practices

## Dependency Injection
- Register services with appropriate lifetimes (Singleton, Scoped, Transient)
- Use IOptions<T> pattern for strongly-typed configuration
- Prefer constructor injection over service locator pattern
- Use AddHttpClient<T>() for typed HTTP clients with automatic DI

## Performance
- Use async/await for all I/O-bound operations
- Avoid blocking calls in async methods
- Use ValueTask for frequently-synchronous async paths
- Implement IAsyncDisposable for unmanaged resources
- Use ArrayPool<T> or MemoryPool<T> to reduce allocations

## Error Handling
- Use Result<T> pattern instead of exceptions for expected failures
- Reserve exceptions for truly exceptional circumstances
- Log structured data with Serilog, not string interpolation
- Always include correlation IDs in error responses

## Testing
- Use xUnit with FluentAssertions for readable assertions
- Mock external dependencies with Moq or NSubstitute
- Test both happy path and error scenarios
- Aim for 80%+ code coverage on business logic

## Security
- Never store API keys in source code
- Use User Secrets for development credentials
- Implement rate limiting on public endpoints
- Use CORS policies with explicit origins in production
- Validate and sanitize all user input
