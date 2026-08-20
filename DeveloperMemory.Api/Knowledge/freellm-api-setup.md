---
title: FreeLLM API Setup
project: DeveloperMemory
tags: freellm, api, setup, configuration, llm
---

# FreeLLM API Setup

## Overview
FreeLLM API is a local OpenAI-compatible API server that provides LLM inference capabilities. It runs on localhost:3001 and serves as the backend for DeveloperMemory's AI proxy.

## Endpoints
- **Chat Completions**: POST /v1/chat/completions
- **Models**: GET /v1/models
- **Health**: GET /health

## Configuration in DeveloperMemory
The connection is configured in appsettings.json under AppSettings:FreeLlmApi:
- BaseUrl: http://localhost:3001/v1
- ApiKey: your-api-key-here

## Environment Variables
Override with:
- AppSettings__FreeLlmApi__BaseUrl
- AppSettings__FreeLlmApi__ApiKey

## Troubleshooting
- Ensure FreeLLM is running on port 3001 before starting DeveloperMemory
- Check /v1/models endpoint to verify available models
- Check DeveloperMemory logs in logs/devmemory-*.log for connection errors
