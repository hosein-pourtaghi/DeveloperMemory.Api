# Current Status

**Last reviewed:** August 24, 2026

This document provides an honest assessment of what currently exists in the repository versus what is planned.

---

## Repository Reality

**The repository currently contains only documentation and example content.** There is no source code — no `.cs` files, no `.csproj`, no `Program.cs`, no `appsettings.json`, no `.sln` file. The entire repository consists of:

- Markdown documentation files describing the intended architecture
- Example knowledge documents (Markdown with YAML frontmatter)
- Example developer profiles (Markdown with YAML frontmatter)

All previously existing documentation described a fully implemented .NET 10.0 application. This was inaccurate and has been corrected.

---

## What Currently Exists

### Documentation
- `README.md` — Project overview, architecture, and quick start guide
- `PROJECT_VISION.md` — Mission, problem statement, target users, and scope
- `CURRENT_STATUS.md` — This document
- `ROADMAP.md` — Phased development plan
- `KNOWLEDGE_FORMAT.md` — YAML frontmatter format specification for knowledge documents and profiles
- `CHANGELOG.md` — Version history (design milestones, not code releases)
- `AGENTS.md` — AI agent coding guide for when implementation begins

### Example Content
- `Knowledge/ai-agent-rules.md` — Example: global rules for AI coding agent behavior
- `Knowledge/code-generation-rules.md` — Example: standards for AI-generated code quality
- `Profiles/developer-profile.md` — Example: full-stack developer profile
- `Profiles/development-preferences.md` — Example: development preferences and principles

### Data Format Specifications
- Knowledge document format: Markdown + YAML frontmatter (`title`, `project`, `tags`)
- Developer profile format: Markdown + YAML frontmatter (`name`, `role`, `skills`, `experience`)

---

## What Is NOT Implemented

The following features are described in legacy documentation but do not exist as code:

- OpenAI-compatible gateway (`/v1/chat/completions`)
- Auto model selection and mode detection
- Token tracking and logging
- Knowledge document search and retrieval
- Developer profile loading and caching
- Prompt enrichment and context injection
- LLM provider proxying (FreeLlmApiClient)
- Streaming response forwarding
- Management API endpoints (`/api/Knowledge`, `/api/Profiles`)
- Error handling middleware
- Configuration system

**None of these features have been coded.** They are design goals, not implemented functionality.

---

## Design Assets Available

The repository does contain valuable design work that can guide implementation:

1. **Architecture design** — Layered architecture with clear separation of concerns (Presentation → Application → Domain → Infrastructure)
2. **API contract design** — OpenAI-compatible request/response models
3. **Data format specification** — YAML frontmatter format for knowledge and profiles
4. **Retrieval algorithm design** — Keyword-based relevance scoring (title +0.5, content +0.3, project +0.1, tags +0.1)
5. **Configuration schema** — `appsettings.json` structure for provider, paths, and model selection
6. **Dependency injection plan** — Service registration with appropriate lifetimes
7. **Coding standards** — Naming conventions, C# patterns, controller and service conventions
8. **Extension guide** — How to add new modes and knowledge sources

---

## Corrected Terminology

Previous documentation used inconsistent terminology. The following terms are now standardized:

| Term | Definition |
|---|---|
| **Gateway** | The system acts as an OpenAI-compatible proxy that enriches requests |
| **Context enrichment** | The process of adding profile and knowledge context to AI requests |
| **Knowledge document** | A Markdown file with YAML frontmatter containing technical guidance |
| **Developer profile** | A Markdown file with YAML frontmatter describing a developer |
| **Retrieval** | The process of selecting relevant knowledge for a given request |
| **Prompt construction** | Building the enriched system message from profile + knowledge + original content |
| **Transparent proxy** | Works with any OpenAI-compatible client without modification |

---

## Known Limitations (Design Stage)

Even in the planned implementation, these limitations are acknowledged:

- No authentication or multi-user support (local tool only)
- Keyword-based retrieval only (no semantic/vector search in v1)
- In-memory document cache (requires reindex after changes)
- No function/tool call processing (forwarded but not interpreted)
- Approximate token counting (~4 chars/token heuristic)
- CORS open (development only)
