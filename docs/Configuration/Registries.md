# Docker Registry Configuration

ReadyStackGo supports pulling Docker images from private registries. This page describes how to configure registry credentials.

## Overview

When deploying a stack, ReadyStackGo attempts to pull the required images. Credentials are needed for private registries.

**Order of credential search (v0.15):**
1. **Database Registries** - Registries configured via Settings UI with matching Image Patterns
2. `Docker:ConfigPath` from IConfiguration (appsettings.json or `DOCKER__CONFIGPATH` environment variable)
3. `DOCKER_CONFIG` environment variable (standard Docker convention)
4. `/root/.docker/config.json` (Linux container)
5. `~/.docker/config.json` (user profile fallback)
6. No auth (for public images)

## Registry Management UI (v0.15)

Since v0.15, ReadyStackGo provides a web interface for managing Docker registries:

**Settings > Registries**

### Features

- **Add Registry**: Create new registry configurations with name, URL, and optional credentials
- **Edit Registry**: Update existing registry settings
- **Delete Registry**: Remove registry configurations
- **Set Default**: Mark a registry as default for images without matching patterns
- **Image Patterns**: Configure glob-style patterns for automatic credential matching

### Adding a Registry

1. Navigate to **Settings** in the sidebar
2. Click **Add Registry**
3. Fill in the details:
   - **Name**: Display name (e.g., "Docker Hub - Company Account")
   - **URL**: Registry URL (e.g., `https://index.docker.io/v1/`)
   - **Username**: Optional - for private registries
   - **Password**: Optional - for private registries
   - **Image Patterns**: Optional - glob patterns for automatic matching

### Image Patterns

Image patterns determine which registry credentials are used for specific images. Patterns use glob-style syntax:

| Pattern | Matches |
|---------|---------|
| `library/*` | `library/nginx`, `library/redis` |
| `myorg/**` | `myorg/app`, `myorg/sub/image` |
| `ghcr.io/**` | `ghcr.io/owner/repo`, `ghcr.io/org/sub/image` |
| `nginx` | Exact match for `nginx` |

**Pattern Rules:**
- `*` matches any characters within a single path segment
- `**` matches any characters across multiple path segments
- Patterns are case-insensitive
- Tags and digests are ignored during matching

**Example Configuration:**

| Registry | Image Patterns | Used For |
|----------|---------------|----------|
| Docker Hub (Company) | `mycompany/*`, `mycompany/**` | Company images on Docker Hub |
| GitHub Container Registry | `ghcr.io/**` | All GitHub packages |
| Azure Container Registry | `myregistry.azurecr.io/**` | Azure-hosted images |
| Default (Docker Hub) | *(none - marked as default)* | All other public images |

### Credential Resolution

When pulling an image, ReadyStackGo:

1. Checks all configured registries for matching Image Patterns
2. If a pattern matches, uses that registry's credentials
3. If no pattern matches, falls back to the default registry (if set)
4. If no database registry matches, falls back to file-based credentials

## File-Based Configuration (Legacy)

For environments without UI access, credentials can still be configured via Docker config files.

### Docker Config Mount (recommended for non-UI setups)

Mount the Docker config file into the container:

**Docker Compose Example:**
```yaml
services:
  readystackgo:
    image: wiesenwischer/readystackgo:latest
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ~/.docker/config.json:/root/.docker/config.json:ro
      - rsgo-config:/app/config
      - ./stacks:/app/stacks
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConfigPath=/app/config
    ports:
      - "8080:8080"
```

**Important:**
- The file must be mounted to `/root/.docker/config.json`
- The `:ro` flag makes the mount read-only (recommended for security)
- The user on the host must have run `docker login` beforehand

### Configuration via IConfiguration

The path to the Docker config can be set via IConfiguration:

**Via Environment Variable:**
```yaml
environment:
  - DOCKER__CONFIGPATH=/custom/path/config.json
```

**Via appsettings.json:**
```json
{
  "Docker": {
    "ConfigPath": "/custom/path/config.json"
  }
}
```

### DOCKER_CONFIG Environment Variable

The standard Docker convention is also supported:

```yaml
environment:
  - DOCKER_CONFIG=/docker-config
volumes:
  - ~/.docker:/docker-config:ro
```

**Note:** `DOCKER_CONFIG` points to the directory, not the file. ReadyStackGo automatically appends `/config.json`.

## Error Handling

- If an image pull fails and **no local image** exists: **Error** (deployment is aborted)
- If an image pull fails but a **local image exists**: **Warning** (local image is used)

This prevents unintended deployments with outdated images.

## Supported Registries

ReadyStackGo works with any OCI-compliant registry:

| Registry | URL Format |
|----------|------------|
| Docker Hub | `https://index.docker.io/v1/` |
| GitHub Container Registry | `https://ghcr.io` |
| Azure Container Registry | `https://<name>.azurecr.io` |
| Google Container Registry | `https://gcr.io` |
| Amazon ECR | `https://<account>.dkr.ecr.<region>.amazonaws.com` |
| Self-hosted | `https://registry.example.com` or `http://localhost:5000` |

## Security Notes

- Passwords are stored in the SQLite database (not encrypted at rest)
- Use the Settings UI for managing credentials when possible
- For higher security: deploy ReadyStackGo in a secure environment with restricted access
- The database file should only be readable by the ReadyStackGo process

## Troubleshooting

### "pull access denied" Error

```
Failed to pull image 'mycompany/myimage:latest' and no local copy exists.
Error: pull access denied for mycompany/myimage, repository does not exist or may require 'docker login'
```

**Causes:**
1. No registry credentials configured
2. Wrong credentials
3. Image doesn't exist in the registry
4. Image pattern doesn't match

**Solutions:**
1. Add a registry via Settings > Registries
2. Configure an Image Pattern that matches your image
3. Verify credentials are correct
4. Check the image name exists in the registry

### Registry Not Used for Image

If credentials are configured but not used:

1. Check **Image Patterns** - ensure the pattern matches your image
2. Verify pattern syntax (use `*` for single segment, `**` for multiple)
3. Check if another registry has a more specific matching pattern

### Enable Debug Logging

For detailed credential resolution logs:

```yaml
environment:
  - Logging__LogLevel__ReadyStackGo.Infrastructure.Docker=Debug
```

This shows:
- Which registries are checked
- Which patterns are evaluated
- Which credentials are used
