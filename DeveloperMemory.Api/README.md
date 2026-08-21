# Developer Memory API

## Overview

Developer Memory API is an **OpenAI-compatible Developer Memory Gateway** — a .NET 10.0 middleware that sits between AI coding assistants (such as Cline) and OpenAI-compatible LLM providers. It enriches AI requests with persistent developer preferences, coding guidelines, project knowledge, and relevant long-term memory.

## How It Works

```
IDE AI Client / Cline
        ↓
DeveloperMemory.Api  (OpenAI-compatible gateway)
        ↓
Developer Profile + Knowledge Retrieval + Prompt Enrichment
        ↓
OpenAI-Compatible Provider (FreeLLM, OpenAI, etc.)
        ↓
Streaming or Standard Response
        ↓
IDE AI Client / Cline
```

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
| Model | `auto` (or any model your provider supports) |
| API Key | Your provider's API key (if required) |

Cline sends standard OpenAI chat completion requests. DeveloperMemory enriches them with your developer profile and relevant knowledge before forwarding to the configured LLM provider. Streaming responses are passed through transparently.

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

DeveloperMemory fully supports OpenAI-compatible streaming via Server-Sent Events (SSE). When a client requests `stream: true`, the response from the downstream provider is forwarded directly to the client without buffering. This ensures low-latency token-by-token delivery for coding assistants.

## Memory and Prompt Enrichment

DeveloperMemory enriches requests using:

1. **Developer Profiles** — Your name, role, skills, experience, and coding philosophy
2. **Knowledge Documents** — Technical documentation stored as Markdown files with YAML frontmatter
3. **Relevant Memory** — Automatically retrieved based on the user's query using keyword relevance scoring

Enriched context is injected into the system message, preserving the original conversation history. Client system messages are preserved and extended, not replaced.

## Tech Stack

- **Framework**: .NET 10.0 / ASP.NET Core
- **Logging**: Serilog (console + rolling file)
- **API Docs**: Swashbuckle / OpenAPI
- **Data Format**: Markdown with YAML frontmatter
- **External**: OpenAI-compatible LLM API proxy

## Documentation

- **[CLAUDE.md](CLAUDE.md)** — Complete project reference: architecture, API docs, data models, configuration, and error handling
- **[AGENTS.md](AGENTS.md)** — AI agent coding guide: standards, extension patterns, gotchas, and contribution workflow
- **[KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md)** — YAML frontmatter format for documents and profiles

## License

Internal project — see repository for license details.
