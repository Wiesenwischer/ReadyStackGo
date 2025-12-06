# Docker Registry Configuration

ReadyStackGo supports pulling Docker images from private registries. This page describes the different options for configuring registry credentials.

## Overview

When deploying a stack, ReadyStackGo attempts to pull the required images. Credentials are needed for private registries.

**Order of credential search (v0.5):**
1. `Docker:ConfigPath` from IConfiguration (appsettings.json or `DOCKER__CONFIGPATH` environment variable)
2. `DOCKER_CONFIG` environment variable (standard Docker convention)
3. `/root/.docker/config.json` (Linux container)
4. `~/.docker/config.json` (user profile fallback)
5. No auth (for public images)

## Error Handling

As of v0.5:
- If an image pull fails and **no local image** exists → **Error** (deployment is aborted)
- If an image pull fails but a **local image exists** → **Warning** (local image is used)

This prevents unintended deployments with outdated images.

## Current State (v0.5)

### Docker Config Mount (recommended)

The simplest method is mounting the Docker config file into the container:

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
- The file must be mounted to `/root/.docker/config.json` (not to a different directory)
- The `:ro` flag makes the mount read-only (recommended for security)
- The user on the host must have run `docker login` beforehand

### Configuration via IConfiguration

Alternatively, the path to the Docker config can be set via IConfiguration:

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

## Supported Registries

ReadyStackGo automatically detects the correct registry based on the image name:

| Image | Registry |
|-------|----------|
| `nginx:latest` | Docker Hub (`https://index.docker.io/v1/`) |
| `amssolution/myimage:v1` | Docker Hub (`https://index.docker.io/v1/`) |
| `ghcr.io/owner/image:tag` | GitHub Container Registry (`ghcr.io`) |
| `myregistry.azurecr.io/image` | Azure Container Registry (`myregistry.azurecr.io`) |
| `localhost:5000/image` | Local Registry (`localhost:5000`) |

## Planned: Registry Configuration (v0.6)

### Configuration File

Registries will be configured in `rsgo.registries.json`:

```json
{
  "registries": [
    {
      "id": "dockerhub-ams",
      "name": "AMS Docker Hub",
      "url": "https://index.docker.io/v1/",
      "username": "ams-service-user",
      "password": "base64-encoded-password",
      "isDefault": true,
      "imagePatterns": ["amssolution/*"]
    },
    {
      "id": "ghcr",
      "name": "GitHub Container Registry",
      "url": "ghcr.io",
      "username": "github-user",
      "password": "ghp_token...",
      "imagePatterns": ["ghcr.io/*"]
    }
  ]
}
```

### Fields

| Field | Description |
|-------|-------------|
| `id` | Unique ID of the registry |
| `name` | Display name |
| `url` | Registry URL (without protocol for custom, with protocol for Docker Hub) |
| `username` | Username |
| `password` | Password (Base64-encoded) |
| `isDefault` | Used for all images that don't match any pattern |
| `imagePatterns` | Glob patterns for image matching (e.g., `amssolution/*`, `ghcr.io/myorg/*`) |

### Image Matching

ReadyStackGo assigns images to a registry based on `imagePatterns`:

1. Image `amssolution/identityaccess:latest` → Matches `amssolution/*` → Registry `dockerhub-ams`
2. Image `ghcr.io/myorg/myimage:v1` → Matches `ghcr.io/*` → Registry `ghcr`
3. Image `nginx:latest` → No match → Default registry (if available) or Docker Hub public

## Planned: Registry Management UI (v0.8)

A web interface for managing registries:

- **Settings → Registries**
  - List of all configured registries
  - Add/Edit/Delete
  - Test button to verify credentials
  - Set default registry

## Security Notes

- Passwords are stored Base64-encoded (not encrypted)
- The configuration file should only be readable by the ReadyStackGo process
- Use `:ro` for read-only mounts
- For higher security: use environment variables or secret management (planned for future versions)

## Troubleshooting

### "pull access denied" Error

```
Failed to pull image 'amssolution/myimage:latest' and no local copy exists.
Error: pull access denied for amssolution/myimage, repository does not exist or may require 'docker login'
```

**Causes:**
1. No registry credentials configured
2. Wrong credentials
3. Image doesn't exist in the registry
4. Docker config not correctly mounted

**Solutions:**
1. Run `docker login` on the host
2. Mount Docker config correctly (see above)
3. Verify image name
4. Check logs for credential search details

### Docker Config Not Found

If ReadyStackGo can't find the Docker config, check the logs:

```
Looking for credentials for image amssolution/myimage, registry: https://index.docker.io/v1/
Docker config path: /root/.docker/config.json
Docker config file not found at /root/.docker/config.json
```

**Solutions:**
1. Add volume mount: `~/.docker/config.json:/root/.docker/config.json:ro`
2. Verify `~/.docker/config.json` exists on host
3. Set `DOCKER__CONFIGPATH` environment variable

### Credentials Not Recognized

If the config is found but no credentials are recognized:

```
Available registries in config: https://index.docker.io/v1/
Found credentials for registry https://index.docker.io/v1/
Using credentials for user myuser
```

If these lines **don't** appear:
1. Verify the registry key in config.json is correct
2. Docker Hub uses `https://index.docker.io/v1/` as key
3. Other registries use their domain (e.g., `ghcr.io`)

### Enable Debug Logging

For detailed logs, set the log level to Debug:

```yaml
environment:
  - Logging__LogLevel__ReadyStackGo.Infrastructure.Docker=Debug
```
