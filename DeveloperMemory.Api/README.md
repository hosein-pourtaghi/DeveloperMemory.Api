# DeveloperMemory.Api

**Automatically inject your coding preferences and project knowledge into every AI assistant interaction.**

DeveloperMemory.Api sits between AI coding assistants (Cline, Continue, Cursor, Copilot) and LLM providers. It enriches every chat completion request with relevant context from developer-authored Markdown files — so you never have to paste the same coding standards, project rules, or preferences into every AI conversation again.

## The Problem

Every AI coding conversation starts from zero. The AI doesn't know your coding style, your project's architecture rules, or your team's standards. You paste the same context over and over.

## How It Works

```
AI Coding Assistant          DeveloperMemory.Api           LLM Provider
      │                              │                          │
      │  POST /v1/chat/completions   │                          │
      │─────────────────────────────>│                          │
      │                              │  Load your profile       │
      │                              │  Search project knowledge│
      │                              │  Enrich system message   │
      │                              │                          │
      │                              │  Forward enriched request│
      │                              │─────────────────────────>│
      │                              │                          │
      │                              │  Stream/standard response│
      │  Response                    │<─────────────────────────│
      │<─────────────────────────────│                          │
```

1. **Write your context once** — Create Markdown files with your preferences and project rules
2. **Point your AI client at DeveloperMemory** — Configure your AI assistant's API base URL
3. **Context is injected automatically** — Every request gets enriched with relevant knowledge

## Quick Start

```bash
cd DeveloperMemory.Api
dotnet restore
dotnet run
```

- **API**: `http://localhost:5041`
- **Swagger UI**: `/swagger` (Development mode)
- **Health Check**: `GET /health`

## Configure Your AI Client

| Setting | Value |
|---|---|
| API Base URL | `http://localhost:5041/v1` |
| Model | Any value (gateway auto-selects based on task mode) |
| API Key | Any value (not validated in V1) |

## Adding Context

### Developer Profile

Create a Markdown file in `Profiles/`:

```markdown
---
name: Your Name
role: Backend Developer
skills: C#, ASP.NET Core, TypeScript
experience: 5+ years
---

# Your Profile

Your coding preferences and background...
```

### Project Knowledge

Create a Markdown file in `Knowledge/`:

```markdown
---
name: Coding Standards
project: MyApp
tags: standards, dotnet
---

# Coding Standards

Always use PascalCase for public members...
Prefer composition over inheritance...
```

After adding files, call `POST /api/Knowledge/reindex` to reload.

## What Happens Under the Hood

When your AI client sends a chat completion request:

1. **Mode Detection** — Detects whether the AI is planning or building, and selects the optimal model
2. **Context Assembly** — Loads your profile and searches knowledge documents for relevance
3. **Request Enrichment** — Appends context to the system message while preserving your conversation history
4. **Provider Forwarding** — Sends the enriched request to the LLM provider
5. **Response Streaming** — Returns the provider's response (streaming or non-streaming)

## API Endpoints

### AI Client Endpoints (OpenAI-compatible)

| Endpoint | Method | Description |
|---|---|---|
| `/v1/chat/completions` | POST | Chat completions with automatic context enrichment |
| `/v1/models` | GET | List available models |
| `/v1/models/{modelId}` | GET | Get model details |

### Management Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/api/Knowledge` | GET | Search knowledge documents |
| `/api/Knowledge/documents` | GET | List all knowledge documents |
| `/api/Knowledge/{id}` | GET | Get a document by ID |
| `/api/Knowledge` | POST | Create a new knowledge document |
| `/api/Knowledge/reindex` | POST | Reload all documents from disk |
| `/api/Profiles` | GET | List developer profiles |
| `/health` | GET | Health check |

## Documentation

- **[PROJECT_VISION.md](PROJECT_VISION.md)** — Why this project exists, what it is, and where it's going
- **[CURRENT_STATUS.md](CURRENT_STATUS.md)** — What's implemented, what's partial, what's planned
- **[ROADMAP.md](ROADMAP.md)** — Product evolution milestones
- **[CLAUDE.md](CLAUDE.md)** — Architecture, API reference, configuration, data models
- **[AGENTS.md](AGENTS.md)** — Coding standards and extension patterns for contributors
- **[KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md)** — YAML frontmatter format reference

## License

Internal project — see repository for license details.
