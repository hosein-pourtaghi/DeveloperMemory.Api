---
title: Project Architecture
project: DeveloperMemory
tags: architecture, dotnet, aspnet-core, design-patterns
---

# DeveloperMemory Architecture

## Overview
This project uses a layered architecture pattern with clear separation of concerns:

- **Presentation Layer**: ASP.NET Core controllers expose RESTful and OpenAI-compatible endpoints
- **Application Layer**: Services handle business logic (knowledge search, profile management, prompt building, LLM communication)
- **Domain Layer**: Models define data structures for documents, profiles, search results, and OpenAI-compatible types
- **Infrastructure Layer**: Configuration (AppSettings), logging (Serilog), DI extensions

## Key Design Decisions
- All services registered as Singletons for in-memory caching of documents and profiles
- Markdown files with YAML frontmatter used as the data storage format (no database)
- FreeLlmApiClient acts as a thin HTTP proxy to external LLM APIs
- OpenAI-compatible endpoint allows drop-in replacement for any OpenAI API consumer (Cline, Continue, etc.)

## Data Flow
1. Markdown files are loaded from filesystem on startup
2. YAML frontmatter is parsed for metadata (title, project, tags)
3. User queries are matched against documents using keyword relevance scoring
4. PromptBuilder assembles system prompt + profile context + search results + user query
5. Enriched prompt is sent to the configured LLM API (FreeLLM on localhost:3001)
6. LLM response is returned to the caller

## Technology Stack
- .NET 10.0 / ASP.NET Core
- Serilog for structured logging
- Swashbuckle for Swagger/OpenAPI
- HttpClient for LLM API communication
- JSON serialization via System.Text.Json
