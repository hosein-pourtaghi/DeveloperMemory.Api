# Knowledge and Profile Format — YAML Frontmatter Reference

*Last updated: 2026-08-25*

---

## Overview

DeveloperMemory.Api uses two types of context sources:

1. **Knowledge Documents** — Markdown files with YAML frontmatter in the `Knowledge/` directory
2. **Developer Profiles** — Markdown files with YAML frontmatter in the `Profiles/` directory

These are the legacy V1 context sources and remain active. They coexist with the persistent memory system (PostgreSQL-backed `MemoryEntry`). Both knowledge documents and persistent memories are retrieved and injected into AI request context by the gateway.

---

## Knowledge Document Format

**Location:** `Knowledge/` directory

```markdown
---
title: "AI Agent Rules"
project: ""
tags: ai-agent, coding-standards, rules
---

# AI Agent Rules

Content of the knowledge document...
```

### Supported Frontmatter Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `title` | string | No | Document title. Falls back to `name`, then filename. |
| `name` | string | No | Alias for `title`. Used for backward compatibility. |
| `project` | string | No | Project name for project-scoped filtering |
| `tags` | string (comma-separated) | No | Tags for filtering |
| `scope` | string | No | Scope identifier (parsed but not currently used) |

### How Fields Are Parsed

The frontmatter parser splits on `:` and reads key-value pairs:

- `title` and `name` both map to the document title (name is an alias)
- `project` is stored for project filtering
- `tags` are parsed as comma-separated values
- `scope` is present in existing documents but is **not** currently parsed or stored

### Example Documents in This Repository

| File | Title | Project | Tags |
|---|---|---|---|
| `ai-agent-rules.md` | AI Agent Rules | (empty) | ai-agent, coding-standards, rules |
| `code-generation-rules.md` | Code Generation Rules | (empty) | code-generation, quality, standards |

### Important Notes

- The parser splits on `:` and uses the first two segments only. Values containing `:` may be truncated.
- Files without valid frontmatter (missing `---` delimiters) are still loaded — title defaults to the filename.
- Only `.md` files are loaded.
- Search uses keyword matching with relevance scoring: title match (+0.5), content match (+0.3), project match (+0.1), tag match (+0.1).

---

## Developer Profile Format

**Location:** `Profiles/` directory

```markdown
---
name: Developer
scope: global
role: Full-Stack Developer
experience: 5+ years
---

# Developer Profile

Bio and description content...
```

### Supported Frontmatter Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `name` | string | No | Developer's name or profile name |
| `role` | string | No | Professional role/title |
| `experience` | string | No | Years or description of experience |
| `skills` | string (comma-separated) | No | Comma-separated skill list |
| `scope` | string | No | Scope identifier (present but not actively used) |

### How Fields Are Parsed

- `name` maps to `DeveloperProfile.Name`
- `role` maps to `DeveloperProfile.Role`
- `experience` maps to `DeveloperProfile.Experience`
- `skills` is parsed as comma-separated into `DeveloperProfile.Skills` (List<string>)
- The content after the frontmatter block becomes `DeveloperProfile.Bio`

### Example Profiles in This Repository

| File | Name | Role | Experience |
|---|---|---|---|
| `developer-profile.md` | Developer | Full-Stack Developer | 5+ years |
| `development-preferences.md` | Development Preferences | — | — |

### Important Notes

- Files without valid frontmatter (missing `---` delimiters) are skipped and return null.
- Only `.md` files are loaded.
- The profile system is file-based; profiles are loaded at startup and cached in memory.

---

## Relationship to Persistent Memory

The knowledge and profile systems are **context sources** used by the gateway. They coexist with the PostgreSQL-backed persistent memory system:

| Feature | Knowledge Documents | Developer Profiles | Persistent Memory |
|---|---|---|---|
| Storage | Markdown files on disk | Markdown files on disk | PostgreSQL database |
| Parsing | YAML frontmatter | YAML frontmatter | Structured entity |
| Search | Keyword matching | Loaded entirely | Keyword search with tags |
| Lifecycle | Manual file editing | Manual file editing | Full lifecycle management |
| Project scoping | Via `project` field | No | Via `ProjectId` + `MemoryScope` |
| API | `/api/Knowledge` | `/api/Profiles` | `/api/Memory` |

All three sources are retrieved and injected into the prompt by `PromptBuilder` during request enrichment. The instruction precedence is:

1. Client's existing system message (preserved, never replaced)
2. Persistent memory context (MemoryEntry results)
3. Developer profile context (file-based)
4. Knowledge document context (file-based, keyword search)
5. User messages (preserved as-is)

---

## Design Notes

The frontmatter format is intentionally simple. As the project evolves:

1. The persistent memory system (`MemoryEntry`) provides a richer, database-backed alternative for structured context.
2. Knowledge documents remain useful for curated, human-authored reference material.
3. Future retrieval may unify multiple context sources under a single intelligent retrieval layer.
