# Changelog

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
