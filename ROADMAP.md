# Development Roadmap

**Last updated:** 2026-08-28 (Phase K complete)

This roadmap describes the remaining evolution of DeveloperMemory.Api. Source code and tests define what is implemented today; each phase below is independently authorized only when work begins on that phase.

## Completed Baseline

Phases A through J.1 established the Clean Architecture structure, authentication and ownership controls, PostgreSQL persistence, lifecycle-aware memory storage, deterministic retrieval and Prompt Intelligence foundations, gateway integration, observability, and retrieval regression coverage.

### Phase K — Prompt Processing History + Production Verification
**STATUS: COMPLETE**

- Owner-aware prompt-processing history application boundary
- Scoped `IPromptProcessingHistoryService → PromptProcessingHistoryService`
- Scoped `IPromptProcessingRecordRepository → PromptProcessingRecordRepository`
- SQL-side owner filtering and bounded reads
- Preserved `profileId`, `from`, `to`, `optimizationMode`, `validationStatus`, and `fallbackUsed` filters
- Native PostgreSQL integration verification
- Production DI/controller activation verification
- Authenticated Kestrel HTTP verification and PostgreSQL restart persistence
- Final verified test count: **634 passed, 0 failed**

**Non-goals:** Phase K does not implement semantic retrieval, intelligent memory capture, agent execution, MCP/tools, central orchestration, or cloud production deployment.

## Remaining Roadmap

### Phase L — Semantic Memory Retrieval
**STATUS: NOT STARTED**

**Objective:** Add provider-independent semantic retrieval alongside the existing keyword path.

**Why it exists:** Keyword matching cannot reliably identify conceptually similar memories.

**Dependencies:** Existing retrieval contracts, PostgreSQL persistence, and the current lifecycle/ownership safeguards.

**Major capabilities:** Embedding abstraction, provider-independent embedding implementation, vector similarity search, semantic relevance ranking, hybrid keyword-plus-semantic retrieval, result/context budgeting, and project/workspace-aware filtering.

**Architectural boundaries:** Embeddings and vector persistence belong in replaceable Infrastructure implementations; retrieval orchestration remains behind Application contracts; API consumers must not depend on a concrete provider.

**Non-goals:** No intelligent memory capture, agent runtime, MCP integration, or central orchestration.

**Acceptance criteria:** Semantic and hybrid retrieval are selectable without breaking keyword fallback; ownership, scope, project/workspace, lifecycle, ranking, and bounds remain enforced; provider substitution is possible; focused unit, PostgreSQL, and runtime tests pass.

### Phase M — Memory Intelligence & Lifecycle Intelligence
**STATUS: NOT STARTED**

**Objective:** Turn manual memory storage and lifecycle mechanics into selective, explainable memory intelligence.

**Why it exists:** Persistent storage alone does not determine what is valuable, redundant, contradictory, or still valid.

**Dependencies:** Phase L retrieval/similarity capabilities and existing lifecycle/domain rules.

**Major capabilities:** Importance, confidence, and relevance evaluation; selective capture; duplicate and contradiction detection; consolidation; intelligent supersession, expiration, archival, and lifecycle decisions.

**Architectural boundaries:** Policies and business rules belong in Domain/Application; provider calls remain replaceable Infrastructure concerns; capture must remain bounded and privacy-aware.

**Non-goals:** No blind transcript storage or autonomous agent execution.

**Acceptance criteria:** Capture decisions are selective, explainable, owner-safe, lifecycle-aware, bounded, and covered by deterministic and provider-independent tests.

### Phase N — Advanced LLM-Powered Prompt Intelligence
**STATUS: NOT STARTED**

**Objective:** Extend the deterministic Prompt Intelligence baseline with optional semantic/LLM-assisted analysis.

**Why it exists:** Deterministic heuristics cannot fully interpret nuanced intent, constraints, contradictions, or model-specific prompt requirements.

**Dependencies:** Existing `IPromptIntelligenceEngine`, Phase L retrieval, and Phase M intelligence policies.

**Major capabilities:** LLM-assisted intent and task classification, semantic constraint interpretation, contradiction detection, deduplication, intelligent context selection, token budgeting, advanced optimization, and model-aware construction.

**Architectural boundaries:** The deterministic pipeline remains a safe fallback; LLM providers are accessed through replaceable abstractions; API controllers remain thin.

**Non-goals:** No mandatory vendor lock-in or replacement of deterministic safety controls.

**Acceptance criteria:** LLM assistance is optional, bounded, observable, privacy-aware, and has deterministic fallback with regression coverage.

### Phase O — Project & Workspace Context Intelligence
**STATUS: NOT STARTED**

**Objective:** Provide coherent project, workspace, repository, rules, source, documentation, and task context.

**Why it exists:** User and memory context alone is insufficient for reliable project-aware assistance.

**Dependencies:** Phases L-N and existing project/workspace identifiers and context contracts.

**Major capabilities:** `IProjectContextProvider`, repository/source/documentation context, project rules, workspace context, task context, and project-aware retrieval.

**Architectural boundaries:** Context providers remain replaceable; filesystem/repository integrations stay outside Domain; ownership and project boundaries remain enforced.

**Non-goals:** No agent execution or tool protocol implementation.

**Acceptance criteria:** Context is correctly scoped, bounded, explainable, and integrated without coupling core services to one repository or workspace provider.

### Phase P — Agent Runtime Abstraction & Execution Integration
**STATUS: NOT STARTED**

**Objective:** Integrate replaceable downstream agent runtimes without turning the API into a monolithic agent.

**Why it exists:** Prepared context must be usable by multiple execution environments.

**Dependencies:** Prompt Intelligence and project context capabilities.

**Major capabilities:** `IAgentRuntime`, agent discovery, task delegation, execution requests/status, result collection, and provider replacement.

**Architectural boundaries:** Runtime adapters belong outside core Domain logic; the control plane coordinates contracts but does not embed a specific agent framework.

**Non-goals:** No single mandatory runtime or autonomous behavior by default.

**Acceptance criteria:** At least one adapter can execute through the abstraction, statuses/results are bounded and auditable, and replacement does not require controller redesign.

### Phase Q — MCP & Tool Integration
**STATUS: NOT STARTED**

**Objective:** Add modular discovery and invocation of tools and MCP servers.

**Why it exists:** Agents and models need controlled access to external capabilities.

**Dependencies:** Phase P runtime boundary and Phase O context/security policies.

**Major capabilities:** `IToolProvider`, `IMcpClient`, `IToolRegistry`, `IToolExecutor`, discovery, permission boundaries, invocation, and result handling.

**Architectural boundaries:** Tool/MCP adapters remain replaceable Infrastructure components; permission checks and contracts remain explicit; core memory logic must not depend on a specific server.

**Non-goals:** No unrestricted tool execution or implicit permissions.

**Acceptance criteria:** Tools can be discovered, authorized, invoked, and audited through provider-independent contracts with failure and timeout handling.

### Phase R — Central AI Orchestration / Personal AI Control Plane
**STATUS: NOT STARTED**

**Objective:** Coordinate memory, models, agents, tools, and workflows for multi-step requests.

**Why it exists:** A unified control plane is needed for decomposition and execution coordination across capabilities.

**Dependencies:** Phases L-Q.

**Major capabilities:** Request decomposition, model/memory/agent/tool selection, multi-step planning, monitoring, validation, memory persistence, and workflow coordination.

**Architectural boundaries:** Orchestration composes existing contracts; it must not bypass ownership, policy, provider, or lifecycle boundaries.

**Non-goals:** No replacement of downstream agents or LLMs and no uncontrolled autonomy.

**Acceptance criteria:** Multi-step plans are bounded, observable, recoverable, validated, and tested across provider substitutions and authorization boundaries.

### Phase S — Production Deployment & Operational Hardening
**STATUS: NOT STARTED**

**Objective:** Make the system suitable for production/cloud operation.

**Why it exists:** Local Kestrel/PostgreSQL verification is not equivalent to production readiness.

**Dependencies:** Stable core capabilities and operational requirements.

**Major capabilities:** Production deployment, cloud hosting, containerization, secret management, observability, resilience, scaling, backup/recovery, and operational security.

**Architectural boundaries:** Deployment concerns remain outside business logic; local PostgreSQL/Kestrel development must remain supported.

**Non-goals:** No cloud provider is mandated by this roadmap.

**Acceptance criteria:** Documented deployment, secure configuration, health/readiness behavior, monitoring, recovery procedures, and operational tests exist for the selected environment.

### Phase T — Advanced Interfaces & Workflow Automation
**STATUS: LONG-TERM**

**Objective:** Extend the control plane to richer interaction and automation scenarios.

**Why it exists:** Voice, scheduled, external-service, and autonomous workflows are longer-term product directions beyond the core memory layer.

**Dependencies:** Phases P-R and production hardening from Phase S.

**Major capabilities:** Voice interaction, conversational workflow execution, scheduled workflows, external service integrations, email/actions, and multi-step autonomous workflows.

**Architectural boundaries:** Interfaces and workflow adapters must consume existing provider-independent orchestration contracts and preserve explicit permissions and auditability.

**Non-goals:** No premature implementation during Phase L and no assumption that autonomous execution is always enabled.

**Acceptance criteria:** Each workflow capability has explicit authorization, observability, failure recovery, bounded execution, and provider-independent integration tests.

## Phase Ordering

```text
K COMPLETE
  ↓
L Semantic Memory Retrieval
  ↓
M Memory Intelligence & Lifecycle Intelligence
  ↓
N Advanced LLM-Powered Prompt Intelligence
  ↓
O Project & Workspace Context Intelligence
  ↓
P Agent Runtime Abstraction & Execution Integration
  ↓
Q MCP & Tool Integration
  ↓
R Central AI Orchestration / Personal AI Control Plane
  ↓
S Production Deployment & Operational Hardening
  ↓
T Advanced Interfaces & Workflow Automation (LONG-TERM)
```
