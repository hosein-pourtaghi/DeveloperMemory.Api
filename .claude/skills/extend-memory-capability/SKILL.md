---
name: extend-memory-capability
description: Step-by-step workflow for adding a new memory capability or domain feature end-to-end across the Clean Architecture layers. Use when the user asks to add a new entity, endpoint, or intelligence capability.
---

# Add a new memory capability

Authoritative order (AGENTS.md "How to Extend"), with layer rules:

1. **Domain** (`src/DeveloperMemory.Domain`) — entities, enums, repository interfaces. Zero project dependencies.
2. **Domain interfaces** (`Interfaces/`) — new repository contract if persistence is needed.
3. **Infrastructure** (`Persistence/`) — implement the repository; add EF configuration in `Persistence/Configurations/`; **add a migration** (`dotnet ef migrations add <Name> --project src/DeveloperMemory.Infrastructure`).
4. **Application contracts** (`Contracts/`) — service interface + DTOs in `DTOs/`.
5. **Application services** (`Services/`) — implement; constructor injection; map entities↔DTOs here, not in controllers.
6. **API controller** (`src/DeveloperMemory.Api/Controllers/`) — thin; validate input, delegate, return `ActionResult<T>`.
7. **DI** — register in `src/DeveloperMemory.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` (NOT Program.cs for Application/Infrastructure services).
8. **Tests** in `tests/` — mirror the layer; DB-backed work goes in Postgres fixture/factory suites.

## Hard rules (violating these is the common failure mode)

- Business rules live in **Domain**, use cases in **Application** — never in the API project.
- `OwnerId` is server-derived from `ICurrentUser` and enforced **fail-closed** at repository and retrieval layers — never accept it from the client.
- Memory lifecycle states (`Active/Superseded/Expired/...`) and scope-based isolation (`PrivacyFilter`) must be respected by any new retrieval path.
- No blind memory capture: ingestion goes through `IMemoryPolicy`/`IMemoryExtractionStrategy`.
- One class per file; file-scoped namespaces; `string.Empty`; `[]` collection expressions; pass `CancellationToken`.

## Reference examples to copy the shape from

- AgentContext (interface → impl → controller → DI → tests): `IAgentContextService` → `AgentContextService` → `AgentContextController`
- Repository + configuration: `ApiKeyRepository` + `ApiKeyConfiguration`
