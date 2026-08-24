# AGENTS.md — AI Agent Coding Guide

## Project Structure

```
DeveloperMemory.Api/
├── Controllers/
│   ├── OpenAIChatCompletionController.cs  # /v1/chat/completions, /v1/models
│   ├── KnowledgeController.cs             # /api/Knowledge
│   └── ProfilesController.cs              # /api/Profiles
├── Services/
│   ├── KnowledgeService.cs          # Document parsing, search, indexing
│   ├── ProfileService.cs            # Profile parsing and loading
│   ├── PromptBuilder.cs             # Assembles context and enriches requests
│   ├── FreeLlmApiClient.cs          # HTTP client for downstream providers
│   ├── TokenEstimator.cs            # Token counting utility (~4 chars/token heuristic)
│   ├── RequestLogger.cs             # Logs token metrics to file and console
│   ├── ModeDetector.cs              # Detects plan vs build mode from system prompt
│   └── StableIdHelper.cs            # Deterministic GUID generation from file paths
├── Models/
│   ├── OpenAIRequestResponse.cs     # All OpenAI-compatible types
│   ├── MessageContentConverter.cs   # Handles string or array content fields
│   ├── KnowledgeDocument.cs
│   ├── DeveloperProfile.cs
│   └── SearchResult.cs
├── Infrastructure/
│   ├── Configuration/
│   │   └── AppSettings.cs           # Settings including ModelSelection
│   └── Middleware/
│       ├── GlobalExceptionMiddleware.cs
│       └── RequestLoggingMiddleware.cs  # Diagnostic request body logging
├── Knowledge/                      # Markdown knowledge documents
├── Profiles/                       # Markdown developer profiles
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

## Key Features

### Auto Model Selection
- `ModeDetector` analyzes Cline's system prompt to detect plan vs build mode
- `ModelSelectionSettings` configures which model to use for each mode
- When `AutoSelectModel: true`, the client's model choice is overridden

### Token Tracking
- `TokenEstimator` provides ~4 chars/token estimation for logging
- `RequestLogger` logs three stages: INCOMING → ENRICHED → RESPONSE
- Daily log files at `logs/requests/requests-YYYY-MM-DD.log`
- Console output via Serilog (look for `TokenSummary:` lines)

### Cline Compatibility
- `MessageContentConverter` handles both string and array `content` fields
- `InvalidModelStateResponseFactory` returns OpenAI-compatible errors for bad requests
- `RequestLoggingMiddleware` captures raw request bodies for debugging

## Coding Standards

### Naming
- **PascalCase** for classes, methods, properties, public fields
- **camelCase** for local variables, parameters
- **`_camelCase`** for private fields

### File Organization
- One class per file (exception: OpenAI types grouped in `OpenAIRequestResponse.cs`)
- Use file-scoped namespaces

### C# Patterns
- Nullable reference types enabled
- Use `string.Empty` not `""`
- Use `[]` collection expressions
- Pass `CancellationToken` through async chains

### Controller Conventions
- Controllers must be **thin** — delegate to services
- Return `ActionResult<T>` for type safety
- Use Serilog `_logger` for logging

### Service Conventions
- Services use constructor injection
- Singleton services hold in-memory state
- Stateless services are safe to register as singletons
- External HTTP via typed `HttpClient` (FreeLlmApiClient)

## How to Extend

### Adding a New Mode
1. Add enum value to `ModeDetector.TaskMode`
2. Add detection logic in `ModeDetector.DetectMode()`
3. Add model setting to `ModelSelectionSettings`
4. Add mapping in `OpenAIChatCompletionController.ChatCompletions()`

### Adding a New Knowledge Source
1. Create `.md` file in `Knowledge/` with YAML frontmatter
2. Use `title`, `project`, `tags` fields
3. Call `POST /api/Knowledge/reindex` to reload

## Key Gotchas

1. **IDs are stable**: Document/profile IDs are deterministic, derived from file paths via `StableIdHelper`. Same file always produces the same ID.

2. **Token estimates are approximate**: ~4 chars/token heuristic. For billing-accurate counts, check `provider_tokens` in the response.

3. **Mode detection is heuristic**: Based on system prompt content. Edge cases may misclassify. Set `AutoSelectModel: false` if the gateway selects the wrong model.

4. **Request log files accumulate**: Daily files in `logs/requests/`. Consider cleanup for production.

5. **CORS is wide open**: For development only. Lock down for production.

6. **Multimodal content**: Array content is stored as JSON string in `Message.Content`. It's forwarded correctly but DeveloperMemory doesn't parse individual content parts.

## Related Documentation

- [PROJECT_VISION.md](PROJECT_VISION.md) — Mission, problem statement, core concepts
- [CURRENT_STATUS.md](CURRENT_STATUS.md) — Implementation inventory and status
- [ROADMAP.md](ROADMAP.md) — Development roadmap
- [CLAUDE.md](CLAUDE.md) — Complete project reference
- [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) — Frontmatter format reference

## Testing Checklist

- [ ] `dotnet build` succeeds with 0 errors
- [ ] `GET /health` returns healthy
- [ ] `POST /v1/chat/completions` returns OpenAI-format response
- [ ] `POST /v1/chat/completions` with `stream: true` returns SSE stream
- [ ] `GET /v1/models` returns model list
- [ ] `GET /v1/models/{id}` returns model or 404
- [ ] Token metrics appear in console and log file
- [ ] Auto model selection routes plan vs build correctly
- [ ] `GET /api/Knowledge/documents` returns loaded knowledge documents
- [ ] `GET /api/Knowledge?query=dotnet` returns relevant results
- [ ] `GET /api/Profiles` returns loaded profiles
