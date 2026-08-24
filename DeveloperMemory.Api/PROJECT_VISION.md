# Project Vision — DeveloperMemory.Api

## Mission

DeveloperMemory.Api automatically injects relevant developer preferences and project knowledge into every AI coding assistant interaction, so developers never have to manually paste the same context again.

## Problem Statement

When developers use AI coding assistants — Cline, Continue, Cursor, Copilot, or any OpenAI-compatible client — every conversation starts from zero. The AI has no awareness of:

- The developer's coding conventions, preferences, or technology stack
- Project-specific rules, architecture decisions, or coding standards
- Technical documentation that should influence how code is written

This forces developers to repeatedly paste identical context into every AI interaction. The result is wasted time, inconsistent AI outputs, and knowledge that never accumulates.

## Solution

DeveloperMemory.Api sits between AI coding assistants and LLM providers. It intercepts every chat completion request, assembles relevant context from developer-authored Markdown files, and appends that context to the request before forwarding it to the LLM provider. The developer configures their profile and project knowledge once; the system applies it to every interaction automatically.

```
AI Coding Assistant (Cline, Continue, etc.)
        │
        ▼
DeveloperMemory.Api
        │
        ├── Load Developer Profiles     (who is the developer?)
        ├── Search Knowledge Documents  (what rules apply?)
        ├── Assemble Context            (what should the AI know?)
        │
        ▼
Enriched Request → LLM Provider → Response → AI Coding Assistant
```

## Target Users

- **Individual developers** using AI coding assistants who want consistent, context-aware AI behavior
- **Teams** that want shared project standards and knowledge applied uniformly across all AI interactions
- **Organizations** building internal AI development tooling with institutional knowledge

## Core Value Proposition

**Configure once, apply everywhere.** Instead of pasting the same coding standards, project rules, and preferences into every AI conversation, developers write them as Markdown files. DeveloperMemory.Api reads these files and automatically includes the relevant context in every request — with zero manual effort after initial setup.

The value is not the LLM proxy. The value is the automatic context assembly and injection. The proxy simply enables the mechanism.

## Core Concepts

This project uses four distinct terms. They have specific, non-overlapping meanings.

### Developer Identity

What the system knows about the developer — their role, skills, experience level, and coding preferences. Stored as Markdown files with YAML frontmatter in the `Profiles/` directory.

Developer Identity answers: *"Who is asking the AI for help?"*

### Project Knowledge

Reusable information about projects, technologies, or domains — coding standards, architecture rules, technical documentation, project-specific instructions. Stored as Markdown files with YAML frontmatter in the `Knowledge/` directory.

Project Knowledge answers: *"What rules and context apply to this project?"*

### Context Assembly

The process of retrieving relevant Developer Identity and Project Knowledge, and building a context block that gets appended to the AI's system message. Currently keyword-based; future versions may add semantic retrieval.

Context Assembly answers: *"Which knowledge is relevant to this specific request?"*

### Provider Integration

The OpenAI-compatible forwarding mechanism that sends the enriched request to an LLM provider and returns the response. This enables DeveloperMemory.Api to sit transparently between any AI client and any OpenAI-compatible provider.

Provider Integration answers: *"How does the enriched request reach the LLM?"*

### What "Memory" Means in This Project

The project name uses "Memory" loosely. In V1, there is no autonomous learning, no embeddings, no vector search, and no cross-session recall. The "memory" is **developer-authored, static Markdown files** that the developer explicitly creates and curates.

The developer writes knowledge. The system retrieves and injects it. That is the complete memory model for V1.

## Non-Goals

The following are deliberately excluded from the product scope:

- **Not an AI learning system** — It does not learn from interactions or extract knowledge autonomously
- **Not a vector database** — V1 uses keyword matching, not embeddings or semantic search
- **Not a multi-user SaaS** — V1 is a single-developer gateway, not a multi-tenant platform
- **Not an IDE plugin** — It is a standalone API; IDE integration is handled by the AI client
- **Not a replacement for LLM providers** — It is middleware, not a model host
- **Not a project management system** — It does not track tasks, issues, or project state
- **Not an autonomous coding agent** — It enriches requests; it does not execute code

## Product Capabilities

| Capability | Description | V1 Status |
|---|---|---|
| Developer identity management | Load developer profiles from Markdown | ✅ Implemented |
| Project knowledge management | Load, search, and create knowledge documents | ✅ Implemented |
| Keyword-based retrieval | Find relevant knowledge using text matching | ✅ Implemented |
| Context assembly | Build context blocks from profiles + knowledge | ✅ Implemented |
| Request enrichment | Append context to system messages, preserve conversation history | ✅ Implemented |
| OpenAI-compatible proxy | Forward enriched requests to any OpenAI-compatible provider | ✅ Implemented |
| Streaming support | Forward SSE streaming responses without buffering | ✅ Implemented |
| Mode detection | Detect plan vs build mode for model selection | ✅ Implemented |
| Token tracking | Log token metrics at each pipeline stage | ✅ Implemented |
| Semantic retrieval | Embedding-based similarity search | ❌ Planned for V2 |
| Cross-session learning | Remember decisions and outcomes from conversations | ❌ Planned for V2 |
| Multi-user support | Team-shared knowledge with access control | ❌ Planned for V2 |

## Design Principle

**Simple implementation now, replaceable infrastructure later.** V1 is deliberately minimal. File-based Markdown storage, keyword search, and a thin proxy layer. Every component is designed so it can be replaced with a more sophisticated implementation without changing the overall architecture.

## Long-Term Direction

### V1 (Current)

File-based developer identity and project knowledge, keyword retrieval, context assembly, request enrichment, OpenAI-compatible provider forwarding.

### V2 (Future)

- Persistent database for knowledge and profiles
- Embeddings and vector search for semantic retrieval
- Automatic memory extraction from conversations
- Enhanced context intelligence (relevance weighting, context budgets)

### V3 (Future)

- Multi-user and team support with access control
- Decision and historical memory
- Cross-session learning and preference refinement
