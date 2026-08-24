# Current Status

*Last updated: 2026-08-24. Based on source code inspection and test results.*

## Repository

- **Language:** C# (.NET 10.0)
- **Framework:** ASP.NET Core Web API
- **Tests:** xUnit test project with 50+ unit tests
- **CI/CD:** None
- **Docker:** None

---

## Implemented

The following capabilities are fully implemented in source code and covered by unit tests.

### Core Product

| Component | Location | Description |
|---|---|---|
| Developer Identity Loading | `Services/ProfileService.cs` | Loads developer profiles from Markdown files with YAML frontmatter |
| Project Knowledge Loading | `Services/KnowledgeService.cs` | Loads knowledge documents from Markdown files with YAML frontmatter |
| Keyword Retrieval | `Services/KnowledgeService.cs` | Text-based relevance search across knowledge documents |
| Context Assembly | `Services/PromptBuilder.cs` | Builds context blocks from profiles and knowledge; appends to system messages |
| Request Enrichment | `Services/PromptBuilder.cs` | Preserves conversation history while injecting context |
| Stable Identity | `Services/StableIdHelper.cs` | Deterministic GUIDs from file paths (IDs stable across restarts) |

### Provider Integration

| Component | Location | Description |
|---|---|---|
| Chat Completions Controller | `Controllers/OpenAIChatCompletionController.cs` | OpenAI-compatible `/v1/chat/completions` endpoint |
| Model Endpoints | `Controllers/OpenAIChatCompletionController.cs` | `/v1/models` and `/v1/models/{modelId}` |
| LLM Provider Client | `Services/FreeLlmApiClient.cs` | HTTP client for OpenAI-compatible providers |
| Streaming Forwarding | `Services/FreeLlmApiClient.cs` | SSE streaming without buffering |
| Mode Detection | `Services/ModeDetector.cs` | Detects plan vs build mode for model selection |
| Token Tracking | `Services/TokenEstimator.cs` | ~4 chars/token heuristic for logging |
| Request Logging | `Services/RequestLogger.cs` | Three-stage token metrics to console and daily files |

### Management API

| Component | Location | Description |
|---|---|---|
| Knowledge Controller | `Controllers/KnowledgeController.cs` | CRUD, search, and reindex endpoints |
| Profiles Controller | `Controllers/ProfilesController.cs` | List and load profile endpoints |

### Infrastructure

| Component | Location | Description |
|---|---|---|
| Exception Middleware | `Infrastructure/Middleware/GlobalExceptionMiddleware.cs` | OpenAI-compatible error responses |
| Request Logging Middleware | `Infrastructure/Middleware/RequestLoggingMiddleware.cs` | Diagnostic body logging for debugging |
| Configuration | `Infrastructure/Configuration/AppSettings.cs` | Strongly-typed settings |
| OpenAI Type System | `Models/OpenAIRequestResponse.cs` | Full request/response types with `JsonExtensionData` forwarding |
| Content Converter | `Models/MessageContentConverter.cs` | Handles string and array content fields (Cline compatibility) |
| Application Entry | `Program.cs` | DI registration, middleware pipeline, Swagger, health check |

### Data

| Component | Location | Description |
|---|---|---|
| Knowledge Documents | `Knowledge/*.md` | 2 documents with YAML frontmatter (AI rules, code generation rules) |
| Developer Profiles | `Profiles/*.md` | 2 profiles with YAML frontmatter (developer identity, preferences) |

---

## Partially Implemented

| Component | Status | Notes |
|---|---|---|
| Frontmatter `name` field | Works | Knowledge docs use `name:` instead of `title:`; parser handles both |
| Frontmatter `scope` field | Parsed but unused | Present in files but not consumed by any runtime logic. Reserved for future use. |

---

## Planned (V1 remaining)

| Item | Reason |
|---|---|
| Build verification | Requires .NET 10.0 SDK (not available in current environment) |
| Integration tests | Requires .NET SDK for `WebApplicationFactory` |
| Production CORS lockdown | Currently allows all origins |
| Configuration validation | Missing startup validation for required settings |
| Docker support | Not yet added |
| CI/CD pipeline | Not yet added |

---

## Planned (V2+)

These are intentionally not implemented in V1. See [ROADMAP.md](ROADMAP.md).

- Semantic retrieval (embeddings + vector search)
- Cross-session memory extraction
- Multi-user and team support
- Decision and historical memory

---

## Known Limitations

1. **In-memory document cache** — Documents loaded at startup; reindex via `POST /api/Knowledge/reindex`
2. **Keyword search only** — No semantic/vector search; relevance scoring is text-based substring matching
3. **Frontmatter `scope` field** — Parsed but not used by any runtime logic; reserved for future use
4. **No streaming token counts** — Token estimates logged for non-streaming responses only
5. **CORS wide open** — Development-only; needs lockdown for production
6. **No authentication** — Any client can access all endpoints
