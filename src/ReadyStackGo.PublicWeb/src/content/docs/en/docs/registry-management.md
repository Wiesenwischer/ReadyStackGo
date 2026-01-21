---
title: Registry Management
description: Manage Docker Registries and configure Image Patterns
---

This guide shows you how to manage Docker Registries in ReadyStackGo to pull images from private registries.

## Overview

ReadyStackGo supports pulling Docker images from any OCI-compliant registry. Through the Settings page, you can centrally manage registry credentials and automatically assign them to the correct registries using **Image Patterns**.

### Credential Resolution

When pulling an image, ReadyStackGo searches for credentials in the following order:

1. **Database Registries** - Registries from Settings with matching Image Patterns
2. `DOCKER__CONFIGPATH` - Path from environment variable or appsettings.json
3. `DOCKER_CONFIG` - Standard Docker convention (directory)
4. `/root/.docker/config.json` - Default path in Linux container
5. **No Auth** - For public images

---

## Adding a Registry

1. Navigate to **Settings** in the sidebar
2. Click **Add Registry**
3. Fill in the fields:

| Field | Description |
|-------|-------------|
| **Name** | Display name (e.g., "Docker Hub - Company") |
| **URL** | Registry URL (e.g., `https://index.docker.io/v1/`) |
| **Username** | Optional - for private registries |
| **Password** | Optional - for private registries |
| **Image Patterns** | Optional - Glob patterns for automatic matching |

4. Click **Save**

---

## Image Patterns

Image Patterns determine which registry credentials are used for which images. They use glob-style syntax:

### Pattern Syntax

| Pattern | Description | Example Matches |
|---------|-------------|-----------------|
| `library/*` | Single path segment | `library/nginx`, `library/redis` |
| `myorg/**` | Any number of path segments | `myorg/app`, `myorg/team/app` |
| `ghcr.io/**` | Registry-specific | `ghcr.io/owner/repo` |
| `nginx` | Exact match | Only `nginx` |

### Pattern Rules

- `*` matches any characters within **one** path segment
- `**` matches any characters across **multiple** path segments
- Patterns are **case-insensitive**
- Tags and digests are **ignored** during matching

### Example Configuration

| Registry | Image Patterns | Used For |
|----------|----------------|----------|
| Docker Hub (Company) | `mycompany/*`, `mycompany/**` | Company images on Docker Hub |
| GitHub Container Registry | `ghcr.io/**` | All GitHub Packages |
| Azure Container Registry | `myacr.azurecr.io/**` | Azure-hosted images |
| Default (Docker Hub) | *(none - marked as default)* | All other public images |

---

## Default Registry

You can mark a registry as **Default**. This registry is used for all images that don't match any pattern:

1. Open the registry in Settings
2. Enable **Set as Default**
3. Save

:::note[Only One Default Registry]
Only one registry can be marked as default. When setting a new default, the previous one is automatically deactivated.
:::

---

## Supported Registries

ReadyStackGo works with any OCI-compliant registry:

| Registry | URL Format |
|----------|------------|
| Docker Hub | `https://index.docker.io/v1/` |
| GitHub Container Registry | `https://ghcr.io` |
| Azure Container Registry | `https://<name>.azurecr.io` |
| Google Container Registry | `https://gcr.io` |
| Amazon ECR | `https://<account>.dkr.ecr.<region>.amazonaws.com` |
| Self-hosted | `https://registry.example.com` |

---

## Troubleshooting

### "pull access denied" Error

```
Failed to pull image 'mycompany/myimage:latest' and no local copy exists.
Error: pull access denied for mycompany/myimage
```

**Causes:**
1. No registry credentials configured
2. Wrong credentials
3. Image pattern doesn't match
4. Image doesn't exist in the registry

**Solutions:**
1. Add a registry via Settings
2. Configure an Image Pattern that matches your image
3. Verify credentials
4. Check the image name

### Registry Not Being Used

If configured credentials are not being used:

1. **Check Image Patterns** - Does the pattern match your image?
2. **Verify Pattern Syntax** - Use `*` for one segment, `**` for multiple
3. **More Specific Patterns** - Does another registry have a more specific pattern?

### Enable Debug Logging

For detailed credential resolution logs:

```yaml
environment:
  - Logging__LogLevel__ReadyStackGo.Infrastructure.Docker=Debug
```

---

## Security Notes

- Passwords are stored in the SQLite database (not encrypted at rest)
- Restrict access to the ReadyStackGo instance
- The database file should only be readable by the ReadyStackGo process
- Use service accounts instead of personal credentials

---

## Related Links

- [Stack Deployment](/en/docs/stack-deployment/) - Deploy stacks
- [Installation](/en/getting-started/installation/) - Install ReadyStackGo
