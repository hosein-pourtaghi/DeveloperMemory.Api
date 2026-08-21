# Changelog

## [2.0.0] - 2026-08-21

### Major Changes
- **Streaming support**: Full SSE streaming for `/v1/chat/completions` — response streams directly from downstream provider to client without buffering
- **Preserved conversation history**: PromptBuilder now preserves the original multi-turn message structure; DeveloperMemory context is injected into the system message without flattening conversations
- **Removed redundant ProxyController**: `/api/Proxy` endpoint removed — use `/v1/chat/completions` instead
- **Removed unused ServiceCollectionExtensions**: Dead code cleaned up

### OpenAI-Compatible Improvements
- **Complete request model**: Added `top_p`, `frequency_penalty`, `presence_penalty`, `stop`, `n`, `user`, `stream_options`, `max_completion_tokens` to the request model
- **Streaming response models**: Added `ChatCompletionChunk` and `ChunkChoice` for SSE format
- **OpenAI-compatible error responses**: All `/v1/*` errors return `{ error: { message, type, code, param } }` format
- **Model lookup endpoint**: Added `GET /v1/models/{modelId}` for individual model retrieval
- **Fallback model listing**: `/v1/models` returns configured default model when upstream is unavailable
- **JsonExtensionData support**: Unknown client properties are forwarded to downstream provider without data loss
- **Tool call message support**: `Message` model now includes `tool_calls`, `tool_call_id`, `name` fields

### Architecture
- **GlobalExceptionMiddleware**: Catches unhandled exceptions and returns appropriate error responses (OpenAI-compatible for `/v1/*`, problem details for others)
- **DownstreamProviderException**: Structured exception type for downstream provider HTTP errors with status code and raw content
- **Provider-agnostic design**: FreeLlmApiClient works with any OpenAI-compatible provider, not just FreeLLM

### Security & Configuration
- **Removed hardcoded API key** from `appsettings.json` — use environment variables for secrets
- **Fixed port mismatch**: launchSettings.json now uses port 5041 (HTTP) and 7144 (HTTPS), matching documentation
- **Added HTTPS profile**: launchSettings.json includes both HTTP and HTTPS profiles

### Prompt Enrichment
- **Instruction precedence**: Client system message > DeveloperMemory profile > Knowledge context > User messages
- **Knowledge context limits**: Top 5 relevant documents included, content truncated to 500 chars each
- **Profile context**: Name, role, skills, experience, bio included in system message
- **Non-standard fields preserved**: `project`, `tags`, `profile_id` extensions continue to work

### Documentation
- Updated README.md with Cline integration guide and streaming documentation
- Updated CLAUDE.md with complete API reference, architecture, and configuration
- Updated AGENTS.md with new project structure and coding conventions
- Updated DeveloperMemory.Api.http with streaming examples and model lookup

## [1.1.0] - 2026-08-19
- Consolidated documentation into 4 core files: README.md, CLAUDE.md, AGENTS.md, KNOWLEDGE_FORMAT.md
- Deleted 8 redundant docs: API.md, API_SPECIFICATION.md, DOCUMENTATION.md, DATA_MODELS.md, CONFIGURATION.md, ERROR_HANDLING.md, CONTRIBUTING.md, PROJECT_SUMMARY.md
- Rewrote CLAUDE.md as comprehensive project reference (architecture, API, models, config, error handling)
- Created AGENTS.md with AI agent coding standards, extension patterns, and gotchas
- Fixed KNOWLEDGE_FORMAT.md to accurately reflect actual code parser fields (title, project, tags)
- Fixed DeveloperMemory.Api.http with real project endpoints (was referencing weatherforecast)
- Updated README.md with accurate links and port numbers

## [1.0.0] - 2026-08-14
- Initial documentation overhaul completed
- Created README.md with overview and quick start guide
- Separated architecture and API documentation
- Defined knowledge format in KNOWLEDGE_FORMAT.md
- Added AGENTS.md for coding standards
- Created CONTRIBUTING.md with contribution guidelines
