# Developer Memory API

## Overview

Developer Memory API is a **.NET 10.0 Web API** for knowledge management and AI assistant gateway. It lets developers store/retrieve technical knowledge as Markdown files and query AI models with contextual awareness from those documents and developer profiles.

## Quick Start

```bash
cd DeveloperMemory.Api
dotnet restore
dotnet run
```

- **API**: `https://localhost:7144` / `http://localhost:5041`
- **Swagger UI**: `/swagger` (Development mode)
- **Health Check**: `GET /health`

## Documentation

- **[CLAUDE.md](CLAUDE.md)** — Complete project reference: architecture, API docs, data models, configuration, and error handling
- **[AGENTS.md](AGENTS.md)** — AI agent coding guide: standards, extension patterns, gotchas, and contribution workflow
- **[KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md)** — YAML frontmatter format for documents and profiles

## Core Capabilities

| Feature | Endpoint | Description |
|---|---|---|
| Document Search | `GET /api/Knowledge` | Search knowledge base with relevance scoring |
| Document Management | `GET /api/Knowledge/documents` | List all indexed documents |
| Profile Management | `GET /api/Profiles` | List developer profiles |
| AI Proxy | `POST /api/Proxy` | Query LLM with enriched context from docs + profiles |
| OpenAI-Compatible | `POST /v1/chat/completions` | Drop-in replacement for OpenAI chat API with context injection |
| Model Listing | `GET /v1/models` | List available LLM models |

## Tech Stack

- **Framework**: .NET 10.0 / ASP.NET Core
- **Logging**: Serilog (console + rolling file)
- **API Docs**: Swashbuckle / OpenAPI
- **Data Format**: Markdown with YAML frontmatter
- **External**: OpenAI-compatible LLM API proxy

## License

Internal project — see repository for license details.
