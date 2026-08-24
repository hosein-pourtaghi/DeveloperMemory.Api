# AGENTS.md — AI Agent Coding Guide

## Current State

This repository contains a working .NET 10.0 implementation of the DeveloperMemory.Api gateway. The source code compiles (when a .NET SDK is available), serves HTTP requests, and has been audited and repaired (as of August 2026).

When working in this repository, treat the source code as the primary truth. The design documents in this directory are specifications that may lag behind the implementation.

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
│   ├── PromptBuilder.cs             # Enriches OpenAI requests (preserves conversation)
│   ├── FreeLlmApiClient.cs          # HTTP client for downstream providers
│   ├── TokenEstimator.cs            # Token counting utility (~4 chars/token heuristic)
│   ├── RequestLogger.cs             # Logs token metrics to file and console
│   └── ModeDetector.cs              # Detects plan vs build mode from Cline's prompt
├── Models/
│   ├── OpenAIRequestResponse.cs     # All OpenAI-compatible types
│   ├── MessageContentConverter.cs   # Handles string or array content fields
│   ├── KnowledgeDocument.cs
│   ├── DeveloperProfile.cs
│   ├── PromptRequest.cs
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
2. Use `name` and `scope` fields (see [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md))
3. Call `POST /api/Knowledge/reindex` to reload

## Key Gotchas

1. **IDs are ephemeral**: Document/profile IDs change on every reload. Use `FilePath` for stable identification.

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
