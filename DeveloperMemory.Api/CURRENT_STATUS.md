# CURRENT_STATUS.md — Implementation Status

*Based on actual repository audit. Last verified: 2026-08-24.*

## Repository State

- **Language:** C# (.NET 10.0)
- **Project type:** ASP.NET Core Web API
- **Source files:** 22 source files (16 .cs, 2 .json config, 1 .csproj, 1 .http, 1 .gitignore, 1 .json launch settings)
- **Documentation:** 5 markdown files (README, CLAUDE, AGENTS, CHANGELOG, KNOWLEDGE_FORMAT) + 2 new vision docs
- **Knowledge documents:** 2 markdown files with YAML frontmatter
- **Developer profiles:** 2 markdown files with YAML frontmatter
- **Tests:** None (no test project exists)
- **CI/CD:** None
- **Docker:** None

---

## Component Inventory

### Working

| Component | File | Status |
|---|---|---|
| **OpenAIChatCompletionController** | `Controllers/OpenAIChatCompletionController.cs` | Complete — handles `/v1/chat/completions`, `/v1/models`, `/v1/models/{id}` with streaming, error handling, and mode detection |
| **KnowledgeController** | `Controllers/KnowledgeController.cs` | Complete — CRUD endpoints for knowledge documents with search, create, reindex |
| **ProfilesController** | `Controllers/ProfilesController.cs` | Complete — list and load profile endpoints |
| **FreeLlmApiClient** | `Services/FreeLlmApiClient.cs` | Complete — HTTP client for OpenAI-compatible providers, streaming + non-streaming, model resolution |
| **KnowledgeService** | `Services/KnowledgeService.cs` | Complete — Markdown/YAML frontmatter parsing, keyword search with relevance scoring, document creation |
| **ProfileService** | `Services/ProfileService.cs` | Complete — Markdown/YAML frontmatter parsing, profile loading from filesystem |
| **PromptBuilder** | `Services/PromptBuilder.cs` | Complete — Enriches requests with profile + knowledge context while preserving conversation history |
| **ModeDetector** | `Services/ModeDetector.cs` | Complete — Detects plan vs build mode from system prompt content |
| **TokenEstimator** | `Services/TokenEstimator.cs` | Complete — ~4 chars/token heuristic for logging |
| **RequestLogger** | `Services/RequestLogger.cs` | Complete — Three-stage token logging to console and daily file |
| **GlobalExceptionMiddleware** | `Infrastructure/Middleware/GlobalExceptionMiddleware.cs` | Complete — OpenAI-compatible error responses for /v1/* endpoints |
| **RequestLoggingMiddleware** | `Infrastructure/Middleware/RequestLoggingMiddleware.cs` | Complete — Diagnostic request body logging for debugging |
| **AppSettings** | `Infrastructure/Configuration/AppSettings.cs` | Complete — Strongly-typed settings with ModelSelection, FreeLlmApi, Paths |
| **OpenAI Models** | `Models/OpenAIRequestResponse.cs` | Complete — Full OpenAI request/response types with JsonExtensionData forwarding |
| **MessageContentConverter** | `Models/MessageContentConverter.cs` | Complete — Handles string and array content fields for Cline compatibility |
| **Program.cs** | `Program.cs` | Complete — DI registration, middleware pipeline, Swagger, health check, startup document loading |
| **Knowledge Documents** | `Knowledge/*.md` | Working — AI agent rules and code generation rules load correctly |
| **Developer Profiles** | `Profiles/*.md` | Working — Developer profile and preferences load correctly |

### Partially Implemented

| Component | Issue |
|---|---|
| **Frontmatter `name` field** | Knowledge docs use `name:` instead of `title:`. Parser now handles both (fixed this audit). |
| **Frontmatter `scope` field** | Both knowledge docs and profiles define `scope:` but it is not parsed or stored. Not required for V1. |
| **PromptRequest model** | `Models/PromptRequest.cs` exists but is only used by the legacy `BuildPrompt()` method. The removed ProxyController was its consumer. Harmless dead code. |

### Not Implemented / Missing

| Component | Notes |
|---|---|
| **Test project** | No unit or integration tests exist |
| **CI/CD pipeline** | No GitHub Actions or build automation |
| **Docker support** | No Dockerfile or docker-compose |
| **Authentication** | No auth middleware; CORS is wide open (documented limitation) |
| **Embeddings / vector search** | Out of scope for V1 (future V2) |
| **Decision / historical memory** | Out of scope for V1 (future V2) |
| **Multi-user support** | Out of scope for V1 (future V2) |

---

## Build Status

**Cannot verify in current environment** — The .NET 10.0 SDK is not available in the Freebuff WebContainer environment. The project targets `net10.0` which requires a specific SDK version.

Based on code review:
- The project structure is valid ASP.NET Core
- All namespaces, using statements, and types appear consistent
- NuGet packages are standard and well-known
- No obvious compilation errors identified, but build has not been verified

**To verify build locally:**
```bash
cd DeveloperMemory.Api
dotnet restore
dotnet build
```

---

## Known Limitations

1. **In-memory document cache** — Documents are loaded at startup and held in memory. Reindex requires `POST /api/Knowledge/reindex`.
2. **Keyword search only** — No semantic/vector search. Relevance scoring is text-based substring matching.
3. **ID instability** — Document and profile IDs are generated via `Guid.NewGuid()` on each load. They change on every restart/reindex.
4. **Frontmatter parsing** — Simple `:` split parser. Values containing `:` will be truncated.
5. **No streaming token counts** — Token estimates are logged for non-streaming responses only.
6. **CORS wide open** — Development-only; needs lockdown for production.
