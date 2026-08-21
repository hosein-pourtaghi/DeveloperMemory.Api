# AGENTS.md — AI Agent Coding Guide

This document is for AI coding agents working on the Developer Memory API. It covers coding standards, project conventions, and how to safely extend the codebase.

## Project Structure

```
DeveloperMemory.Api/
├── Controllers/                    # API endpoints (3 files)
│   ├── OpenAIChatCompletionController.cs  # /v1/chat/completions, /v1/models, /v1/models/{id}
│   ├── KnowledgeController.cs             # /api/Knowledge
│   └── ProfilesController.cs              # /api/Profiles
├── Services/                       # Business logic (4 files)
│   ├── KnowledgeService.cs          # Document parsing, search, indexing
│   ├── ProfileService.cs            # Profile parsing and loading
│   ├── PromptBuilder.cs             # Enriches OpenAI requests with context (preserves conversation)
│   └── FreeLlmApiClient.cs          # HTTP client for downstream OpenAI-compatible providers
├── Models/                         # Data structures (5 files)
│   ├── OpenAIRequestResponse.cs     # All OpenAI-compatible types (request, response, streaming, error)
│   ├── KnowledgeDocument.cs         # Document model
│   ├── DeveloperProfile.cs          # Profile model
│   ├── PromptRequest.cs             # Legacy proxy request model
│   └── SearchResult.cs              # Search result model
├── Infrastructure/
│   ├── Configuration/
│   │   └── AppSettings.cs           # Strongly-typed settings
│   └── Middleware/
│       └── GlobalExceptionMiddleware.cs  # Global error handling (OpenAI-compatible errors for /v1)
├── Knowledge/                      # Markdown files (knowledge documents)
├── Profiles/                       # Markdown files (developer profiles)
├── Program.cs                      # Application entry point and DI setup
├── appsettings.json                # Main configuration
└── appsettings.Development.json
```

## Coding Standards

### Naming
- **PascalCase** for: classes, methods, properties, public fields, namespaces
- **camelCase** for: local variables, parameters, method arguments
- **`_camelCase`** for: private fields (prefixed with underscore)

### File Organization
- One class per file (exception: OpenAI types in `OpenAIRequestResponse.cs` are grouped)
- File names match class names exactly
- Use file-scoped namespaces: `namespace DeveloperMemory.Api.Controllers;`

### C# Patterns
- Use **nullable reference types** (project has `<Nullable>enable</Nullable>`)
- Use `?.` and `??` operators for null safety
- Use `string.Empty` instead of `""` for default strings
- Use `List<T>` not arrays for collection properties
- Use `Guid` for IDs, auto-generated in model defaults
- Use collection expressions: `[]` instead of `new List<T>()`

### Controller Conventions
- Decorate with `[ApiController]` and `[Route("v1")]` for OpenAI endpoints
- Decorate with `[ApiController]` and `[Route("api/[controller]")]` for management endpoints
- Controllers should be **thin** — delegate business logic to services
- Return `ActionResult<T>` for type-safe responses
- Use Serilog `_logger` for error logging
- For streaming endpoints, write directly to `Response.Body` — do not buffer

### Service Conventions
- Services read config from `IConfiguration` or `IOptions<AppSettings>`
- Services use constructor injection
- `KnowledgeService` and `ProfileService` hold in-memory caches
- External HTTP calls go through `FreeLlmApiClient`
- `PromptBuilder` is stateless — construct enriched requests without side effects
- Use `CancellationToken` throughout async chains

### Model Conventions
- Use `[JsonPropertyName("snake_case")]` for OpenAI-compatible fields
- Use `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` for optional fields
- Use `[JsonExtensionData]` on models that may receive unknown properties from clients
- Initialize collections with `[]` (collection expressions)

## How to Extend

### Adding a New Controller
1. Create `YourController.cs` in `Controllers/`
2. Decorate with `[ApiController]` and appropriate route
3. Inject services via constructor
4. Register any new services in `Program.cs` DI
5. Keep the controller thin — delegate to services

### Adding a New Service
1. Create `YourService.cs` in `Services/`
2. Add registration in `Program.cs`:
   - `builder.Services.AddSingleton<YourService>();` for stateful services
   - `builder.Services.AddHttpClient<YourService>();` for HTTP clients
3. Inject into controllers via constructor

### Adding a New Model
1. Create `YourModel.cs` in `Models/`
2. Use file-scoped namespace: `namespace DeveloperMemory.Api.Models;`
3. Auto-generate IDs: `public Guid Id { get; set; } = Guid.NewGuid();`
4. Initialize collections: `public List<string> Items { get; set; } = [];`

### Adding Configuration
1. Add property to `AppSettings.cs` (or nested settings class)
2. Add corresponding JSON in `appsettings.json` under `AppSettings`
3. Inject via `IOptions<AppSettings>` where needed

## Key Gotchas

1. **Singleton services + filesystem**: `KnowledgeService` holds documents in memory. Changes to `.md` files require a reindex (`POST /api/Knowledge/reindex`) to take effect.

2. **Frontmatter parsing is simple**: The parser splits on `:` and takes only the first two segments. Values containing `:` will be truncated. Tags are comma-separated on a single line only (not YAML arrays).

3. **IDs are ephemeral**: Document and profile `Id` fields are `Guid.NewGuid()` — they change every time documents are reloaded. Use `FilePath` for stable identification.

4. **OpenAI endpoint extends the standard**: The `/v1/chat/completions` endpoint accepts non-standard fields (`project`, `tags`, `profile_id`) for context filtering. Unknown fields are forwarded via `JsonExtensionData`.

5. **PromptBuilder preserves conversation history**: `BuildEnrichedRequest()` creates a new request with the original messages preserved. DeveloperMemory context is injected into the system message, not by replacing user messages.

6. **Streaming is raw passthrough**: SSE streaming forwards the upstream provider's response directly to the client. DeveloperMemory does not re-serialize streaming chunks.

7. **Model resolution is configurable**: Default model is set in `appsettings.json` under `AppSettings:FreeLlmApi:DefaultModel` (default: `"auto"`). Can be overridden per-request.

8. **CORS is wide open**: `AllowAll` policy allows any origin, method, and header. This is for development only.

9. **DownstreamProviderException**: When the downstream provider returns an HTTP error, `FreeLlmApiClient` throws `DownstreamProviderException` with the status code and raw error content. The controller translates this into an OpenAI-compatible error response.

10. **GlobalExceptionMiddleware**: Catches unhandled exceptions and returns OpenAI-compatible error responses for `/v1/*` endpoints, standard problem details for other endpoints.

## FreeLLM Routing Modes

| Mode | Description | When to Use |
|---|---|---|
| `auto` | Router picks the best available model | Default — good for general queries |
| `auto:fast` | Router picks the fastest available model | Latency-sensitive requests |
| `auto:smart` | Router picks the most capable available model | Complex reasoning tasks |
| `fusion` | Multiple models answer in parallel, judge synthesizes | High-quality responses |
| Explicit ID | Pin to a specific model (e.g. `gemini-3.5-flash`) | When you need a specific model |

### Model Resolution Priority
1. Per-request `model` field (highest priority)
2. `AppSettings:FreeLlmApi:DefaultModel` from config
3. `"auto"` fallback

## Testing Checklist

When making changes, verify:
- [ ] `dotnet build` succeeds with no errors
- [ ] `GET /health` returns healthy
- [ ] `GET /api/Knowledge` returns documents (or empty list)
- [ ] `GET /api/Profiles` returns profiles (or empty list)
- [ ] `GET /swagger` loads in Development mode
- [ ] `POST /v1/chat/completions` returns OpenAI-format response (non-streaming)
- [ ] `POST /v1/chat/completions` with `stream: true` returns SSE stream
- [ ] `GET /v1/models` returns model list
- [ ] `GET /v1/models/{id}` returns model or 404
- [ ] Error responses follow OpenAI format

## Git Workflow

- Create feature branches from `main`
- Write descriptive commit messages
- Keep commits focused (one logical change per commit)
- Create pull requests for review before merging
