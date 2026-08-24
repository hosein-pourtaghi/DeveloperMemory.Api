# Developer Memory API

## What Is This?

Developer Memory API is a **persistent, intelligent memory layer for AI applications and agents**. It sits between AI systems and LLM providers as a Memory Intelligence Gateway, automatically enriching requests with relevant context accumulated over time.

**The problem it solves:** AI models are stateless. They forget your preferences, ignore your project context, and require you to re-explain everything in every conversation. Developer Memory solves this by remembering important information once, retrieving it only when relevant, and providing it to AI systems at the right time.

**The vision:** Move AI from stateless responses based only on the current prompt to context-aware responses informed by relevant knowledge accumulated over time.

## How It Works

```
AI Application / Agent
        ↓
DeveloperMemory.Api  (Memory Intelligence Gateway)
        ↓
Memory Retrieval + Relevance Ranking
        ↓
Context Construction
        ↓
LLM Request Enrichment
        ↓
Forward to LLM Provider
        ↓
Response back to AI Application
```

The gateway is transparent — it speaks the standard OpenAI API protocol, so any AI client works without modification.

## Core Concepts

| Concept | Description |
|---|---|
| **Memory Capture** | Detect valuable information from interactions (preferences, decisions, constraints, goals) |
| **Memory Classification** | Categorize by type (preference, instruction, constraint, goal, decision, etc.) |
| **Memory Lifecycle** | Track state changes (active → updated → superseded → expired → archived) |
| **Memory Retrieval** | Find and rank memories relevant to the current request |
| **Context Construction** | Build token-aware context packages for AI requests |
| **Memory Scopes** | Global, User, Project, Conversation, Session, Agent |

## Current Implementation

This repository contains a **working .NET 10.0 implementation** of the memory gateway's core infrastructure:

### What Works Now

| Feature | Status | Description |
|---|---|---|
| OpenAI-compatible API | ✅ Working | `/v1/chat/completions` with streaming and non-streaming |
| Auto model selection | ✅ Working | Detects plan vs build mode, routes to optimal model |
| Developer profiles | ✅ Working | Markdown profiles with YAML frontmatter |
| Knowledge base | ✅ Working | Markdown knowledge documents with keyword relevance scoring |
| Context enrichment | ✅ Working | Appends profile + knowledge to system messages |
| LLM provider proxy | ✅ Working | Forwards to any OpenAI-compatible provider |
| Token tracking | ✅ Working | Estimates tokens at three pipeline stages |
| Management API | ✅ Working | CRUD for knowledge documents and profiles |
| Streaming | ✅ Working | Full SSE streaming support |
| Error handling | ✅ Working | OpenAI-compatible error responses |
| Request logging | ✅ Working | Diagnostic middleware and daily log files |

### What's Not Yet Implemented

| Feature | Status | Description |
|---|---|---|
| Memory capture pipeline | ❌ Not started | Automatic extraction from conversations |
| Memory classification | ❌ Not started | Categorization by type and lifetime |
| Memory lifecycle management | ❌ Not started | State tracking (active, superseded, expired) |
| Memory scopes | ❌ Partial | Current scopes: global, project. Missing: user, session, agent |
| Semantic search | ❌ Not started | Embedding-based retrieval |
| Persistent storage | ❌ Not started | Currently in-memory only |
| Authentication | ❌ Not started | No auth, local tool only |
| Multi-developer support | ❌ Not started | Single-user only |

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

| Document | Purpose |
|---|---|
| [PROJECT_VISION.md](PROJECT_VISION.md) | Vision, problem statement, core responsibilities, memory model |
| [CURRENT_STATUS.md](CURRENT_STATUS.md) | What works, what's planned, known limitations |
| [ROADMAP.md](ROADMAP.md) | Phased development plan toward full memory intelligence |
| [CLAUDE.md](CLAUDE.md) | Complete technical reference: architecture, API, data models |
| [AGENTS.md](AGENTS.md) | AI agent coding guide |
| [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) | YAML frontmatter format for documents and profiles |

## Limitations

- **No authentication** — CORS is open, no auth middleware. Local development only.
- **Keyword search only** — No semantic/vector search. Relevance scoring is text-based.
- **In-memory cache** — Documents loaded on startup. Reindex via `POST /api/Knowledge/reindex`.
- **No memory capture** — Currently requires manual knowledge creation. Automatic extraction is planned.
- **No memory lifecycle** — No automatic update, superseding, or expiration of memories.
- **Single scope** — Only global and project scopes implemented. User, session, and agent scopes are planned.
- **No function/tool calling** — Tool call messages are forwarded but not processed.
- **Approximate token counts** — ~4 chars/token heuristic.

## License

Internal project — see repository for license details.
