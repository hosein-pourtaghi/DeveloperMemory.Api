---
title: Cline VS Code Integration
project: DeveloperMemory
tags: cline, vscode, ai, integration, workflow
---

# Cline VS Code Integration Guide

## How It Works
Cline connects to DeveloperMemory API as its OpenAI-compatible endpoint. When you ask Cline a question:

1. Cline sends a standard OpenAI chat completion request to DeveloperMemory
2. DeveloperMemory searches your knowledge base for relevant documents
3. Your developer profile is attached as context
4. The enriched prompt (profile + knowledge + your question) is sent to FreeLLM API
5. The LLM response is returned to Cline

## Configuration in Cline
Set the API base URL to: `http://localhost:5041/v1` (or `https://localhost:7144/v1` for HTTPS)
Set the model to: `gpt-3.5-turbo` (or whatever model FreeLLM supports)

## What Gets Injected
- **Developer Profile**: Your name, role, skills, experience, and bio
- **Relevant Documents**: Knowledge base entries matching your query with relevance scoring
- **System Prompt**: Customizable instructions for the LLM

## Tips
- Add more .md files to Knowledge/ to expand what Cline knows about your projects
- Keep files focused on specific topics for better search relevance
- Use descriptive tags to improve filtering
- Reindex after adding new documents: POST /api/Knowledge/reindex
