# Developer Memory API

## What Is This?

Developer Memory API is a **persistent, intelligent memory layer for AI applications and agents**. It sits between AI systems and LLM providers as a Memory Intelligence Gateway, automatically enriching requests with relevant context accumulated over time.

**The problem it solves:** AI models are stateless. They forget your preferences, ignore your project context, and require you to re-explain everything in every conversation. Developer Memory solves this by remembering important information once, retrieving it only when relevant, and providing it to AI systems at the right time.

**The vision:** Move AI from stateless responses based only on the current prompt to context-aware responses informed by relevant knowledge accumulated over time.

**The core problem it solves:** AI coding assistants start every conversation with zero context. DeveloperMemory ensures they always have access to your preferences, project rules, and relevant knowledge — automatically.

## How It Works

```
AI Application / Agent
        ↓
DeveloperMemory.Api  (Memory Intelligence Gateway)
        ↓
Load Developer Profile(s)
        ↓
Search Knowledge Documents (keyword-based)
        ↓
Assemble Context & Enrich System Message
        ↓
Mode Detection (Plan vs Build) → Auto Model Selection
        ↓
LLM Request Enrichment
        ↓
Forward to LLM Provider
        ↓
Response back to AI Application
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

## Configuration

Set the downstream LLM provider in `appsettings.json`:

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
    }
  }
}
```

Or use environment variables: `AppSettings__FreeLlmApi__BaseUrl`, `AppSettings__FreeLlmApi__ApiKey`, etc.

## Documentation

- **[PROJECT_VISION.md](PROJECT_VISION.md)** — Mission, problem statement, core concepts, and long-term direction
- **[CURRENT_STATUS.md](CURRENT_STATUS.md)** — Actual implementation inventory based on code audit
- **[ROADMAP.md](ROADMAP.md)** — What's done, what's next, and future plans
- **[CLAUDE.md](CLAUDE.md)** — Complete project reference: architecture, API docs, data models, configuration
- **[AGENTS.md](AGENTS.md)** — AI agent coding guide: standards, extension patterns, gotchas
- **[KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md)** — YAML frontmatter format for documents and profiles

## License

Internal project — see repository for license details.
