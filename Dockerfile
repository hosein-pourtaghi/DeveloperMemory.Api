# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore (cached layer)
COPY src/DeveloperMemory.Domain/DeveloperMemory.Domain.csproj src/DeveloperMemory.Domain/
COPY src/DeveloperMemory.Application/DeveloperMemory.Application.csproj src/DeveloperMemory.Application/
COPY src/DeveloperMemory.Infrastructure/DeveloperMemory.Infrastructure.csproj src/DeveloperMemory.Infrastructure/
COPY src/DeveloperMemory.Api/DeveloperMemory.Api.csproj src/DeveloperMemory.Api/

RUN dotnet restore src/DeveloperMemory.Api/DeveloperMemory.Api.csproj

# Copy source and build
COPY src/ src/
RUN dotnet publish src/DeveloperMemory.Api/DeveloperMemory.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish ./

# Copy profiles and knowledge for runtime
COPY src/DeveloperMemory.Api/Profiles/ ./Profiles/
COPY src/DeveloperMemory.Api/Knowledge/ ./Knowledge/

# Environment defaults
ENV ASPNETCORE_ENVIRONMENT=Production
# Default to in-memory database for local development.
# Railway MUST override: UseInMemoryDatabase=false
ENV UseInMemoryDatabase=true

# Bind to Railway's runtime PORT when present, falling back to the local
# development port (5041) otherwise. A shell-form entrypoint is required so
# $PORT is expanded at container start; Kestrel only listens on 0.0.0.0:5041
# when no PORT is supplied. Local `dotnet run` development is unaffected
# (it uses Properties/launchSettings.json).
ENTRYPOINT ["sh", "-c", "dotnet DeveloperMemory.Api.dll --urls http://0.0.0.0:${PORT:-5041}"]
