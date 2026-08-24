# CLAUDE.md — Developer Memory API

## Project Overview

DeveloperMemory.Api is a **persistent, intelligent memory layer for AI applications and agents**. It enables AI systems to remember relevant information about a user, their preferences, goals, projects, decisions, and previous interactions across conversations.

**Current status:** Working .NET 10.0 implementation of the core LLM proxy infrastructure with knowledge injection. The full memory intelligence pipeline is not yet implemented.

**Core vision:** Act as a Memory Intelligence Gateway between users, AI applications, agents, and LLM providers. Capture valuable information, classify it, manage its lifecycle, retrieve relevant memories, and construct context for AI requests.

---

## Architecture

### Target Architecture — Memory Intelligence Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                    MEMORY CAPTURE PIPELINE                   │
│                                                             │
│  User or AI Application                                     │
│         ↓                                                   │
│  Interaction Processing                                     │
│         ↓                                                   │
│  Memory Capture and Extraction                              │
│         ↓                                                   │
│  Memory Classification                                      │
│         ↓                                                   │
│  Memory Storage and Lifecycle Management                    │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                   MEMORY RETRIEVAL PIPELINE                  │
│                                                             │
│  AI Application                                             │
│         ↓                                                   │
│  Memory Retrieval                                           │
│         ↓                                                   │
│  Relevance Ranking                                          │
│         ↓                                                   │
│  Context Construction                                       │
│         ↓                                                   │
│  LLM Request Enrichment                                     │
│         ↓                                                   │
│  LLM Provider                                               │
└─────────────────────────────────────────────────────────────┘
```

### Current Architecture (Working)

```
IDE AI Client / Cline
        |
        v
DeveloperMemory.Api
        |
        v
Request Validation / Normalization
        |
        v
Load Developer Profile + Search Knowledge
        |
        v
Prompt/Context Enrichment (preserves message history)
        |
        v
OpenAI-Compatible Provider
        |
        v
Streaming (SSE) or Standard Response
        |
        v
IDE AI Client / Cline
```

### Layered Architecture

```
Presentation    ->  Controllers (OpenAIChatCompletionController, KnowledgeController, ProfilesController)
                  Middleware  (GlobalExceptionMiddleware, RequestLoggingMiddleware)
Application     ->  Services (KnowledgeService, ProfileService, PromptBuilder, FreeLlmApiClient, TokenEstimator, RequestLogger, ModeDetector)
Domain          ->  Models (KnowledgeDocument, DeveloperProfile, SearchResult, OpenAI* types)
Infrastructure  ->  Configuration (AppSettings), Logging (Serilog)
```

---

## Memory Model (Target)

### Memory Types

| Type | Description | Example |
|---|---|---|
| `Preference` | User's preferred way of doing things | "I prefer functional programming" |
| `Instruction` | Explicit instruction to follow | "Always use TypeScript strict mode" |
| `Constraint` | Limitation or restriction | "Never use console.log in production" |
| `Goal` | What the user is trying to achieve | "Building a real-time chat app" |
| `PersonalFact` | Information about the user | "I'm a senior backend developer" |
| `ProjectContext` | Information about a specific project | "This project uses PostgreSQL" |
| `TechnicalKnowledge` | Technical information | "The API uses JWT for authentication" |
| `Decision` | A decision that was made | "We chose Redis for caching" |
| `WorkingContext` | Temporary context for current work | "Currently debugging the auth module" |

### Memory States

| State | Description |
|---|---|
| `Active` | Currently valid and available for retrieval |
| `Updated` | Has been modified (previous version superseded) |
| `Superseded` | Replaced by newer information |
| `Expired` | Past its expiration date |
| `Archived` | No longer active but preserved for history |
| `Deleted` | Removed from the system |

### Memory Scopes

| Scope | Description | Lifetime |
|---|---|---|
| `Global` | Applies everywhere | Permanent |
| `User` | Specific to a user | Permanent (until deleted) |
| `Project` | Specific to a project | Permanent (until deleted) |
| `Conversation` | Relevant only to current conversation | End of conversation |
| `Session` | Temporary working context | End of session |
| `Agent` | Specific to an AI agent | Agent lifetime |

---

## Dependency Injection

| Registration | Type | Lifetime |
|---|---|---|
| `ProfileService` | Singleton | In-memory profile cache |
| `KnowledgeService` | Singleton | In-memory document index |
| `PromptBuilder` | Singleton | Stateless prompt construction |
| `FreeLlmApiClient` | HttpClient | Transient (via `AddHttpClient<T>`) |
| `TokenEstimator` | Singleton | Stateless token estimation |
| `RequestLogger` | Singleton | Stateless logging |
| `ModeDetector` | Singleton | Stateless mode detection |
| `AppSettings` | Options | Bound from `appsettings.json` -> `AppSettings` section |

---

## Instruction Precedence (highest to lowest)

1. **Client's system message** — Preserved and extended, never replaced
2. **DeveloperMemory profile context** — Appended to system message
3. **Knowledge context** — Relevant documents appended to system message
4. **User messages** — Preserved as-is; original conversation history intact

---

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
- `model` — Model selection (resolved: per-request -> config -> "auto")
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

---

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

---

## Data Models

### KnowledgeDocument
`Id` (Guid), `Title`, `Content`, `Project`, `Tags` (List<string>), `FilePath`, `LastModified`.

### DeveloperProfile
`Id` (Guid), `Name`, `Role`, `Skills` (List<string>), `Experience`, `Bio`, `FilePath`, `LastModified`.

### SearchResult
`Id` (Guid), `Title`, `Content`, `Project`, `Tags`, `Score` (double), `FilePath`.

### OpenAIChatCompletionRequest
Standard OpenAI fields plus DeveloperMemory extensions. Unknown fields captured via `JsonExtensionData`.

### Message
- `role` (string) — Message role
- `content` (string?) — Message content
- `tool_calls` (List<ToolCall>?) — Tool calls (for assistant messages)
- `tool_call_id` (string?) — Tool call ID (for tool messages)
- `name` (string?) — Name field
- `ExtensionData` — Captures additional properties for forwarding

---

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
    "ModelSelection": {
      "AutoSelectModel": true,
      "PlanModel": "auto:smart",
      "BuildModel": "auto:fast"
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

---

## Build & Run

```bash
cd DeveloperMemory.Api
dotnet restore
dotnet build
dotnet run
```

- HTTP: `http://localhost:5041`
- HTTPS: `https://localhost:7144`
- Swagger: `/swagger` (Development only)
- Health: `GET /health`

**Requires:** .NET 10.0 SDK installed on the machine.

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Serilog.AspNetCore` | 8.0.3 | Structured logging |
| `Serilog.Sinks.Console` | 6.0.0 | Console log output |
| `Serilog.Sinks.File` | 6.0.0 | File log output with rolling |
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | OpenAPI spec generation |
| `Swashbuckle.AspNetCore` | 10.0.1 | Swagger UI |

---

## Limitations

- **No memory capture pipeline** — Currently requires manual knowledge creation
- **No memory classification** — All knowledge treated as generic documents
- **No memory lifecycle** — No automatic update, superseding, or expiration
- **No semantic search** — Keyword matching only
- **No persistent storage** — In-memory only, lost on restart
- **No authentication** — CORS is open, local tool only
- **No function/tool calling** — Tool call messages forwarded but not processed
- **Approximate token counts** — ~4 chars/token heuristic
- **Frontmatter parser truncation** — Values containing `:` after first colon are truncated
