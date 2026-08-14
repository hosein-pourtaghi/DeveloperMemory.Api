# Data Models Reference

This document defines the data structures used by the Developer Memory API.

## DeveloperProfile
Represents a developer's profile information.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Id` | `Guid` | Unique identifier |
| `Name` | `string` | Full name of the developer |
| `Role` | `string` | Professional role |
| `Skills` | `List<string>` | List of technical skills |
| `Experience` | `string` | Years or description of experience |
| `Bio` | `string` | Biographical information |
| `FilePath` | `string` | Path to the source markdown file |
| `LastModified` | `DateTime` | Last modification timestamp |

## KnowledgeDocument
Represents a technical document stored in the system.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Id` | `Guid` | Unique identifier |
| `Title` | `string` | Document title |
| `Content` | `string` | Markdown content |
| `Project` | `string` | Associated project name |
| `Tags` | `List<string>` | Categorization tags |
| `FilePath` | `string` | Path to the source markdown file |
| `LastModified` | `DateTime` | Last modification timestamp |

## PromptRequest
Request model for AI assistant queries.

| Property | Type | Description |
| :--- | :--- | :--- |
| `Query` | `string?` | The user's question or prompt |
| `Project` | `string?` | Filter by project |
| `Tags` | `List<string>?` | Filter by tags |
| `ProfileId` | `string?` | ID of the developer profile to use as context |
| `SystemPrompt` | `string?` | Custom system instructions for the LLM |