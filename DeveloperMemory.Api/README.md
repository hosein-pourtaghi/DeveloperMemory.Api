# Developer Memory API

## What Is This?

Developer Memory API is a **persistent context and memory gateway** for AI coding assistants. It sits between your IDE's AI client (Cline, Continue, Cursor, etc.) and OpenAI-compatible LLM providers, automatically enriching every request with your developer profile, coding standards, and project knowledge.

**The problem it solves:** AI assistants are stateless. They forget your preferences, ignore your coding standards, and require you to re-explain your project context in every session. Developer Memory gives AI persistent, relevant context so suggestions are consistent and informed.

## How It Works

```
IDE AI Client (Cline, Continue, etc.)
        |
        v
DeveloperMemory.Api  (OpenAI-compatible gateway)
        |
        v
Load Developer Profile + Search Knowledge
        |
        v
Enrich system message with context
        |
        v
Forward to LLM Provider (OpenAI, FreeLLM, etc.)
        |
        v
Response back to IDE AI Client
```

The gateway is transparent — it speaks the standard OpenAI API protocol, so your AI client works without modification.

## Project Status

**Current state: Design and documentation complete. Implementation not yet started.**

The repository contains:
- Architecture design and API contract specifications
- Data format specifications for knowledge documents and developer profiles
- Example knowledge documents and developer profiles
- Coding standards and extension guides

See [CURRENT_STATUS.md](CURRENT_STATUS.md) for details.

## Design Documents

| Document | Purpose |
|---|---|
| [PROJECT_VISION.md](PROJECT_VISION.md) | Mission, problem statement, target users, core value |
| [CURRENT_STATUS.md](CURRENT_STATUS.md) | What exists vs what is planned |
| [ROADMAP.md](ROADMAP.md) | Phased development plan |
| [KNOWLEDGE_FORMAT.md](KNOWLEDGE_FORMAT.md) | YAML frontmatter format for documents and profiles |
| [CHANGELOG.md](CHANGELOG.md) | Design milestone history |
| [AGENTS.md](AGENTS.md) | AI agent coding guide for implementation |

## Planned Architecture

The intended architecture is a layered .NET 10.0 application:

- **Presentation layer:** OpenAI-compatible controllers and error handling middleware
- **Application layer:** Prompt builder, knowledge retrieval, profile loading, LLM client
- **Domain layer:** Data models for knowledge documents, developer profiles, and OpenAI types
- **Infrastructure layer:** Configuration, logging, file system access

## Core Concepts

### Developer Profiles

Markdown files describing who you are — your skills, experience, role, and preferences. Example:

```markdown
---
name: Jane Smith
role: Senior Backend Developer
skills: C#, ASP.NET Core, Docker, PostgreSQL
experience: 8 years
---

Senior backend developer specializing in the .NET ecosystem...
```

### Knowledge Documents

Markdown files with YAML frontmatter containing coding standards, project rules, and technical guidance. Example:

```markdown
---
title: "Code Generation Rules"
project: "MyApp"
tags: coding-standards, quality
---

# Code Generation Rules
Generated code should be production-quality...
```

### Context Enrichment

When a request arrives, the gateway:
1. Loads your developer profile
2. Searches knowledge for documents relevant to the current task
3. Appends profile + knowledge to the system message
4. Preserves your original conversation history
5. Forwards the enriched request to the LLM provider

Your explicit instructions always take priority over injected context.

## Quick Start (When Implementation Begins)

```bash
cd DeveloperMemory.Api
dotnet restore
dotnet run
```

The API will be available at:
- **HTTP:** `http://localhost:5041`
- **Swagger UI:** `/swagger` (Development mode)
- **Health Check:** `GET /health`

## Cline Integration (Planned)

| Setting | Value |
|---|---|
| API Base URL | `http://localhost:5041/v1` |
| Model | Any value (gateway handles selection) |
| API Key | Any value (not validated in local mode) |

## Limitations

- No source code yet — this is a design-phase repository
- Keyword-based retrieval planned for v1 (semantic search is v2+)
- Local tool only — no authentication or multi-user support planned initially
- No persistent storage in v1 (in-memory, requires reindex after changes)

## License

Internal project — see repository for license details.
