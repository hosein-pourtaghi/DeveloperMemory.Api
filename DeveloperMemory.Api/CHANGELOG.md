# Changelog

## [2.5.0] - 2026-08-24

### V1 Verification & Hardening
- **Fixed frontmatter parser colon bug**: Changed from `line.Split(':')` (which truncated values containing `:`) to `line.IndexOf(':')` to preserve the full value after the first colon. Applied to both `KnowledgeService` and `ProfileService`.
- **Fixed profile path security**: Added path containment validation to `POST /api/Profiles` to prevent reading files outside the configured profiles directory.
- **Removed dead code**: Deleted unused `MaxKnowledgeContextLength` constant from `PromptBuilder`, removed legacy `SendPromptAsync` method from `FreeLlmApiClient` (no callers after ProxyController removal).
- **Renamed `PromptRequest.cs`** to `CreateDocumentRequest.cs` to match its actual content.
- **Fixed KNOWLEDGE_FORMAT.md**: Updated document example to use `name:` (matching actual knowledge files) instead of `title:`. Clarified that `name` and `title` are aliases.
- **Updated CURRENT_STATUS.md**: Removed stale colon parser limitation, fixed duplicate `scope` entry.
- **Updated CLAUDE.md**: Removed stale colon parser limitation from limitations list.

## [2.4.0] - 2026-08-24

### Vision & Documentation Correction
- **Rewrote PROJECT_VISION.md** as single source of truth. Fixed terminology drift: replaced ambiguous "memory"/"knowledge"/"context" usage with clear, non-overlapping definitions (Developer Identity, Project Knowledge, Context Assembly, Provider Integration). Explicitly defined what the product is and is not.
- **Rewrote README.md** to lead with the product story (problem → solution → how it works) instead of implementation details.
- **Rewrote CURRENT_STATUS.md** with clear Implemented/Partial/Planned categories based on source code evidence.
- **Rebuilt ROADMAP.md** around product evolution milestones (Foundation → Production-Ready → Semantic Retrieval → Intelligent Context → Team & Enterprise) instead of technical task lists.
- **Rewrote CLAUDE.md** as a clean architecture reference, separated from product vision. Fixed Memory Model table. Corrected AGENTS.md stale "IDs are ephemeral" gotcha.
- **Fixed terminology inconsistency** across all documentation files.

## [2.3.0] - 2026-08-24

### V1 Verification, Testing & Hardening

- **Fixed ID stability**: Document and profile IDs are now deterministic, derived from file paths via SHA-256 hash (`StableIdHelper`). Same file always produces same ID across restarts and reindexes.
- **Removed dead code**: Deleted `PromptRequest` class and legacy `BuildPrompt()` method from `PromptBuilder` (zero consumers after ProxyController removal).
- **Added test project**: Created `DeveloperMemory.Api.Tests` with xUnit + Moq. 50+ unit tests covering:
  - `ModeDetector`: 11 tests for plan/build/unknown detection
  - `TokenEstimator`: token counting accuracy for empty, single, and multi-message requests
  - `PromptBuilder`: context assembly, message preservation, edge cases (no system message, knowledge limiting)
  - `KnowledgeService`: frontmatter parsing, search, document creation, ID stability, reindex
  - `ProfileService`: frontmatter parsing, profile loading, ID stability
  - `StableIdHelper`: deterministic GUID generation, path normalization
- **Defined scope metadata**: `scope` frontmatter field documented as reserved/planned for V1.

## [2.2.0] - 2026-08-24

### Repository Audit & Documentation Alignment
- **Full codebase audit**: Verified actual implementation state (contrary to previous documentation-only claims)
- **Implementation inventory**: All 22 source files catalogued with working/partial/broken classification
- **Code quality fixes**:
  - Removed unused and misleading `BuildModeIndicators` array in `ModeDetector`
  - Fixed frontmatter parser to handle `name` as alias for `title` in knowledge documents
  - Fixed YAML escaping in `CreateDocumentAsync` for titles with special characters

### New Documentation
- **PROJECT_VISION.md**: Mission, problem statement, target users, core concepts, long-term direction
- **CURRENT_STATUS.md**: Actual implementation inventory based on code audit
- **ROADMAP.md**: Completed work, next steps, and future/V2+ plans

### Documentation Updates
- **README.md**: Updated to reflect actual implementation status, added links to new docs
- **CLAUDE.md**: Added memory model section, references to new docs, expanded limitations
- **AGENTS.md**: Added testing checklist items for management APIs, references to new docs

## [2.1.0] - 2026-08-21

### Auto Model Selection
- **Mode detection**: Automatically detects Cline's plan vs build mode from system prompt analysis
- **Plan mode**: Routes to `auto:smart` for complex reasoning, architecture, and planning tasks
- **Build mode**: Routes to `auto:fast` for code implementation and tool execution tasks
- **Configurable**: `AppSettings:ModelSelection` section with `AutoSelectModel`, `PlanModel`, `BuildModel`
- **Override**: Set `AutoSelectModel: false` to let the client control model selection

### Token Tracking
- **Token estimation**: Estimates token counts at each pipeline stage (~4 chars/token heuristic)
- **Three-stage logging**: incoming → enriched → response tokens logged for every request
- **Provider tokens**: Actual token counts from the provider (if available in response)
- **Enrichment overhead**: Shows how many tokens DeveloperMemory adds to each request
- **File logging**: Daily log files at `logs/requests/requests-YYYY-MM-DD.log`
- **Console logging**: TokenSummary lines in Serilog console output

### Multimodal Content Support
- **MessageContentConverter**: Custom JSON converter handles both string and array `content` fields
- Cline's tool result messages with content arrays now deserialize correctly
- Array content is preserved as JSON string and forwarded to downstream provider

### Model Validation Error Handling
- **InvalidModelStateResponseFactory**: ASP.NET model validation errors now return OpenAI-compatible error JSON instead of empty 400 body
- Request body deserialization errors show the actual reason

### Request Logging Middleware
- **RequestLoggingMiddleware**: Diagnostic middleware logs raw request bodies for `/v1/*` POST endpoints
- Helps debug client compatibility issues

## [2.0.0] - 2026-08-21

### Major Changes
- **Streaming support**: Full SSE streaming for `/v1/chat/completions`
- **Preserved conversation history**: PromptBuilder preserves multi-turn message structure
- **Removed redundant ProxyController**: Use `/v1/chat/completions` instead
- **Removed unused ServiceCollectionExtensions**: Dead code cleanup

### OpenAI-Compatible Improvements
- Complete request model with all standard OpenAI parameters
- Streaming response models (ChatCompletionChunk)
- OpenAI-compatible error responses for all `/v1/*` endpoints
- Model lookup endpoint (GET /v1/models/{modelId})
- JsonExtensionData support for forwarding unknown fields

### Architecture
- GlobalExceptionMiddleware for global error handling
- DownstreamProviderException for structured provider errors
- Provider-agnostic FreeLlmApiClient

## [1.1.0] - 2026-08-19
- Consolidated documentation into 4 core files
- Rewrote CLAUDE.md as comprehensive project reference

## [1.0.0] - 2026-08-14
- Initial documentation overhaul
