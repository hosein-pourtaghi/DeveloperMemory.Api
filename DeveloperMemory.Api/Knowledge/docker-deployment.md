---
title: Docker Deployment Guide
project: DeveloperMemory
tags: docker, deployment, devops, containerization
---

# Docker Deployment Guide

## Dockerfile Pattern for .NET
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5041
EXPOSE 7144

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DeveloperMemory.Api.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DeveloperMemory.Api.dll"]
```

## Docker Compose
```yaml
version: '3.8'
services:
  developermemory:
    build: .
    ports:
      - "5041:5041"
      - "7144:7144"
    volumes:
      - ./Knowledge:/app/Knowledge
      - ./Profiles:/app/Profiles
    environment:
      - AppSettings__FreeLlmApi__BaseUrl=http://freellm:3001/v1
  freellm:
    image: freellm/api:latest
    ports:
      - "3001:3001"
```

## Environment Variables
Override any appsettings.json value with environment variables using `__` separator:
- `AppSettings__FreeLlmApi__BaseUrl`
- `AppSettings__FreeLlmApi__ApiKey`
- `AppSettings__Paths__KnowledgeFolder`
- `AppSettings__Paths__ProfilesFolder`

## Volume Mounts
Mount Knowledge/ and Profiles/ directories to persist data across container restarts.
