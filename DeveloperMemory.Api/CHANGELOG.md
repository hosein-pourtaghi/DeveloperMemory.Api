# Changelog

This changelog tracks design milestones, not code releases. The project is currently in the design and documentation phase.

## [Design] - 2026-08-24

### Documentation Audit and Vision Correction
- **Corrected all documentation** to reflect actual repository state (no source code exists)
- **Created PROJECT_VISION.md** with clear mission, problem statement, and scope
- **Created CURRENT_STATUS.md** with honest assessment of what exists vs what is planned
- **Created ROADMAP.md** with phased development plan and clear scope boundaries
- **Standardized terminology** across all documentation
- **Removed false claims** of implemented features from all docs
- **Fixed frontmatter format documentation** to match actual knowledge file formats

## [Design] - 2026-08-21

### Auto Model Selection Design
- Designed mode detection system (plan vs build) based on system prompt analysis
- Designed model routing configuration (`auto:smart`, `auto:fast`)
- Designed `ModelSelectionSettings` configuration schema

### Token Tracking Design
- Designed three-stage token tracking pipeline (incoming → enriched → response)
- Designed `TokenEstimator` (~4 chars/token heuristic)
- Designed `RequestLogger` for console and file output

### Multimodal Content Design
- Designed `MessageContentConverter` for string and array content handling

### Request Logging Design
- Designed `RequestLoggingMiddleware` for debugging

## [Design] - 2026-08-21

### Streaming Design
- Designed SSE streaming support for `/v1/chat/completions`
- Designed streaming response models (`ChatCompletionChunk`)

### Conversation History Preservation
- Designed `PromptBuilder.BuildEnrichedRequest()` to preserve multi-turn messages

### OpenAI-Compatible API Design
- Designed complete request/response models
- Designed error response format
- Designed model lookup endpoint

### Architecture Design
- Designed layered architecture (Presentation → Application → Domain → Infrastructure)
- Designed `GlobalExceptionMiddleware`
- Designed `FreeLlmApiClient` for provider-agnostic HTTP

## [Design] - 2026-08-19

### Documentation Consolidation
- Consolidated project documentation into core reference files
- Created `CLAUDE.md` as comprehensive project reference
- Created `AGENTS.md` as AI agent coding guide

## [Design] - 2026-08-14

### Initial Design
- Defined project vision and core concepts
- Designed knowledge document format (Markdown + YAML frontmatter)
- Designed developer profile format
- Designed keyword-based relevance scoring algorithm
- Designed configuration schema
- Created example knowledge documents and profiles
