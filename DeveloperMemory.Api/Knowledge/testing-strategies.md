---
title: Testing Strategies
project: DeveloperMemory
tags: testing, xunit, unit-tests, integration-tests, best-practices
---

# Testing Strategies

## Unit Testing with xUnit
- Test service logic in isolation
- Mock external dependencies (HTTP clients, file system)
- Use descriptive test names: `SearchDocuments_WithMatchingQuery_ReturnsResults`
- Test both happy path and error scenarios

## Integration Testing
- Use WebApplicationFactory for API endpoint testing
- Test full request/response cycle
- Verify DI registration works correctly
- Test with real file system (temp directories)

## Test Coverage Goals
- Business logic: 90%+
- Controllers: 80%+
- Models: Minimal (POCOs, but test serialization)

## Mocking Patterns
```csharp
var mockService = new Mock<IKnowledgeService>();
mockService.Setup(s => s.SearchDocuments(It.IsAny<string>()))
    .Returns(new List<SearchResult> { /* test data */ });
```

## API Testing
- Use HttpClient in tests for endpoint verification
- Test JSON serialization/deserialization
- Verify status codes and response formats
- Test error responses and edge cases
