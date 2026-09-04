# DeveloperMemory.Api — V2 Vision & Master Specification

## 1. Document Status

**Project:** DeveloperMemory.Api  
**Version:** V2  
**Status:** AUTHORITATIVE / IMMUTABLE VISION  
**Purpose:** Define the long-term architectural vision and capabilities of DeveloperMemory.Api V2.

### Immutability Rule

This document is the **canonical V2 vision** for DeveloperMemory.Api.

All future implementation work, refactoring, phases, agents, prompts, and architectural decisions MUST preserve this vision.

The V2 vision MUST NOT be changed, reduced, expanded, or reinterpreted implicitly.

A change to this vision is allowed **ONLY when explicitly requested and approved by the project owner**.

Normal implementation decisions, bug fixes, performance improvements, refactoring, technology changes, and phase planning are NOT permission to change this vision.

If an implementation decision appears to conflict with this document, the implementation must adapt unless the project owner explicitly changes the vision.

---

# 2. Core Vision

DeveloperMemory.Api V2 is intended to evolve from a **developer memory and prompt-intelligence API** into a **generic AI agent orchestration and persistent intelligence platform**.

The desired experience is:

> "I tell the assistant what I want. The system determines what needs to be done, which agents and tools are required, what information must be collected, which models are appropriate, executes the work, persists useful knowledge/data, and reports the result."

The system must be **generic and project-independent**.

It must NOT be designed specifically for:

- financial markets
- cryptocurrency
- gold
- a particular website
- one deployment provider
- one programming language
- one project
- one data source
- one LLM
- one AI provider
- one agent type

Financial-market analysis is only an example use case demonstrating the required generality.

---

# 3. Primary V2 Goals

V2 has two major capability pillars.

## Pillar A — Generic Agent System + Orchestrator

DeveloperMemory.Api must support a dynamic agent architecture containing:

1. An **Assistant / Orchestrator Agent**
2. Multiple specialized agents
3. Dynamic task decomposition
4. Dynamic agent selection
5. Dynamic model selection
6. Tool invocation
7. Inter-agent communication
8. Task state and execution tracking
9. Persistent context and memory
10. Result aggregation
11. Error handling and recovery

The user should communicate primarily with the Assistant/Orchestrator.

The user should NOT have to manually decide:

- which agent should execute the task
- which model should be used
- which tools are needed
- which data sources are required
- the execution order
- whether tasks can execute in parallel
- how intermediate results should be passed between agents

---

# 4. Assistant / Orchestrator Agent

The system must have a primary intelligent agent that acts as the user's general-purpose assistant and orchestrator.

Its responsibilities include:

### Understanding

Interpret natural-language user requests.

### Planning

Determine:

- objectives
- subtasks
- dependencies
- required tools
- required data
- required agents
- appropriate models
- execution order
- validation requirements
- expected outputs

### Delegation

Assign work to specialized agents.

Example:

```text
User
  ↓
Assistant / Orchestrator
  ├── Research Agent
  ├── Data Collection Agent
  ├── Analysis Agent
  ├── Coding Agent
  ├── Testing Agent
  ├── Deployment Agent
  └── Verification Agent
```

These are examples, not fixed agent types.

### Coordination

Coordinate agent execution and pass relevant context/results between agents.

### Validation

Verify whether agent results satisfy the requested objective.

### Recovery

When an agent fails, the orchestrator should be capable of:

- retrying
- changing strategy
- selecting another agent
- selecting another model
- requesting additional information
- continuing with partial results when appropriate
- reporting failure clearly when recovery is impossible

---

# 5. Agents Are Generic Capabilities

Agents must NOT be hard-coded around one business domain.

An agent is a reusable execution capability.

Examples:

- Research Agent
- Web/Data Collection Agent
- Browser Agent
- API Agent
- Coding Agent
- Code Review Agent
- Testing Agent
- Database Agent
- Data Analysis Agent
- Mathematical Analysis Agent
- Documentation Agent
- Deployment Agent
- Monitoring Agent
- Security Agent

The architecture must allow new agents to be introduced without redesigning the core system.

An agent should have a definable contract containing concepts such as:

- identity
- purpose
- capabilities
- supported tools
- input contract
- output contract
- required context
- model requirements
- permissions
- execution state
- failure behavior

---

# 6. Dynamic Agent Creation / Configuration

The long-term goal is that agents can be configured dynamically rather than requiring application code changes for every new task domain.

The system should support describing an agent through metadata such as:

```text
Agent:
    Name
    Description
    Capabilities
    Tools
    Model Policy
    Input Schema
    Output Schema
    Permissions
    Constraints
    System Instructions
    Execution Policy
```

The architecture must allow the orchestrator to discover available agents and select appropriate ones dynamically.

---

# 7. Generic Data Acquisition System

DeveloperMemory.Api V2 must eventually provide a **generic data acquisition capability**.

This is NOT a financial-data collector.

It is a general mechanism for obtaining external information.

The user should be able to say:

> "Every hour collect the relevant data from these websites, normalize it, store it, and make it available to my analysis agents."

The system should determine the required acquisition workflow.

Possible sources include:

- REST APIs
- GraphQL APIs
- websites
- HTML pages
- structured web pages
- documents
- files
- public datasets
- other external services
- future/custom connectors

---

# 8. Natural-Language Data Collection Configuration

A major V2 objective is:

> The user should be able to define data acquisition requirements primarily through conversation.

For example:

> "Monitor these five websites every 30 minutes. Extract the relevant information, normalize the data, detect changes, and store historical records."

The orchestrator should determine:

1. What sources are involved
2. What information is required
3. How frequently it should be collected
4. What acquisition tools are needed
5. What schema should represent the data
6. Where the data should be persisted
7. What validation is required
8. Which downstream agents should process the data

---

# 9. Data Acquisition Must Be Generic

The implementation MUST NOT contain assumptions such as:

```text
GoldScraper
BitcoinScraper
StockScraper
SpecificWebsiteScraper
```

unless these are plugins/configurations built on top of the generic acquisition framework.

The core capability should instead resemble:

```text
Source
  ↓
Acquisition Strategy
  ↓
Extraction
  ↓
Normalization
  ↓
Validation
  ↓
Persistence
  ↓
Analysis / Agents
```

A specific financial-market collector should therefore be an instance/configuration of the generic system, not the architecture itself.

---

# 10. Persistent Data and Memory

The system must distinguish between:

## Memory

Information about:

- users
- projects
- workspaces
- preferences
- decisions
- constraints
- previous interactions
- agent context
- task history
- learned project knowledge

and:

## Collected Data

External information acquired by agents, such as:

- market data
- API responses
- documents
- website data
- datasets
- measurements
- historical observations
- application telemetry

These concepts may share infrastructure where appropriate, but they MUST NOT be conceptually conflated.

DeveloperMemory's purpose is not simply "a database."

Its purpose is **persistent contextual intelligence**.

---

# 11. DeveloperMemory as the Intelligence Context Layer

DeveloperMemory.Api must remain responsible for persistent contextual knowledge and memory.

It should provide the intelligence layer that allows agents to understand:

- who is asking
- what project they are working on
- what they previously decided
- what constraints exist
- what tasks have already happened
- what information is relevant
- what knowledge should persist
- what information is obsolete
- what context should be supplied to an agent

The memory system must remain:

- persistent
- scoped
- retrievable
- rankable
- lifecycle-aware
- project-aware
- workspace-aware
- extensible

---

# 12. Agent Memory

Agents must be able to operate with contextual memory.

The system should support relevant context such as:

```text
Global Memory
Project Memory
Workspace Memory
Task Memory
Agent Memory
Execution Memory
Collected Data
```

The orchestrator must determine what context is relevant to each agent/task.

Agents should not receive the entire database blindly.

Context must be selected according to:

- relevance
- scope
- task
- permissions
- lifecycle
- priority
- recency
- project/workspace context

---

# 13. Model Abstraction

Agents MUST NOT be tightly coupled to one LLM.

FreeLLMApi is the model gateway/provider layer.

DeveloperMemory.Api / the agent orchestration layer should treat LLMs as interchangeable model capabilities.

The architecture must support:

```text
Agent
   ↓
Model Selection Policy
   ↓
FreeLLMApi
   ↓
Available Model
```

Different models may have different strengths:

- stronger reasoning model
- faster model
- coding-specialized model
- cheaper model
- local model
- fallback model

The system must be capable of selecting an appropriate model based on the task.

---

# 14. Multiple LLM Accounts / Providers

The architecture should support the possibility of multiple model sources.

Examples:

```text
FreeLLMApi
Provider A
Provider B
Local LLM
Future Provider
```

The orchestration layer must not assume that one model or one account will always be available.

Model routing should eventually consider:

- capability
- availability
- latency
- cost
- reliability
- context capacity
- task requirements
- fallback policy

The exact routing algorithm is an implementation concern.

---

# 15. FreeLLMApi Responsibility

FreeLLMApi remains the **LLM/model gateway**.

Its primary responsibility is providing access to available models through a consistent API.

DeveloperMemory.Api should not duplicate the responsibilities of the model gateway unnecessarily.

Conceptually:

```text
OpenWebUI
     ↓
DeveloperMemory.Api
     ↓
Agent / Orchestrator
     ↓
Model Selection
     ↓
FreeLLMApi
     ↓
LLM
```

FreeLLMApi provides model access.

DeveloperMemory.Api provides:

- memory
- context
- orchestration
- agents
- tools
- task execution
- persistent intelligence
- data acquisition coordination

---

# 16. OpenWebUI Responsibility

OpenWebUI is primarily the user-facing conversational interface.

It should allow the user to communicate naturally with the Assistant/Orchestrator.

The user should not need to understand the internal agent topology.

OpenWebUI should remain replaceable.

The architecture must not make the core intelligence dependent on OpenWebUI.

---

# 17. Tools

Agents must eventually be able to use tools.

Examples:

- HTTP/API requests
- Web browsing
- Web extraction
- File operations
- Git operations
- Database operations
- Code execution
- Build/test execution
- Deployment APIs
- Monitoring APIs
- cloud-provider APIs

Tools should have explicit contracts.

Tool invocation should be controlled by:

- permissions
- input validation
- execution policy
- security boundaries
- auditability

---

# 18. Autonomous Application Development

A major long-term use case is allowing the user to delegate application development.

Example:

> "Build this application, test it, configure it, deploy it to Railway, and verify that it is working."

The orchestrator should be able to decompose this into tasks such as:

```text
Requirements
    ↓
Architecture
    ↓
Implementation
    ↓
Database
    ↓
Configuration
    ↓
Testing
    ↓
Build
    ↓
Deployment
    ↓
Verification
    ↓
Monitoring
```

The actual agent topology must remain dynamic.

---

# 19. External System Integration

The V2 architecture must support interaction with external systems.

Examples:

- GitHub
- Railway
- cloud providers
- PostgreSQL
- external APIs
- deployment platforms
- monitoring systems
- source-control systems
- web services

Credentials/API keys must NEVER be treated as ordinary memory.

Secrets require dedicated secure handling.

The orchestrator may know that a credential exists and may request/use it through an approved secure mechanism, but secrets must not be casually inserted into prompts, logs, memory records, or model context.

---

# 20. Scheduled and Continuous Work

The system must eventually support tasks that continue after the user sends the initial request.

Examples:

```text
Every 5 minutes
Every hour
Every day
Every week
Until a condition occurs
Until a specified date
```

Example:

> "Collect this information every hour for the next six months."

The system should create a durable execution/scheduling definition.

The user should later be able to ask:

> "What happened today?"

or:

> "Analyze everything collected over the last three months."

Historical data must remain available according to its retention policy.

---

# 21. Example: Financial Market Monitoring

Financial-market monitoring is an example workload, NOT a special architecture.

A possible workflow:

```text
User
 ↓
Assistant / Orchestrator
 ↓
Research / Source Discovery
 ↓
Data Acquisition Agents
 ↓
Normalization
 ↓
Validation
 ↓
Persistent Historical Data
 ↓
Analysis Agents
 ├── Trend Analysis
 ├── Technical Indicators
 ├── Statistical Analysis
 ├── Pattern Detection
 └── Multi-Timeframe Analysis
 ↓
Result Aggregation
 ↓
Assistant
 ↓
User
```

Possible data:

- price
- volume
- OHLC
- time series
- market metadata
- historical observations

Possible calculations:

- moving averages
- RSI
- MACD
- Bollinger Bands
- support/resistance
- trend detection
- channel detection
- volatility
- multi-timeframe analysis

These are examples of capabilities the generic agent/tool system should make possible.

They must NOT become hard-coded assumptions throughout DeveloperMemory.Api.

---

# 22. Multi-Timeframe Analysis

The generic analysis architecture must support different temporal granularities where the underlying data supports them.

Examples:

- minutes
- hourly
- daily
- weekly
- monthly

An analysis agent should be able to request the appropriate dataset and perform the required computation.

---

# 23. Analysis Must Prefer Deterministic Computation

LLMs should not be responsible for performing calculations that can reliably be performed by deterministic software.

Preferred flow:

```text
Price/data
   ↓
Deterministic calculation engine
   ↓
Numerical results
   ↓
LLM analysis/reasoning
```

LLMs should primarily handle:

- interpretation
- reasoning
- planning
- explanation
- synthesis
- decision support

Deterministic code should handle:

- calculations
- transformations
- validation
- structured data processing

---

# 24. Agent Orchestration vs Business Logic

The orchestrator should decide **what needs to happen**.

Specialized services/tools should perform **deterministic operations**.

Avoid turning the orchestrator into one giant service containing every possible business operation.

Preferred separation:

```text
Orchestration
     ↓
Agents
     ↓
Tools / Services
     ↓
External Systems / Data
```

---

# 25. Generic Project Model

The system must support multiple independent projects.

Example:

```text
Project A
 ├── Agents
 ├── Memory
 ├── Data Sources
 ├── Tasks
 └── Executions

Project B
 ├── Agents
 ├── Memory
 ├── Data Sources
 ├── Tasks
 └── Executions
```

A project must not contaminate another project's context.

Workspace and project isolation remain fundamental.

---

# 26. Dynamic Future Projects

A major requirement is that new projects should not require architectural redesign.

Months from now, the user should be able to say:

> "I have a new project. I want you to monitor these sources, process this information, and notify me when condition X occurs."

The system should be capable of turning that conversation into a project/task/data/agent configuration.

The architecture must therefore be designed for **general-purpose extensibility**, not today's specific use cases.

---

# 27. Observability and Auditability

Autonomous agents create significant debugging and operational complexity.

The system must therefore support observability around:

- user requests
- generated plans
- agent selection
- model selection
- tool calls
- execution states
- failures
- retries
- outputs
- persistence operations
- scheduling
- external calls

Sensitive information must be excluded or redacted appropriately.

The existing diagnostic logging direction should be preserved and extended rather than replaced unnecessarily.

---

# 28. Security

Autonomous agents can perform real actions.

Security is therefore a first-class V2 requirement.

The system must eventually support:

- authentication
- authorization
- tool permissions
- agent permissions
- project isolation
- secret management
- execution policies
- audit logs
- safe handling of external credentials
- controlled destructive operations

An agent must not automatically receive unrestricted access to every available tool.

---

# 29. Human Approval Boundaries

For potentially destructive or high-impact operations, the system should support explicit user approval.

Examples:

- deleting production data
- deploying production systems
- rotating credentials
- destructive database operations
- financial transactions
- irreversible external actions

The orchestrator should be capable of stopping at an approval boundary and asking the user for confirmation.

---

# 30. Persistence Architecture

PostgreSQL is a suitable primary persistent store for the system, but V2 should avoid unnecessary coupling to a specific storage technology where practical.

The architecture should distinguish logically between:

```text
Memory
Task State
Agent State
Execution History
Collected Data
Configuration
Diagnostics
```

The exact database schema is an implementation concern.

Do not create multiple databases merely because different conceptual entities exist unless there is a concrete architectural reason.

---

# 31. No Unnecessary New Application

The V2 objective is NOT to create a collection of unrelated microservices.

DeveloperMemory.Api should remain the central intelligence/orchestration platform unless there is a demonstrated reason to split a capability into another deployable service.

A new application should only be introduced when justified by:

- scalability
- security isolation
- independent deployment
- operational requirements
- external system constraints
- clear architectural boundaries

Do not create a separate application simply because a new capability was added.

---

# 32. Provider-Agnostic Architecture

The system must remain provider-agnostic where reasonably possible.

Avoid hard-coding:

- one LLM provider
- one website
- one deployment platform
- one programming language
- one database
- one browser implementation
- one frontend
- one external API

Provider-specific integrations should exist behind explicit abstractions or connector/tool boundaries where abstraction provides real value.

Do not over-engineer abstractions that have no current architectural purpose.

---

# 33. Current System Must Be Preserved

V2 is an evolution of the existing DeveloperMemory.Api.

Existing successful capabilities must not be discarded merely because V2 introduces agents and orchestration.

Existing areas such as:

- persistent memory
- semantic retrieval
- lifecycle intelligence
- project/workspace context
- prompt intelligence
- ranking
- agent context
- diagnostics

should be treated as foundations for V2.

Do not rewrite stable functionality without a concrete reason.

---

# 34. Implementation Philosophy

V2 implementation must follow these principles:

1. Incremental development
2. Small verifiable phases
3. Existing behavior preserved
4. Tests before/alongside significant changes
5. Real infrastructure validation where required
6. No speculative abstractions
7. No unnecessary microservices
8. No domain-specific architecture disguised as generic infrastructure
9. Explicit contracts
10. Secure-by-default execution
11. Deterministic computation where possible
12. LLM reasoning where it adds value

---

# 35. Development Environment Constraint

Local development MUST NOT depend on Docker.

The existing project direction is:

```text
.NET / Kestrel
PostgreSQL installed locally
Real local infrastructure
```

Docker may be considered for future deployment/infrastructure scenarios only when explicitly required.

Do not introduce Docker into the local development workflow.

---

# 36. Target User Experience

The final V2 experience should approach:

```text
User
  ↓
"Build X."
  ↓
Assistant / Orchestrator
  ↓
Understand
  ↓
Plan
  ↓
Retrieve memory/context
  ↓
Select agents
  ↓
Select tools
  ↓
Select appropriate model(s)
  ↓
Execute
  ↓
Validate
  ↓
Persist useful knowledge/data
  ↓
Report
```

For long-running tasks:

```text
User
  ↓
"Monitor X for six months."
  ↓
Orchestrator
  ↓
Create durable workflow
  ↓
Scheduled acquisition
  ↓
Data persistence
  ↓
Analysis
  ↓
Condition detection
  ↓
Notification / report
```

---

# 37. Definition of Success

DeveloperMemory.Api V2 is successful when a user can describe a complex objective in natural language and the system can transform that objective into a controlled, persistent, observable workflow involving:

- memory
- context
- agents
- models
- tools
- external data
- deterministic processing
- scheduling
- persistence
- validation

without requiring the user to manually design the internal architecture for every new project.

---

# 38. What V2 Is NOT

V2 is NOT:

- merely a chat UI
- merely a memory database
- merely an LLM proxy
- merely an OpenWebUI extension
- merely a scraper
- merely a financial analysis system
- merely a collection of hard-coded agents
- merely a prompt generator
- a collection of unrelated microservices

V2 is a **general-purpose persistent agent intelligence and orchestration layer**.

---

# 39. Architectural Mental Model

The canonical mental model is:

```text
                         ┌─────────────────────┐
                         │      OpenWebUI      │
                         │   User Interface    │
                         └──────────┬──────────┘
                                    │
                                    ▼
                    ┌────────────────────────────┐
                    │     DeveloperMemory.Api    │
                    │                            │
                    │  Assistant / Orchestrator  │
                    │            │               │
                    │      ┌─────┴─────┐         │
                    │      │           │         │
                    │    Agents      Memory      │
                    │      │           │         │
                    │      │       Retrieval     │
                    │      │           │         │
                    │    Tools     Context       │
                    │      │                     │
                    │      └──────┬──────────────┘
                    │             │
                    │       Task Execution
                    │             │
                    └─────────────┼──────────────┘
                                  │
             ┌────────────────────┼────────────────────┐
             │                    │                    │
             ▼                    ▼                    ▼
       ┌───────────┐       ┌─────────────┐      ┌─────────────┐
       │FreeLLMApi │       │ External     │      │ PostgreSQL  │
       │   Models  │       │ Tools/APIs   │      │ Persistence │
       └───────────┘       └─────────────┘      └─────────────┘
```

This diagram represents responsibilities, not mandatory implementation details.

---

# 40. Final Governing Rule

Whenever future work is proposed for DeveloperMemory.Api, ask:

> "Does this move the system toward the V2 vision while preserving existing behavior and keeping the architecture generic?"

If YES:

Proceed, provided the implementation is justified and incremental.

If NO:

Do not implement it merely because it is convenient for a specific project.

If the requested feature conflicts with this V2 vision:

Stop and explicitly identify the conflict.

Do NOT silently modify the V2 vision.

Only the project owner can authorize a change to this document.

---

# V2 Vision Summary

DeveloperMemory.Api V2 will become a **generic, persistent, model-agnostic AI agent orchestration platform**.

Its central Assistant/Orchestrator will understand natural-language objectives, dynamically plan work, select agents, select models, invoke tools, acquire external information, coordinate execution, persist memory and collected data, validate results, and support long-running workflows.

The system must be generic enough that today's financial-data example and tomorrow's completely unrelated project can use the same underlying architecture.

**This vision is immutable unless explicitly changed by the project owner.**
