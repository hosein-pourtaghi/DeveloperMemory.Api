---
title: API Design Patterns
project: DeveloperMemory
tags: api, rest, design-patterns, aspnet-core, best-practices
---

# API Design Patterns

## RESTful Endpoint Conventions
- Use plural nouns for resources: `/api/Knowledge`, `/api/Profiles`
- Use HTTP verbs for actions: GET=read, POST=create, PUT=update, DELETE=remove
- Return appropriate status codes: 200=OK, 201=Created, 400=BadRequest, 404=NotFound, 500=ServerError
- Use query parameters for filtering: `/api/Knowledge?query=dotnet&project=MyApp`

## Controller Structure
- One controller per resource domain
- Use `[ApiController]` and `[Route("api/[controller]")]` attributes
- Constructor injection for services
- Return `ActionResult<T>` for type safety

## Error Handling Pattern
```csharp
try
{
    var result = await _service.DoWorkAsync(request);
    return Ok(result);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing request");
    return StatusCode(500, $"Error: {ex.Message}");
}
```

## OpenAI-Compatible Endpoints
- Mirror the OpenAI API structure for drop-in replacement
- Use `/v1/chat/completions` and `/v1/models` paths
- Accept standard OpenAI request format with optional extensions

## Documentation
- Swagger/OpenAPI for interactive API docs
- XML comments on public methods for auto-generated documentation
