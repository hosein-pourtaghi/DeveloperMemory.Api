# CLAUDE.md — DeveloperMemory API

## Project Overview

DeveloperMemory.Api is an OpenAI-compatible context injection proxy built on .NET 10.0. It sits between AI coding assistants and LLM providers, automatically enriching chat completion requests with developer-authored context from Markdown files.

**Purpose:** Prevent developers from repeatedly providing the same context to AI coding assistants.

**How it works:** Intercept chat completion requests → load developer profiles → search knowledge documents → append context to system message → forward to LLM provider → return response.

**Related documents:**
- [PROJECT_VISION.md](PROJECT_VISION.md) — Why this project exists
- [CURRENT_STATUS.md](CURRENT_STATUS.md) — What's implemented
- [ROADMAP.md](ROADMAP.md) — Where it's going
- [AGENTS.md](AGENTS.md) — Coding standards for contributors
- [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) — Frontmatter format reference

## Architecture

### Request Flow

```
AI Client (Cline, Continue, etc.)
        │
        ▼
OpenAIChatCompletionController
        │
        ├── ModeDetector          → Detect plan/build mode
        ├── ProfileService        → Load developer profiles
        ├── KnowledgeService      → Search knowledge documents
        ├── PromptBuilder         → Assemble context, enrich request
        │
        ▼
FreeLlmApiClient ──────────────► LLM Provider
        │
        ▼
Response (streaming or non-streaming) → AI Client
```

### Component Responsibilities

| Component | Responsibility |
|---|---|
| `OpenAIChatCompletionController` | Thin controller. Routes `/v1/chat/completions`, `/v1/models`. Delegates to services. |
| `KnowledgeController` | Management API. CRUD, search, reindex for knowledge documents. |
| `ProfilesController` | Management API. List and load developer profiles. |
| `ProfileService` | Loads developer profiles from Markdown files. Parses YAML frontmatter. |
| `KnowledgeService` | Loads knowledge documents, parses frontmatter, performs keyword search. |
| `PromptBuilder` | Builds context blocks from profiles and knowledge. Appends to system message. Preserves conversation history. |
| `FreeLlmApiClient` | HTTP client for OpenAI-compatible providers. Streaming and non-streaming. |
| `ModeDetector` | Detects plan vs build mode from system prompt content. Static, stateless. |
| `TokenEstimator` | Estimates token counts (~4 chars/token). Static, for logging only. |
| `RequestLogger` | Three-stage token metrics logging (incoming → enriched → response). |
| `StableIdHelper` | Generates deterministic GUIDs from file paths (SHA-256 hash). |
| `GlobalExceptionMiddleware` | Catches unhandled exceptions. Returns OpenAI-compatible errors for `/v1/*`. |
| `RequestLoggingMiddleware` | Diagnostic middleware. Logs raw request bodies for debugging. |

### Dependency Injection

| Service | Lifetime | Notes |
|---|---|---|
| `ProfileService` | Singleton | In-memory profile cache |
| `KnowledgeService` | Singleton | In-memory document index |
| `PromptBuilder` | Singleton | Stateless |
| `FreeLlmApiClient` | HttpClient (transient) | `AddHttpClient<T>()` |
| `RequestLogger` | Singleton | Writes to daily log files |
| `AppSettings` | Options | Bound from `appsettings.json` |
| `ModelSelectionSettings` | Options | Bound from `appsettings.json` |

### Context Assembly

When a chat completion request arrives:

1. Extract the last user message as the search query
2. Load developer profiles from `Profiles/` directory
3. Search knowledge documents for relevance (keyword matching)
4. Build a context block and append it to the system message:
   ```
   [Original system message]

   --- DeveloperMemory Context ---

   [Developer Profile]
   Name: ...
   Role: ...
   Skills: ...

   [Relevant Knowledge]
   ## Document Title (relevance: 0.80)
   Content preview...

   --- End DeveloperMemory Context ---
   ```
5. All user and assistant messages are preserved unchanged
6. Forward enriched request to the downstream provider

### Instruction Precedence

1. **Client's system message** — Preserved and extended, never replaced
2. **Developer identity context** — Appended to system message
3. **Project knowledge context** — Appended to system message
4. **User messages** — Preserved as-is

## API Reference

### POST /v1/chat/completions

Chat completion with automatic context enrichment. Supports streaming.

**Request:** Standard OpenAI chat completion request. Optional extensions:
- `project` (string) — Filter knowledge by project
- `tags` (list) — Filter knowledge by tags
- `profile_id` (string) — Specific developer profile

All standard OpenAI parameters are forwarded. Unknown fields captured via `JsonExtensionData` and forwarded without data loss.

**Response:** Standard OpenAI chat completion response (or SSE stream).

### GET /v1/models

List available models. Falls back to configured default if upstream is unavailable.

### GET /v1/models/{modelId}

Get model details. Returns 404 with OpenAI-compatible error if not found.

### Error Format

All errors on `/v1/*` follow OpenAI-compatible format:

```json
{
  "error": {
    "message": "Description",
    "type": "error_type",
    "code": "error_code",
    "param": "parameter_name"
  }
}
```

### Management Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/Knowledge` | Search documents (query, project, tags) |
| `GET` | `/api/Knowledge/documents` | List all documents |
| `GET` | `/api/Knowledge/{id}` | Get document by GUID |
| `POST` | `/api/Knowledge` | Create document |
| `POST` | `/api/Knowledge/reindex` | Reload all documents from disk |
| `GET` | `/api/Profiles` | List all profiles |
| `POST` | `/api/Profiles` | Load profile from file path |
| `GET` | `/health` | Health check |

## Data Models

| Model | Fields | Notes |
|---|---|---|
| `KnowledgeDocument` | Id, Title, Content, Project, Tags, FilePath, LastModified | Id is deterministic (SHA-256 of file path) |
| `DeveloperProfile` | Id, Name, Role, Skills, Experience, Bio, FilePath, LastModified | Id is deterministic |
| `SearchResult` | Id, Title, Content, Project, Tags, Score, FilePath | Score from keyword relevance |
| `CreateDocumentRequest` | Title, Content, Project, Tags | Used by KnowledgeController |
| `OpenAIChatCompletionRequest` | Standard OpenAI fields + project, tags, profile_id | `JsonExtensionData` preserves unknown fields |
| `Message` | Role, Content, ToolCalls, ToolCallId, Name, ExtensionData | `MessageContentConverter` handles string/array content |

## Configuration

### appsettings.json

```json
{
  "AppSettings": {
    "FreeLlmApi": {
      "BaseUrl": "http://localhost:3001/v1",
      "ApiKey": "",
      "DefaultModel": "auto"
    },
    "Paths": {
      "KnowledgeFolder": "./Knowledge",
      "ProfilesFolder": "./Profiles",
      "RequestLogFolder": "./logs/requests"
    },
    "ModelSelection": {
      "AutoSelectModel": true,
      "PlanModel": "auto:smart",
      "BuildModel": "auto:fast"
    }
  }
}
```

### Environment Variable Overrides

Use `__` separator: `AppSettings__FreeLlmApi__BaseUrl`, etc.

### Model Resolution

1. Per-request `model` field (highest)
2. `AppSettings:FreeLlmApi:DefaultModel` from config
3. `"auto"` fallback

### Provider Routing Modes

| Mode | Description |
|---|---|
| `auto` | Router picks the best available model |
| `auto:fast` | Router picks the fastest model |
| `auto:smart` | Router picks the most capable model |
| Explicit model ID | Pin to a specific model (e.g., `gpt-4`) |

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run
```

- HTTP: `http://localhost:5041`
- HTTPS: `https://localhost:7144`
- Swagger: `/swagger` (Development only)
- Health: `GET /health`

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Serilog.AspNetCore` | 8.0.3 | Structured logging |
| `Serilog.Sinks.Console` | 6.0.0 | Console log output |
| `Serilog.Sinks.File` | 6.0.0 | File log output with rolling |
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | OpenAPI spec generation |
| `Swashbuckle.AspNetCore` | 10.0.1 | Swagger UI |

## Limitations

- **No authentication** — CORS is open, no auth middleware
- **Keyword search only** — No semantic/vector search in V1
- **In-memory cache** — Documents loaded on startup; reindex via API
- **No function/tool calling processing** — Tool call messages forwarded but not interpreted
- **Frontmatter `scope` field** — Parsed but not used by runtime; reserved for future use
- **No streaming token counts** — Token estimates for non-streaming only
