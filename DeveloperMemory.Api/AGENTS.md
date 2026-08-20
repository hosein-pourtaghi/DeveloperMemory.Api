# AGENTS.md — AI Agent Coding Guide

This document is for AI coding agents working on the Developer Memory API. It covers coding standards, project conventions, and how to safely extend the codebase.

## Project Structure

```
DeveloperMemory.Api/
├── Controllers/           # API endpoints (4 files)
│   ├── KnowledgeController.cs       # /api/Knowledge
│   ├── ProfilesController.cs        # /api/Profiles
│   ├── ProxyController.cs           # /api/Proxy
│   └── OpenAIChatCompletionController.cs  # /v1/chat/completions, /v1/models
├── Services/              # Business logic (4 files)
│   ├── KnowledgeService.cs          # Document parsing, search, indexing
│   ├── ProfileService.cs            # Profile parsing and loading
│   ├── PromptBuilder.cs             # Constructs LLM prompts with context
│   └── FreeLlmApiClient.cs          # HTTP client for external LLM API
├── Models/                # Data structures (5 files)
│   ├── KnowledgeDocument.cs         # Document model
│   ├── DeveloperProfile.cs          # Profile model
│   ├── PromptRequest.cs             # Proxy request model
│   ├── SearchResult.cs              # Search result model
│   └── OpenAIRequestResponse.cs     # All OpenAI-compatible types
├── Infrastructure/
│   ├── Configuration/
│   │   └── AppSettings.cs           # Strongly-typed settings
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs  # DI helper (currently unused in Program.cs)
├── Knowledge/             # Markdown files (knowledge documents)
├── Profiles/              # Markdown files (developer profiles)
├── Program.cs             # Application entry point and DI setup
├── appsettings.json       # Main configuration
└── appsettings.Development.json
```

## Coding Standards

### Naming
- **PascalCase** for: classes, methods, properties, public fields, namespaces
- **camelCase** for: local variables, parameters, method arguments
- **`_camelCase`** for: private fields (prefixed with underscore)

### File Organization
- One class per file (exception: OpenAI types in `OpenAIRequestResponse.cs` are grouped)
- File names match class names exactly
- Use file-scoped namespaces: `namespace DeveloperMemory.Api.Controllers;`

### C# Patterns
- Use **nullable reference types** (project has `<Nullable>enable</Nullable>`)
- Use `?.` and `??` operators for null safety
- Use `string.Empty` instead of `""` for default strings
- Use `List<T>` not arrays for collection properties
- Use `Guid` for IDs, auto-generated in model defaults

### Controller Conventions
- Decorate with `[ApiController]` and `[Route("api/[controller]")]`
- Use `[FromQuery]` for query parameters, `[FromBody]` for request bodies
- Return `ActionResult<T>` for type-safe responses
- Wrap external calls in try-catch, return `StatusCode(500, message)` on failure
- Use Serilog `_logger` for error logging in controllers that make external calls

### Service Conventions
- Services read config from `IConfiguration` or `IOptions<AppSettings>`
- Services use constructor injection
- `KnowledgeService` and `ProfileService` hold in-memory caches (`_documents`, profiles loaded on each call)
- External HTTP calls go through `FreeLlmApiClient` which manages auth headers

### Documentation
- Add XML doc comments to public methods (project has `<GenerateDocumentationFile>true</GenerateDocumentationFile>`)
- Keep documentation files in sync when adding features

## How to Extend

### Adding a New Controller
1. Create `YourController.cs` in `Controllers/`
2. Decorate with `[ApiController]` and `[Route("api/[controller]")]`
3. Inject services via constructor
4. Register any new services in `Program.cs` DI

### Adding a New Service
1. Create `YourService.cs` in `Services/`
2. Add registration in `Program.cs`:
   - `builder.Services.AddSingleton<YourService>();` for stateful services
   - `builder.Services.AddHttpClient<YourService>();` for HTTP clients
3. Inject into controllers via constructor

### Adding a New Model
1. Create `YourModel.cs` in `Models/`
2. Use file-scoped namespace: `namespace DeveloperMemory.Api.Models;`
3. Auto-generate IDs: `public Guid Id { get; set; } = Guid.NewGuid();`
4. Initialize collections: `public List<string> Items { get; set; } = new();`
5. Use `string.Empty` as default for string properties

### Adding Configuration
1. Add property to `AppSettings.cs` (or nested settings class)
2. Add corresponding JSON in `appsettings.json` under `AppSettings`
3. Inject via `IOptions<AppSettings>` where needed

## Key Gotchas

1. **Singleton services + filesystem**: `KnowledgeService` holds documents in memory. Changes to `.md` files require a reindex (`POST /api/Knowledge/reindex`) to take effect.

2. **Frontmatter parsing is simple**: The parser splits on `:` and takes only the first two segments. Values containing `:` will be truncated. Tags are comma-separated on a single line only (not YAML arrays).

3. **IDs are ephemeral**: Document and profile `Id` fields are `Guid.NewGuid()` — they change every time documents are reloaded. Use `FilePath` for stable identification.

4. **`ServiceCollectionExtensions.cs` is not used**: `Program.cs` registers services directly. The extension class exists but is not called.

5. **OpenAI endpoint extends the standard**: The `/v1/chat/completions` endpoint accepts non-standard fields (`project`, `tags`, `profile_id`) for context filtering.

6. **PromptBuilder truncates content**: Search results in prompts are truncated to 200 characters (`result.Content.Substring(0, Math.Min(200, result.Content.Length))`).

7. **`FreeLlmApiClient` uses model `gpt-3.5-turbo` by default**: The model is hardcoded in `SendPromptAsync` and not configurable per-request.

8. **CORS is wide open**: `AllowAll` policy allows any origin, method, and header. This is for development only.

## Testing Checklist

When making changes, verify:
- [ ] `dotnet build` succeeds with no warnings
- [ ] `GET /health` returns healthy
- [ ] `GET /api/Knowledge` returns documents (or empty list)
- [ ] `GET /api/Profiles` returns profiles (or empty list)
- [ ] `GET /swagger` loads in Development mode
- [ ] `POST /v1/chat/completions` returns OpenAI-format response

## Git Workflow

- Create feature branches from `main`
- Write descriptive commit messages
- Keep commits focused (one logical change per commit)
- Create pull requests for review before merging
