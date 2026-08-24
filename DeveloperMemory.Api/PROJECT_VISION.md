# PROJECT_VISION.md — DeveloperMemory API

## Mission

DeveloperMemory.Api is a persistent context and memory gateway that helps AI development tools retrieve relevant developer and project knowledge and build better AI requests. Its primary purpose is to prevent developers and AI coding assistants from repeatedly needing to rediscover or manually provide important context.

## Problem Statement

When developers use AI coding assistants (Cline, Continue, Copilot, etc.), every conversation starts from zero. The AI has no memory of:

- The developer's coding preferences and conventions
- Project-specific rules, architecture decisions, or coding standards
- Technical documentation that should influence how code is written
- Previous decisions about tooling, patterns, or approaches

This forces developers to repeatedly paste the same context, re-explain conventions, and manually inject project knowledge into every AI interaction. The result is wasted time, inconsistent AI outputs, and fragmented knowledge that never accumulates.

## Target Users

- **Developers** using AI coding assistants (Cline, Continue, Cursor, Copilot, or any OpenAI-compatible client)
- **Teams** that want consistent AI behavior aligned with project standards and shared knowledge
- **Organizations** building internal AI development tooling

## Core Value Proposition

DeveloperMemory.Api sits between AI clients and LLM providers as an intelligent proxy that automatically enriches every request with relevant context. The developer configures their profile and project knowledge once; the gateway applies it to every interaction automatically.

## Core Concepts

### What "Memory" Means

In this project, "memory" refers to persistent, developer-authored knowledge that influences how an AI assistant behaves. It is not autonomous learning, not embeddings, not vector databases. It is structured, human-readable Markdown files that the developer explicitly creates and curates.

### Memory Layers

1. **Developer Profile** — Persistent information about the developer: preferences, coding conventions, technology stack, role, experience level.

2. **Knowledge Documents** — Reusable information about projects, technologies, or domains: coding standards, architecture rules, technical documentation, project-specific instructions.

3. **Project Context** — Scoping information that associates knowledge with specific projects or domains.

4. **Decision / Historical Memory** — *(Future capability)* Storing important decisions, outcomes, and historical context. Not implemented in V1.

### How Memory Is Applied

```
AI Client Request
        ↓
DeveloperMemory Gateway
        ↓
Load Developer Profile(s)
        ↓
Search Knowledge Documents (keyword-based)
        ↓
Assemble Context Block
        ↓
Enrich System Message (append context, preserve history)
        ↓
Forward to LLM Provider
        ↓
Return Response
```

### Instruction Precedence

1. Client's existing system message (preserved and extended, never replaced)
2. DeveloperMemory profile context (appended to system message)
3. Retrieved knowledge context (appended to system message)
4. User messages (preserved as-is)

## What the Project Is Not

- **Not an AI learning system** — It does not learn from interactions or autonomously extract memories.
- **Not a vector database** — V1 uses keyword matching, not embeddings or semantic search.
- **Not a multi-user SaaS** — V1 is a single-developer gateway, not a multi-tenant platform.
- **Not an IDE plugin** — It is a standalone API that any OpenAI-compatible client can use.
- **Not a replacement for LLM providers** — It is a middleware layer, not a model host.

## Long-Term Direction

### V1 (Current)
File-based profiles and knowledge, keyword retrieval, context assembly, prompt enrichment, OpenAI-compatible provider forwarding.

### V2 (Future)
- Persistent database for knowledge and profiles
- Embeddings and vector search for semantic retrieval
- Automatic memory extraction from conversations
- Multi-user and team support
- Enhanced context intelligence (relevance weighting, context budgets)
- Decision and historical memory

### Design Principle

**Simple implementation now, replaceable infrastructure later.** V1 is deliberately minimal. Every component is designed so it can be replaced with a more sophisticated implementation without changing the overall architecture.
