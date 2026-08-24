# Developer Memory API

## Overview

Developer Memory API is an **OpenAI-compatible Developer Memory Gateway** — a .NET 10.0 middleware that sits between AI coding assistants (such as Cline) and OpenAI-compatible LLM providers. It enriches AI requests with persistent developer preferences, coding guidelines, project knowledge, and relevant long-term memory.

**The core problem it solves:** AI coding assistants start every conversation with zero context. DeveloperMemory ensures they always have access to your preferences, project rules, and relevant knowledge — automatically.

## How It Works

```
IDE AI Client / Cline
        ↓
DeveloperMemory.Api  (OpenAI-compatible gateway)
        ↓
Load Developer Profile(s)
        ↓
Search Knowledge Documents (keyword-based)
        ↓
Assemble Context & Enrich System Message
        ↓
Mode Detection (Plan vs Build) → Auto Model Selection
        ↓
OpenAI-Compatible Provider (FreeLLM, OpenAI, etc.)
        ↓
Streaming or Standard Response
        ↓
IDE AI Client / Cline
```

## Current Implementation Status

The project is a **working V1 implementation** with the following core capabilities:

- ✅ OpenAI-compatible `/v1/chat/completions` with full streaming support
- ✅ Developer profile loading from Markdown files with YAML frontmatter
- ✅ Knowledge document loading, indexing, and keyword-based search
- ✅ Automatic context enrichment that preserves conversation history
- ✅ Mode detection (plan vs build) with automatic model selection
- ✅ Token tracking and request logging
- ✅ Management APIs for knowledge and profiles
- ✅ Health check endpoint
- ✅ Swagger/OpenAPI documentation

**Not yet implemented:** Tests, CI/CD, authentication, embeddings, vector search, multi-user support.

See [CURRENT_STATUS.md](CURRENT_STATUS.md) for the full implementation inventory.

## Quick Start

```bash
cd DeveloperMemory.Api
dotnet restore
dotnet run
```

- **API**: `http://localhost:5041` / `https://localhost:7144`
- **Swagger UI**: `/swagger` (Development mode)
- **Health Check**: `GET /health`

## Cline Integration

Configure Cline (or any OpenAI-compatible client) with:

| Setting | Value |
|---|---|
| API Base URL | `http://localhost:5041/v1` |
| Model | Any value (gateway auto-selects based on mode) |
| API Key | Any value (not validated) |

The gateway automatically detects whether Cline is in **plan mode** (reasoning/analysis) or **build mode** (code implementation) and routes to the best model for each task.

## Auto Model Selection

When `AutoSelectModel` is enabled, the gateway ignores the client's requested model and selects the optimal model based on detected mode:

| Mode | Detection | Default Model | Purpose |
|---|---|---|---|
| **Plan** | System prompt contains planning indicators (`# TASK`, `Checklist`, `Goal:`) | `auto:smart` | Complex reasoning, architecture planning |
| **Build** | System prompt contains tool definitions (`execute_command`, `write_to_file`) | `auto:fast` | Code implementation, tool execution |
| **Unknown** | No recognizable indicators | Falls back to configured `DefaultModel` | General queries |

Configure in `appsettings.json`:
```json
{
  "AppSettings": {
    "ModelSelection": {
      "AutoSelectModel": true,
      "PlanModel": "auto:smart",
      "BuildModel": "auto:fast"
    }
  }
}
```

Set `AutoSelectModel: false` to let the client control model selection.

## Token Tracking

Every request is logged with token metrics at three stages for comparison:

```
TokenSummary: incoming=~1234 | enriched=~1567 | response=~456 | provider=456 | enrichment_overhead=~333 tokens
```

| Metric | Description |
|---|---|
| `incoming_tokens` | Estimated tokens in Cline's original request |
| `enriched_tokens` | Estimated tokens after DeveloperMemory adds profile + knowledge context |
| `response_tokens` | Estimated tokens in the LLM response |
| `provider_tokens` | Actual token count reported by the provider (if available) |
| `enrichment_overhead` | `enriched - incoming` — the cost of added context |

Logs are written to:
- **Console**: Via Serilog (look for `TokenSummary:` lines)
- **File**: `logs/requests/requests-YYYY-MM-DD.log` (daily files)

## OpenAI-Compatible Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/v1/chat/completions` | POST | Chat completions (streaming + non-streaming) |
| `/v1/models` | GET | List available models |
| `/v1/models/{modelId}` | GET | Get specific model details |

## Management Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/api/Knowledge` | GET | Search knowledge base |
| `/api/Knowledge/documents` | GET | List all documents |
| `/api/Knowledge/{id}` | GET | Get document by ID |
| `/api/Knowledge` | POST | Create a new document |
| `/api/Knowledge/reindex` | POST | Reindex all documents |
| `/api/Profiles` | GET | List developer profiles |
| `/api/Profiles` | POST | Load profile from file |
| `/health` | GET | Health check |

## Streaming Support

DeveloperMemory fully supports OpenAI-compatible streaming via Server-Sent Events (SSE). When a client requests `stream: true`, the response from the downstream provider is forwarded directly to the client without buffering.

## Documentation

- **[PROJECT_VISION.md](PROJECT_VISION.md)** — Mission, problem statement, core concepts, and long-term direction
- **[CURRENT_STATUS.md](CURRENT_STATUS.md)** — Actual implementation inventory based on code audit
- **[ROADMAP.md](ROADMAP.md)** — What's done, what's next, and future plans
- **[CLAUDE.md](CLAUDE.md)** — Complete project reference: architecture, API docs, data models, configuration
- **[AGENTS.md](AGENTS.md)** — AI agent coding guide: standards, extension patterns, gotchas
- **[KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md)** — YAML frontmatter format for documents and profiles

## License

Internal project — see repository for license details.
