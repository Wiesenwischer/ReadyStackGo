# Stage 1: Build Frontend (Vite + React)
FROM node:20-alpine AS frontend-build

WORKDIR /app/frontend

# Copy package files
COPY src/ReadyStackGo.WebUi/package*.json ./

# Install dependencies
RUN npm ci

# Copy source files
COPY src/ReadyStackGo.WebUi/ ./

# Build frontend (outputs to ../ReadyStackGo.Api/wwwroot)
RUN npm run build

# Stage 2: Build Backend (.NET)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build

WORKDIR /app

# Copy solution and project files
COPY *.sln ./
COPY src/ReadyStackGo.Api/*.csproj ./src/ReadyStackGo.Api/
COPY src/ReadyStackGo.Application/*.csproj ./src/ReadyStackGo.Application/
COPY src/ReadyStackGo.Domain/*.csproj ./src/ReadyStackGo.Domain/
COPY src/ReadyStackGo.Infrastructure/*.csproj ./src/ReadyStackGo.Infrastructure/
COPY src/ReadyStackGo.Infrastructure.DataAccess/*.csproj ./src/ReadyStackGo.Infrastructure.DataAccess/
COPY src/ReadyStackGo.Infrastructure.Docker/*.csproj ./src/ReadyStackGo.Infrastructure.Docker/
COPY src/ReadyStackGo.Infrastructure.Security/*.csproj ./src/ReadyStackGo.Infrastructure.Security/

# Restore dependencies (only for src projects, exclude tests)
RUN dotnet restore src/ReadyStackGo.Api/ReadyStackGo.Api.csproj

# Copy all source files
COPY src/ ./src/

# Copy frontend build output to wwwroot
COPY --from=frontend-build /app/ReadyStackGo.Api/wwwroot ./src/ReadyStackGo.Api/wwwroot

# Build args for version baking (set by CI/CD, defaults for local builds)
ARG GIT_SEMVER=0.0.0-dev
ARG GIT_SHA=unknown

# Build and publish
RUN dotnet publish src/ReadyStackGo.Api/ReadyStackGo.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:Version=$GIT_SEMVER \
    -p:InformationalVersion=${GIT_SEMVER}+${GIT_SHA}

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install curl for healthcheck and git for Git repository stack sources
RUN apt-get update && apt-get install -y --no-install-recommends curl git && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published application
COPY --from=backend-build /app/publish .

# Create directories for config, data (SQLite), and stacks mount points
RUN mkdir -p /app/config /app/data /app/stacks

# Copy example stacks (copied to volume on first mount)
COPY stacks/ /app/stacks/

# Expose ports
EXPOSE 8080 8443

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "ReadyStackGo.Api.dll"]
