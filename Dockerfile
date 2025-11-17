# Stage 1: Build Frontend (Vite + React)
FROM node:20-alpine AS frontend-build

WORKDIR /app/frontend

# Copy package files
COPY src/ReadyStackGo.WebUI/package*.json ./

# Install dependencies
RUN npm ci

# Copy source files
COPY src/ReadyStackGo.WebUI/ ./

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

# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY src/ ./src/

# Copy frontend build output to wwwroot
COPY --from=frontend-build /app/frontend/dist ./src/ReadyStackGo.Api/wwwroot

# Build and publish
RUN dotnet publish src/ReadyStackGo.Api/ReadyStackGo.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Copy published application
COPY --from=backend-build /app/publish .

# Create config directory
RUN mkdir -p /app/config

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
