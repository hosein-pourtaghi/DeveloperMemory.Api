# Changelog

All notable changes to the DeveloperMemory.Api project are documented here.

## [2.2.0] - 2026-08-24

### Bug Fixes
- **Fixed frontmatter parsing colon bug** in `KnowledgeService.cs` and `ProfileService.cs` — `line.Split(':')` was truncating values containing colons (e.g., URLs). Now uses `string.Join(":", parts.Skip(1))` to preserve full values.
- **Removed hardcoded API key** from `appsettings.json` — the FreeLLM API key was committed in plaintext. Now empty by default; must be provided via environment variable or configuration.

### Knowledge Files
- **Fixed knowledge document frontmatter** — Changed `title`/`project`/`tags` fields to `name`/`scope` to match the actual parser implementation.

### Documentation
- **Complete documentation audit and rewrite** — All documentation now accurately reflects the working implementation.
- **Created PROJECT_VISION.md** — Clear mission, problem statement, target users, and scope.
- **Created CURRENT_STATUS.md** — Honest assessment of what works, what's partially implemented, and what's planned.
- **Created ROADMAP.md** — Phased development plan with clear scope boundaries.
- **Rewrote README.md** — Accurately describes current state and links to detailed docs.
- **Rewrote CHANGELOG.md** — Separates design milestones from code releases.
- **Rewrote KNOWLEDGE_FORMAT.md** — Documents actual frontmatter format with parser behavior notes.
- **Updated AGENTS.md and CLAUDE.md** — Reflects working source code, not design-phase docs.

## [2.1.0] - 2026-08-24

### Documentation Phase 2
- Rewrote all documentation to be consistent and forward-looking
- Created design milestone history

## [2.0.0] - 2026-08-24

### Documentation Phase 1
- Consolidated project documentation into core reference files
- Created `CLAUDE.md` as comprehensive technical reference
- Created `AGENTS.md` as AI agent coding guide

## [1.0.0] - 2026-08-21

### Initial Design and Implementation
- Created .NET 10.0 project structure with ASP.NET Core Web API
- Implemented OpenAI-compatible `/v1/chat/completions` endpoint with streaming
- Implemented `/v1/models` and `/v1/models/{id}` endpoints
- Implemented Knowledge and Profiles management APIs
- Implemented Markdown + YAML frontmatter parsing for knowledge documents and profiles
- Implemented keyword-based relevance scoring for knowledge retrieval
- Implemented prompt/context enrichment with instruction precedence
- Implemented transparent request forwarding to OpenAI-compatible providers
- Implemented developer profile loading and caching
- Implemented auto model selection (plan vs build mode)
- Implemented token estimation and request logging
- Implemented multimodal content forwarding (string and array content)
- Implemented SSE streaming support
- Implemented global exception middleware and request logging middleware
- Created example knowledge documents and developer profiles
