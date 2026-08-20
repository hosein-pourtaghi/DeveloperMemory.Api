# CLAUDE.md — Developer Memory API

## Project Overview

Developer Memory API is a **.NET 10.0 Web API** that serves as a knowledge management system and AI assistant gateway. It enables developers to store/retrieve technical knowledge as Markdown files and query AI models with contextual awareness from those documents and developer profiles.

**Core capabilities:**
- Store and search technical documentation in Markdown with YAML frontmatter
- Manage developer profiles with skills, experience, and roles
- Proxy queries to external LLM APIs enriched with context from profiles and documents
- Expose an **OpenAI-compatible `/v1/chat/completions`** endpoint

## Architecture

### Layered Architecture

```
Presentation    →  Controllers (KnowledgeController, ProfilesController, ProxyController, OpenAIChatCompletionController)
Application     →  Services (KnowledgeService, ProfileService, PromptBuilder, FreeLlmApiClient)
Domain          →  Models (KnowledgeDocument, DeveloperProfile, PromptRequest, SearchResult, OpenAI* types)
Infrastructure  →  Configuration (AppSettings), Extensions (ServiceCollectionExtensions), Logging (Serilog)
```

### Dependency Injection (Program.cs)

All services are registered as **Singletons** (services load from filesystem and hold in-memory state):

| Registration | Type | Lifetime |
|---|---|---|
| `ProfileService` | Singleton | In-memory profile cache |
| `KnowledgeService` | Singleton | In-memory document index |
| `PromptBuilder` | Singleton | Stateless prompt construction |
| `FreeLlmApiClient` | HttpClient | Transient (via `AddHttpClient<T>`) |
| `AppSettings` | Options | Bound from `appsettings.json` → `AppSettings` section |

### Data Flow

**Document Search:**
1. Markdown files in `Paths:KnowledgeFolder` are parsed on startup (`KnowledgeService.LoadDocumentsAsync()`)
2. YAML frontmatter is extracted for metadata (`title`, `project`, `tags`)
3. Content after frontmatter becomes the document body
4. Search uses keyword matching with relevance scoring: title (0.5) > content (0.3) > project (0.1) > tags (0.1 each)

**AI Proxy Query (`/api/Proxy`):**
1. `PromptRequest` arrives with query, optional project/tags filter, optional profile ID, optional model override
2. `ProfileService` loads all profiles from `Paths:ProfilesFolder`
3. `KnowledgeService` searches documents matching query/project/tags
4. `PromptBuilder` assembles: system prompt + profile context + search results + user query
5. `FreeLlmApiClient` sends assembled prompt to the configured LLM API with resolved model
6. LLM response is returned as-is

**OpenAI-Compatible Endpoint (`/v1/chat/completions`):**
1. Standard OpenAI chat completion request arrives with model/temperature/max_tokens
2. Last user message is extracted as the search query
3. Same context-enrichment flow as above (profiles + document search + prompt building)
4. Model resolution: per-request `model` field → `DefaultModel` config → `"auto"`
5. Response is mapped back to OpenAI response format (`OpenAIChatCompletionResponse`)

### Model Resolution Priority

The model used for LLM requests is resolved in this order:
1. **Per-request override** — `model` field in the request body (highest priority)
2. **Configuration** — `AppSettings:FreeLlmApi:DefaultModel` from `appsettings.json`
3. **Fallback** — `"auto"` (router picks the best available model)

### Startup Behavior

- Documents are **loaded on startup**: `await knowledgeService.LoadDocumentsAsync()`
- Swagger UI is available in Development mode at `/swagger`
- Health check endpoint: `GET /health`

## FreeLLM Routing Modes

DeveloperMemory integrates with FreeLLM API's model routing system. Supported routing modes:

| Mode | Description | When to Use |
|---|---|---|
| `auto` | Router picks the best available model | Default — good for general queries |
| `auto:fast` | Router picks the fastest available model | Latency-sensitive requests |
| `auto:smart` | Router picks the most capable available model | Complex reasoning tasks |
| `fusion` | Multiple models answer in parallel, a judge synthesizes one answer | High-quality responses, research |
| Explicit ID | Any model ID from FreeLLM catalog (e.g. `gemini-3.5-flash`) | Pin to a specific model |

### Configuration

Set the default routing mode in `appsettings.json`:
```json
{
  "AppSettings": {
    "FreeLlmApi": {
      "BaseUrl": "http://localhost:3001/v1",
      "ApiKey": "your-api-key",
      "DefaultModel": "auto"
    }
  }
}
```

Override via environment variable: `AppSettings__FreeLlmApi__DefaultModel=auto:fast`

### Per-Request Model Override

Any request can override the configured default by including a `model` field:

**Via `/api/Proxy`:**
```json
{
  "query": "How do I optimize this query?",
  "model": "auto:smart",
  "project": "MyApp"
}
```

**Via `/v1/chat/completions` (OpenAI-compatible):**
```json
{
  "model": "fusion",
  "messages": [
    { "role": "user", "content": "Explain SOLID principles" }
  ],
  "temperature": 0.7,
  "max_tokens": 500
}
```

**Pin to a specific model:**
```json
{
  "model": "gemini-3.5-flash",
  "messages": [
    { "role": "user", "content": "Quick question" }
  ]
}
```

## Complete API Reference

### Knowledge Controller (`/api/Knowledge`)

| Method | Path | Description | Parameters |
|---|---|---|---|
| `GET` | `/api/Knowledge` | Search documents | `query` (string), `project` (string, optional), `tags` (list<string>, optional) |
| `GET` | `/api/Knowledge/documents` | Get all documents | — |
| `GET` | `/api/Knowledge/{id}` | Get document by GUID | `id` (path) |
| `POST` | `/api/Knowledge/reindex` | Reload and reindex all documents | — |
| `POST` | `/api/Knowledge` | Create a new knowledge document | Body: `CreateDocumentRequest` JSON |

**Response:** `SearchResult[]` for search, `KnowledgeDocument[]` for document listing.

**Create Document Request (`CreateDocumentRequest`):**
```json
{
  "title": "My New Document",
  "content": "# Document content here...",
  "project": "MyProject",
  "tags": ["dotnet", "api"]
}
```

### Profiles Controller (`/api/Profiles`)

| Method | Path | Description | Parameters |
|---|---|---|---|
| `GET` | `/api/Profiles` | Get all loaded profiles | — |
| `POST` | `/api/Profiles` | Load a profile from file | Body: `string` (file path) |

**Response:** `DeveloperProfile[]` or `DeveloperProfile`.

### Proxy Controller (`/api/Proxy`)

| Method | Path | Description | Parameters |
|---|---|---|---|
| `POST` | `/api/Proxy` | Forward query to LLM with context | Body: `PromptRequest` JSON |

**Request body (`PromptRequest`):**
```json
{
  "query": "How do I configure Serilog?",
  "project": "MyProject",
  "tags": ["logging", "dotnet"],
  "profileId": "guid-string",
  "systemPrompt": "You are a helpful .NET assistant.",
  "model": "auto:fast"
}
```

### OpenAI-Compatible Controller (`/v1`)

| Method | Path | Description | Parameters |
|---|---|---|---|
| `POST` | `/v1/chat/completions` | OpenAI-compatible chat completion | Body: `OpenAIChatCompletionRequest` |
| `GET` | `/v1/models` | List available models | — |

**Request body (`OpenAIChatCompletionRequest`):**
```json
{
  "model": "auto",
  "messages": [
    { "role": "system", "content": "You are a helpful assistant." },
    { "role": "user", "content": "How do I use dependency injection?" }
  ],
  "temperature": 0.7,
  "max_tokens": 150,
  "project": "MyProject",
  "tags": ["dotnet"],
  "profile_id": "guid-string"
}
```

Non-standard fields (`project`, `tags`, `profile_id`) are extensions for context filtering.

### Sample Requests by Routing Mode

**Auto routing (default):**
```bash
curl -X POST http://localhost:5041/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"auto","messages":[{"role":"user","content":"Hello"}]}'
```

**Fast auto routing:**
```bash
curl -X POST http://localhost:5041/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"auto:fast","messages":[{"role":"user","content":"Quick question"}]}'
```

**Smart auto routing:**
```bash
curl -X POST http://localhost:5041/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"auto:smart","messages":[{"role":"user","content":"Complex architecture question"}]}'
```

**Fusion (multi-model):**
```bash
curl -X POST http://localhost:5041/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"fusion","messages":[{"role":"user","content":"Best practices for .NET"}]}'
```

**Explicit model:**
```bash
curl -X POST http://localhost:5041/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"gemini-3.5-flash","messages":[{"role":"user","content":"Explain async/await"}]}'
```

### Health Check

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Returns `{ "Status": "Healthy", "Timestamp": "..." }` |

## Data Models

### KnowledgeDocument
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Auto-generated unique ID |
| `Title` | `string` | From frontmatter `title` field or filename |
| `Content` | `string` | Markdown body (after frontmatter) |
| `Project` | `string` | From frontmatter `project` field |
| `Tags` | `List<string>` | From frontmatter `tags` field (comma-separated) |
| `FilePath` | `string` | Absolute path to the source .md file |
| `LastModified` | `DateTime` | File last-write timestamp |

### DeveloperProfile
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Auto-generated unique ID |
| `Name` | `string` | From frontmatter `name` field |
| `Role` | `string` | From frontmatter `role` field |
| `Skills` | `List<string>` | From frontmatter `skills` field (comma-separated) |
| `Experience` | `string` | From frontmatter `experience` field |
| `Bio` | `string` | Markdown body (after frontmatter) |
| `FilePath` | `string` | Absolute path to the source .md file |
| `LastModified` | `DateTime` | File last-write timestamp |

### SearchResult
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Document ID |
| `Title` | `string` | Document title |
| `Content` | `string` | Document content |
| `Project` | `string` | Project name |
| `Tags` | `List<string>` | Tags |
| `Score` | `double` | Relevance score (higher = more relevant) |
| `FilePath` | `string` | Source file path |

### PromptRequest
| Property | Type | Description |
|---|---|---|
| `Query` | `string?` | User's question |
| `Project` | `string?` | Filter documents by project |
| `Tags` | `List<string>?` | Filter documents by tags |
| `ProfileId` | `string?` | Developer profile GUID to include as context |
| `SystemPrompt` | `string?` | Custom system instructions for the LLM |
| `Model` | `string?` | Model routing mode override (auto, auto:fast, auto:smart, fusion, or explicit ID) |

### OpenAI Types
- `OpenAIChatCompletionRequest`: Standard fields (`model`, `messages`, `temperature`, `max_tokens`, `stream`) plus extensions (`project`, `tags`, `profile_id`)
- `Message`: `{ role: string, content: string }`
- `OpenAIChatCompletionResponse`: `{ id, object, created, model, choices[], usage }`
- `Choice`: `{ index, message, finish_reason }`
- `Usage`: `{ prompt_tokens, completion_tokens, total_tokens }`
- `OpenAIModel`: `{ id, object, created, owned_by }`
- `OpenAIModelListResponse`: `{ object: "list", data: OpenAIModel[] }`

## Configuration

### appsettings.json Structure

```json
{
  "AppSettings": {
    "FreeLlmApi": {
      "BaseUrl": "http://localhost:3001/v1",
      "ApiKey": "your-api-key",
      "DefaultModel": "auto"
    },
    "Paths": {
      "KnowledgeFolder": "./Knowledge",
      "ProfilesFolder": "./Profiles"
    }
  },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/devmemory-.log", "rollingInterval": "Day", "retainedFileCountLimit": 30 } }
    ]
  }
}
```

### Environment Variable Overrides
Use `__` separator: `AppSettings__FreeLlmApi__ApiKey`, `AppSettings__FreeLlmApi__DefaultModel`, `AppSettings__Paths__KnowledgeFolder`

### Strongly-Typed Settings
- `AppSettings.FreeLlmApi.BaseUrl` — LLM API base URL (must include `/v1` suffix)
- `AppSettings.FreeLlmApi.ApiKey` — Bearer token for LLM API auth
- `AppSettings.FreeLlmApi.DefaultModel` — Default routing mode (auto, auto:fast, auto:smart, fusion, or explicit model ID)
- `AppSettings.Paths.KnowledgeFolder` — Directory for knowledge `.md` files
- `AppSettings.Paths.ProfilesFolder` — Directory for profile `.md` files

## Error Handling

| Status Code | When |
|---|---|
| `200 OK` | Success |
| `400 Bad Request` | Invalid request format, missing fields, or invalid profile file |
| `404 Not Found` | Document ID does not exist |
| `500 Internal Server Error` | LLM API connection failure, file system errors, JSON deserialization errors |

All errors are logged via Serilog to both console and `logs/devmemory-*.log`.

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Serilog.AspNetCore` | 8.0.3 | Structured logging |
| `Serilog.Sinks.Console` | 6.0.0 | Console log output |
| `Serilog.Sinks.File` | 6.0.0 | File log output with rolling |
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | OpenAPI spec generation |
| `Swashbuckle.AspNetCore` | 10.0.1 | Swagger UI |

## Build & Run

```bash
dotnet restore          # Install dependencies
dotnet build            # Build the project
dotnet run              # Run the API (https://localhost:7144 / http://localhost:5041)
dotnet run --project DeveloperMemory.Api  # Run from solution root
```

Swagger UI: `/swagger` (Development mode only)
