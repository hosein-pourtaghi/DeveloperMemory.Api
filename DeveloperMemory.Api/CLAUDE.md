# CLAUDE.md — Developer Memory API

## Project Overview

DeveloperMemory.Api is an **OpenAI-compatible Developer Memory Gateway** built on .NET 10.0. It sits between AI coding assistants (Cline, Continue, etc.) and OpenAI-compatible LLM providers, enriching requests with persistent developer preferences, coding guidelines, project knowledge, and relevant long-term memory.

**Core purpose:** Prevent developers and AI coding assistants from repeatedly needing to rediscover or manually provide important context.

**Core capabilities:**
- Expose OpenAI-compatible `/v1/chat/completions` with full streaming support
- Enrich requests with developer profiles and knowledge before forwarding
- Preserve the original conversation history (multi-turn support)
- Forward to any OpenAI-compatible downstream provider (FreeLLM, OpenAI, etc.)
- Store and search technical documentation in Markdown with YAML frontmatter

**Related documents:**
- [PROJECT_VISION.md](PROJECT_VISION.md) — Mission, problem statement, core concepts
- [CURRENT_STATUS.md](CURRENT_STATUS.md) — Implementation inventory and status
- [ROADMAP.md](ROADMAP.md) — Development roadmap
- [AGENTS.md](AGENTS.md) — AI agent coding guide
- [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) — Frontmatter format reference

## Architecture

### Request Flow

```
IDE AI Client / Cline
        ↓
DeveloperMemory.Api
        ↓
Request Validation / Normalization
        ↓
Load Developer Profile + Search Knowledge
        ↓
Prompt/Context Enrichment (preserves message history)
        ↓
OpenAI-Compatible Provider
        ↓
Streaming (SSE) or Standard Response
        ↓
IDE AI Client / Cline
```

### Layered Architecture

```
Presentation    →  Controllers (OpenAIChatCompletionController, KnowledgeController, ProfilesController)
                  Middleware  (GlobalExceptionMiddleware)
Application     →  Services (KnowledgeService, ProfileService, PromptBuilder, FreeLlmApiClient)
Domain          →  Models (KnowledgeDocument, DeveloperProfile, SearchResult, OpenAI* types)
Infrastructure  →  Configuration (AppSettings), Logging (Serilog)
```

### Dependency Injection

| Registration | Type | Lifetime |
|---|---|---|
| `ProfileService` | Singleton | In-memory profile cache |
| `KnowledgeService` | Singleton | In-memory document index |
| `PromptBuilder` | Singleton | Stateless prompt construction |
| `FreeLlmApiClient` | HttpClient | Transient (via `AddHttpClient<T>`) |
| `AppSettings` | Options | Bound from `appsettings.json` → `AppSettings` section |

### Key Components

**OpenAIChatCompletionController** — Thin controller handling `/v1/chat/completions`, `/v1/models`, and `/v1/models/{modelId}`. Delegates all business logic to services.

**PromptBuilder** — Enriches OpenAI requests with developer profile and knowledge context. Uses `BuildEnrichedRequest()` which preserves the original conversation history and injects context into the system message. Legacy `BuildPrompt()` method retained for backward compatibility.

**FreeLlmApiClient** — Provider-agnostic HTTP client for OpenAI-compatible APIs. Supports streaming (`SendStreamingCompletionAsync`) and non-streaming (`SendCompletionAsync`). Uses `ResponseHeadersRead` for streaming to avoid buffering.

**KnowledgeService** — Loads Markdown documents from the filesystem, parses YAML frontmatter, and performs keyword-based relevance search.

**ProfileService** — Loads developer profiles from Markdown files with YAML frontmatter.

**GlobalExceptionMiddleware** — Catches unhandled exceptions and returns OpenAI-compatible error responses for `/v1/*` endpoints.

### Instruction Precedence (highest to lowest)

1. **Client's system message** — Preserved and extended, never replaced
2. **DeveloperMemory profile context** — Appended to system message
3. **Knowledge context** — Relevant documents appended to system message
4. **User messages** — Preserved as-is; original conversation history intact

### Prompt Enrichment Detail

When a chat completion request arrives:
1. Extract the last user message as the search query
2. Load developer profiles from the `Profiles/` directory
3. Search knowledge documents for relevance
4. Build enriched request via `PromptBuilder.BuildEnrichedRequest()`:
   - If a system message exists: append DeveloperMemory context to it
   - If no system message: create one with context
   - All other messages preserved unchanged
5. Forward enriched request to the downstream provider
6. Return response (streaming or non-streaming)

## OpenAI-Compatible API Reference

### POST /v1/chat/completions

Chat completion endpoint supporting both streaming and non-streaming.

**Request body:**
```json
{
  "model": "auto",
  "messages": [
    { "role": "system", "content": "You are a helpful assistant." },
    { "role": "user", "content": "How do I use dependency injection?" }
  ],
  "temperature": 0.7,
  "top_p": 1.0,
  "max_tokens": 2048,
  "stream": false,
  "frequency_penalty": 0.0,
  "presence_penalty": 0.0,
  "stop": null,
  "n": 1,
  "user": "cline-user"
}
```

**Standard OpenAI parameters forwarded:**
- `model` — Model selection (resolved: per-request → config → "auto")
- `messages` — Full conversation history (preserved)
- `temperature` — Sampling temperature
- `top_p` — Nucleus sampling
- `max_tokens` / `max_completion_tokens` — Token limits
- `stream` — Enable SSE streaming
- `stream_options` — Streaming options (e.g., `include_usage`)
- `frequency_penalty` — Frequency penalty
- `presence_penalty` — Presence penalty
- `stop` — Stop sequences
- `n` — Number of completions
- `user` — User identifier

**DeveloperMemory extensions (optional):**
- `project` — Filter knowledge by project
- `tags` — Filter knowledge by tags
- `profile_id` — Specific developer profile GUID

**Non-standard fields** from the client are captured via `JsonExtensionData` and forwarded to the downstream provider without data loss.

### GET /v1/models

List available models from the upstream provider. Falls back to the configured default model if the upstream is unavailable.

**Response:**
```json
{
  "object": "list",
  "data": [
    {
      "id": "auto",
      "object": "model",
      "created": 1700000000,
      "owned_by": "developer-memory"
    }
  ]
}
```

### GET /v1/models/{modelId}

Get details for a specific model. Returns 404 with OpenAI-compatible error if not found.

### Error Responses

All errors on `/v1/*` endpoints follow the OpenAI-compatible format:

```json
{
  "error": {
    "message": "Description of the error",
    "type": "error_type",
    "code": "error_code",
    "param": "parameter_name"
  }
}
```

**Error types:**
- `invalid_request_error` — Bad request, missing fields, model not found
- `authentication_error` — Upstream provider auth failure
- `permission_error` — Access denied
- `rate_limit_error` — Rate limit exceeded (429)
- `timeout_error` — Upstream provider timeout
- `server_error` — Internal error or upstream failure
- `upstream_error` — Non-mapped upstream provider error (502)

## Management API Reference

### Knowledge Controller (`/api/Knowledge`)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/Knowledge` | Search documents (query, project, tags) |
| `GET` | `/api/Knowledge/documents` | List all documents |
| `GET` | `/api/Knowledge/{id}` | Get document by GUID |
| `POST` | `/api/Knowledge` | Create document (body: `CreateDocumentRequest`) |
| `POST` | `/api/Knowledge/reindex` | Reload and reindex all documents |

### Profiles Controller (`/api/Profiles`)

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/Profiles` | List all loaded profiles |
| `POST` | `/api/Profiles` | Load a profile from file path |

## Data Models

### OpenAIChatCompletionRequest
Standard OpenAI request fields (`model`, `messages`, `temperature`, `top_p`, `max_tokens`, `stream`, `frequency_penalty`, `presence_penalty`, `stop`, `n`, `user`, `stream_options`) plus DeveloperMemory extensions (`project`, `tags`, `profile_id`). Unknown fields captured via `JsonExtensionData`.

### Message
- `role` (string) — Message role
- `content` (string?) — Message content
- `tool_calls` (List<ToolCall>?) — Tool calls (for assistant messages)
- `tool_call_id` (string?) — Tool call ID (for tool messages)
- `name` (string?) — Name field
- `ExtensionData` — Captures additional properties (e.g., content arrays) for forwarding

### OpenAIChatCompletionResponse
Standard OpenAI response: `id`, `object`, `created`, `model`, `choices[]`, `usage`, `system_fingerprint`.

### ChatCompletionChunk
Streaming response chunk: `id`, `object` ("chat.completion.chunk"), `created`, `model`, `choices[]` (with `delta` instead of `message`), `usage`.

### OpenAIErrorResponse
```json
{ "error": { "message": "...", "type": "...", "code": "...", "param": "..." } }
```

### KnowledgeDocument
`Id` (Guid), `Title`, `Content`, `Project`, `Tags` (List<string>), `FilePath`, `LastModified`.

### DeveloperProfile
`Id` (Guid), `Name`, `Role`, `Skills` (List<string>), `Experience`, `Bio`, `FilePath`, `LastModified`.

### SearchResult
`Id` (Guid), `Title`, `Content`, `Project`, `Tags`, `Score` (double), `FilePath`.

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
      "ProfilesFolder": "./Profiles"
    }
  }
}
```

### Environment Variable Overrides

Use `__` separator:
- `AppSettings__FreeLlmApi__BaseUrl`
- `AppSettings__FreeLlmApi__ApiKey`
- `AppSettings__FreeLlmApi__DefaultModel`
- `AppSettings__Paths__KnowledgeFolder`
- `AppSettings__Paths__ProfilesFolder`

### Model Resolution Priority

1. Per-request `model` field (highest)
2. `AppSettings:FreeLlmApi:DefaultModel` from config
3. `"auto"` fallback

### FreeLLM Routing Modes

| Mode | Description |
|---|---|
| `auto` | Router picks the best available model |
| `auto:fast` | Router picks the fastest available model |
| `auto:smart` | Router picks the most capable available model |
| `fusion` | Multiple models answer in parallel, judge synthesizes |
| Explicit ID | Pin to a specific model (e.g., `gpt-4`, `gemini-3.5-flash`) |

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

## Memory Model

DeveloperMemory distinguishes four conceptual layers:

| Concept | Description | V1 Status |
|---|---|---|
| **Developer Profile** | Persistent info about the developer: preferences, conventions, skills, role | ✅ File-based Markdown with YAML frontmatter |
| **Knowledge Document** | Reusable info about projects or technologies: standards, rules, documentation | ✅ File-based Markdown with YAML frontmatter |
| **Project Context** | Scoping information that associates knowledge with specific projects | ✅ `project` field in frontmatter and request |
| **Decision / Historical Memory** | Important decisions, outcomes, historical context | ❌ Out of scope for V1 |

The LLM proxy capability is an **integration feature**, not the conceptual center. The core value is the memory and context assembly, not the provider forwarding.

## Limitations

- **No authentication** — CORS is open, no auth middleware is enforced
- **Keyword search only** — No semantic/vector search; relevance scoring is text-based
- **In-memory cache** — Documents loaded on startup; reindex via `POST /api/Knowledge/reindex`
- **ID instability** — Document/profile IDs regenerate on each load; use `FilePath` for stable identification
- **No function/tool calling** — Tool call messages are forwarded but not processed by DeveloperMemory
- **No embeddings endpoint** — Out of scope for V1
- **Frontmatter parser limitations** — Simple `:` split; values containing `:` will be truncated
