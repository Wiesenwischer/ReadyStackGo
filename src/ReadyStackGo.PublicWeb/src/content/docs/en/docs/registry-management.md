---
title: Registry Management
description: Manage Docker Registries and configure Image Patterns
---

This guide shows you how to manage Docker Registries in ReadyStackGo to pull images from private registries.

## Overview

ReadyStackGo supports pulling Docker images from any OCI-compliant registry. Through the Settings page, you can centrally manage registry credentials and automatically assign them to the correct registries using **Image Patterns**.

| Feature | Description |
|---------|-------------|
| **Add Registry** | Configure Docker Hub, GHCR, GitLab, Quay.io or custom registries |
| **Manage Credentials** | Store username/password or token for private registries |
| **Image Patterns** | Automatic assignment of images to registries via glob patterns |
| **Default Registry** | Fallback registry for images without a pattern match |
| **Edit Registry** | Modify name, URL, credentials and patterns after creation |
| **Delete Registry** | Remove registry configuration including credentials |

### Credential Resolution

When pulling an image, ReadyStackGo searches for credentials in the following order:

1. **Database Registries** - Registries from Settings with matching Image Patterns
2. `DOCKER__CONFIGPATH` - Path from environment variable or appsettings.json
3. `DOCKER_CONFIG` - Standard Docker convention (directory)
4. `/root/.docker/config.json` - Default path in Linux container
5. **No Auth** - For public images

---

## Step by Step: Adding a Registry

### Step 1: Navigate to Settings

Open the **Settings** page from the sidebar navigation. You will find the **Container Registries** card there.

![Settings page with Container Registries card](/images/docs/registry-01-settings-nav.png)

---

### Step 2: Open Registry Overview

Click on the **Container Registries** card. On first access, an empty state is shown – no registries configured yet.

![Empty state with no configured registries](/images/docs/registry-02-empty-state.png)

Click **Add Your First Registry** or the **Add Registry** button in the top right.

---

### Step 3: Configure the Registry

Fill out the form. ReadyStackGo provides templates for well-known registries:

| Registry | URL |
|----------|-----|
| Docker Hub | `https://index.docker.io/v1/` |
| GitHub Container Registry | `https://ghcr.io` |
| GitLab Container Registry | `https://registry.gitlab.com` |
| Quay.io | `https://quay.io` |
| Custom | Enter any URL |

Select a registry type from the dropdown – name and URL are filled automatically. Optionally configure **Credentials** (Username + Password/Token) and **Image Patterns**.

![Registry form with Docker Hub and credentials filled in](/images/docs/registry-03-add-form.png)

:::tip[Image Patterns]
Image Patterns automatically determine which registry is used for which image. Without patterns, the registry is only used as default or manually assigned.
:::

---

### Step 4: Registry in the List

After saving, the registry appears in the overview. Badges indicate the status:
- **Authenticated** – Credentials are stored
- **Default** – This registry is used as fallback

![Registry list with newly created Docker Hub registry](/images/docs/registry-04-list-with-registry.png)

From here you can **edit** registries, **set as default**, or **delete** them.

---

## Editing a Registry

Click **Edit** on a registry to modify its name, URL, credentials, or image patterns.

![Edit form of a registry with modified data](/images/docs/registry-05-edit-form.png)

:::note[Updating Credentials]
Existing passwords are not displayed. Leave the field empty to keep the current password, or enable **Clear existing credentials** to completely remove the credentials.
:::

---

## Deleting a Registry

Click **Delete** on a registry. A confirmation page appears showing the registry details.

![Delete confirmation with registry details and warning](/images/docs/registry-06-delete-confirm.png)

:::caution[Irreversible]
Deleting a registry removes all stored credentials and configurations. This action cannot be undone.
:::

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

You can mark a registry as **Default**. This registry is used for all images that don't match any pattern. Click **Set Default** in the registry list.

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
