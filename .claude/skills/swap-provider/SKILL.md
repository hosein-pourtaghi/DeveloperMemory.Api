---
name: swap-provider
description: Add or replace a model/embedding provider behind the existing abstractions without touching the gateway controller. Use when integrating a new LLM provider or embedding source.
---

# Add/swap a provider (model or embeddings)

Provider-agnosticism is a core architectural requirement. Two seams exist; both are DI-swap points — **no controller changes**.

## Model gateway (`IModelGateway`)

1. Create `src/DeveloperMemory.Api/Services/<Name>Client.cs` implementing `IModelGateway` (see `FreeLlmApiClient` — the current OpenAI-compatible adapter; `ManagedStream` wraps provider stream lifecycle).
2. Change **only** the DI registration in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<IModelGateway, NewProviderClient>();
   ```
3. If it needs settings, extend `AppSettings` (`Infrastructure/Configuration/`), bound from `AppSettings:FreeLlmApi`-style sections. Env override: `AppSettings__FreeLlmApi__<Prop>`.
4. Contract tests live in `tests/DeveloperMemory.Api.Tests/IModelGatewayTests.cs` — mirror them.

## Embeddings (`IEmbeddingProvider` / `IVectorStore`)

Already config-selected in `ServiceCollectionExtensions`:
- `EmbeddingOptions` (`Embedding:BaseUrl` etc.): when enabled + BaseUrl set → `OpenAICompatibleEmbeddingProvider` over HTTP; otherwise → `InMemoryEmbeddingProvider`.
- Vector store: PostgreSQL mode + embeddings enabled → `PostgresVectorStore` (pgvector); otherwise `InMemoryVectorStore`.

## Known gap (opportunity, not a bug)

`SemanticRetrievalProvider`/`HybridRetrievalProvider` are registered but **never consumed** — `MemoryRetrievalService` binds the single `IMemoryRetrievalProvider` (keyword) and domain `RetrievalMode` is unused. Wiring mode selection (config-driven, keyword default) is the highest-value retrieval upgrade.

## Never

- Couple business logic to `FreeLlmApiClient` or any concrete provider.
- Break the OpenAI-compatible `/v1/chat/completions` surface.
