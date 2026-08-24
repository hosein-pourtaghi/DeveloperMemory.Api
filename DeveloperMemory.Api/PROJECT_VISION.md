# Project Vision — Developer Memory API

## Mission

Developer Memory API is a persistent context and memory gateway that helps AI development tools retrieve relevant developer and project knowledge and build better AI requests.

## Problem Statement

AI coding assistants are stateless. Every conversation starts from zero. They forget your coding standards, ignore your architectural preferences, and require you to re-explain your project's context in every session. Developers waste time re-explaining the same constraints, and AI suggestions vary wildly because they lack consistent context.

There is no standard way for AI tools to persistently learn and retrieve what matters about a developer, their preferences, or their project's technical decisions.

## Target Users

- **Solo developers** using AI coding assistants who want consistent, context-aware suggestions.
- **Small teams** who share coding standards and project knowledge but have no mechanism to inject that knowledge into AI tools.
- **AI tool builders** who want a reusable memory layer they can integrate into their products.

## Core Value Proposition

Developer Memory API solves this by sitting between AI coding assistants and LLM providers as a transparent proxy. It automatically enriches every AI request with:

1. **Developer profiles** — Who you are, your skills, experience, and working style.
2. **Knowledge documents** — Coding standards, architectural decisions, project rules, and technical context.
3. **Relevant retrieval** — Matching the right knowledge to the current task, not dumping everything into every prompt.

The result: AI that understands your context without you having to repeat yourself, while respecting that your explicit instructions always take priority.

## Core Concepts

### Developer Profiles

Markdown files that describe a developer's skills, experience, role, and preferences. These give the AI a baseline understanding of who it is working with.

### Knowledge Documents

Markdown files with YAML frontmatter containing coding standards, project rules, architectural decisions, and technical guidance. Documents are tagged by project and topic for retrieval.

### Context Enrichment

The gateway intercepts OpenAI-compatible requests, searches for relevant knowledge, and appends it to the system message. The original conversation history and client instructions are preserved and take precedence.

### Transparent Proxy

The gateway exposes a standard OpenAI-compatible API (`/v1/chat/completions`). Any AI client that speaks this protocol works without modification. The gateway forwards enriched requests to any OpenAI-compatible LLM provider.

## What This Is NOT

- **Not an AI model or LLM.** It does not generate responses. It enriches requests before forwarding them.
- **Not a vector database or RAG system.** The current implementation uses keyword-based retrieval. Semantic search is a future goal.
- **Not a replacement for your AI client.** It works alongside your existing tools (Cline, Continue, Cursor, etc.).
- **Not an enterprise platform (yet).** The current scope is a local developer tool. Multi-user, authentication, and team features are future considerations.
- **Not a chat interface.** It is an API gateway, not a UI.

## Long-Term Direction

The project follows a practical, incremental approach:

1. **Foundation (current):** Establish the core architecture, data formats, and a working prototype with keyword-based retrieval and OpenAI-compatible proxying.
2. **V1 (next):** Complete a production-ready local tool with authentication, persistent storage, and improved retrieval.
3. **V2+ (future):** Semantic search with embeddings, multi-developer support, web UI, and potential IDE integrations.

Each phase builds on the previous one. The architecture is designed to be simple now with clear extension points for future capabilities.
