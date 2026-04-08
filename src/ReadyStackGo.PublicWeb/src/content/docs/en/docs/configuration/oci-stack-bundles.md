---
title: OCI Stack Bundles
description: Pull stack definitions from Docker/OCI container registries for versioned, CI/CD-driven stack distribution
---

ReadyStackGo can pull stack definitions directly from **OCI container registries** like Docker Hub, GitHub Container Registry (GHCR), or Azure Container Registry. This enables versioned stack distribution via CI/CD pipelines and deterministic deployments with lock files.

## Overview

| Feature | Description |
|---------|-------------|
| **OCI Registry Source** | Add a container registry as a stack source |
| **Tag-based Versioning** | Each registry tag represents a stack version |
| **Glob Tag Filtering** | Filter tags with patterns like `v*`, `ams-*` |
| **Lock File Support** | Pin images to digests for deterministic deployments |
| **Test Connection** | Verify registry access before adding a source |
| **Authentication** | Support for private registries with username/password |

---

## Step by Step: Adding an OCI Registry Source

### Step 1: Navigate to Stack Sources

Go to **Settings** → **Stack Sources** → **Add Source**.

### Step 2: Select OCI Registry

Choose **OCI Registry** from the source type selection.

### Step 3: Configure the Source

Fill in the registry details:

- **Source ID** – Unique identifier (e.g., `my-oci-stacks`)
- **Display Name** – Human-readable name
- **Registry Host** – Registry hostname without protocol (e.g., `docker.io`, `ghcr.io`, `myregistry.azurecr.io`)
- **Repository** – Full repository path (e.g., `myorg/rsgo-stacks`)
- **Tag Pattern** – Glob pattern to filter tags (default: `*` = all tags)

:::tip[Tag Pattern Examples]
- `*` – All tags
- `v*` – Only tags starting with "v" (e.g., `v1.0.0`, `v2.1.3`)
- `ams-*` – Only tags with "ams-" prefix
- `?.*.*` – Tags like `1.0.0`, `2.1.3` (single digit major version)
:::

### Step 4: Configure Authentication (Optional)

For private registries, provide credentials:

- **Username** – Registry username
- **Password / Token** – Access token or password

| Registry | Username | Password |
|----------|----------|----------|
| Docker Hub | Docker Hub username | Access Token |
| GHCR | GitHub username | PAT with `read:packages` |
| Azure CR | Service Principal ID | Service Principal Secret |

### Step 5: Test Connection

Click **Test Connection** to verify access. The test lists available tags and shows a preview of the first 10 tags.

### Step 6: Create Source

Click **Create Source** to add the registry. RSGO will immediately sync and load stacks from matching tags.

---

## OCI Stack Bundle Format

An OCI Stack Bundle is a container image with stack definition files as layers.

### Bundle Structure

| Layer | Media Type | Content | Required |
|-------|-----------|---------|----------|
| 1 | `application/vnd.rsgo.stack.manifest.v1+yaml` | `stack.yaml` – RSGO Manifest | Yes |
| 2 | `application/vnd.rsgo.stack.lock.v1+json` | `lock.json` – Image Digests | No |
| 3 | `application/vnd.rsgo.stack.meta.v1+json` | `meta.json` – Marketplace Metadata | No |

### Alternative: Standard Docker Image

You can also use a standard Docker image with stack files at known paths:

```
/rsgo/stack.yaml    # or stack.yml
/rsgo/lock.json     # optional
/rsgo/meta.json     # optional
```

RSGO automatically extracts these files from tar.gz layers during sync.

---

## Lock Files

A lock file pins each service image to a specific **digest** (`sha256:...`) instead of a mutable tag. This ensures deployments use the exact same image regardless of tag changes.

### lock.json Format

```json
{
  "apiVersion": "1",
  "stackName": "my-app",
  "stackVersion": "1.0.0",
  "images": [
    {
      "name": "web",
      "image": "nginx",
      "tag": "1.25-alpine",
      "digest": "sha256:a8281ce42034b078dc7d88a5bfe6cb40...",
      "role": "main"
    },
    {
      "name": "cache",
      "image": "redis",
      "tag": "7-alpine",
      "digest": "sha256:e422889e156a60c4e7f0ba0c3e5b..."
    }
  ]
}
```

### How Digest Resolution Works

1. If a lock file exists and contains an entry for a service → `image@sha256:digest`
2. If no lock entry exists for a service → falls back to `image:tag`
3. Lock files are optional — deployments work without them

---

## Publishing OCI Stack Bundles

### Using a Dockerfile

```dockerfile
FROM scratch
COPY stack.yaml /rsgo/stack.yaml
COPY lock.json /rsgo/lock.json
```

```bash
docker build -t ghcr.io/myorg/rsgo-stacks:v1.0.0 .
docker push ghcr.io/myorg/rsgo-stacks:v1.0.0
```

### Using ORAS CLI

```bash
oras push ghcr.io/myorg/rsgo-stacks:v1.0.0 \
  stack.yaml:application/vnd.rsgo.stack.manifest.v1+yaml \
  lock.json:application/vnd.rsgo.stack.lock.v1+json
```

### GitHub Actions Example

```yaml
name: Publish Stack Bundle
on:
  push:
    tags: ['v*']

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Log in to GHCR
        run: echo "${{ secrets.GITHUB_TOKEN }}" | docker login ghcr.io -u ${{ github.actor }} --password-stdin

      - name: Build and push bundle
        run: |
          docker build -t ghcr.io/${{ github.repository }}/stacks:${{ github.ref_name }} .
          docker push ghcr.io/${{ github.repository }}/stacks:${{ github.ref_name }}
```

---

## Caching and Sync Behavior

- Downloaded manifests are cached in `~/.rsgo/oci-cache/{sourceId}/{tag}/`
- On sync, RSGO checks the manifest **digest** — if unchanged, the cached version is used
- Only new or changed tags trigger a re-download
- Pagination is supported for repositories with more than 100 tags

---

## API Reference

### Test Connection

```
POST /api/stack-sources/test-oci-connection
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `registryUrl` | string | Yes | Registry hostname |
| `repository` | string | Yes | Repository path |
| `username` | string | No | Registry username |
| `password` | string | No | Registry password |

**Response:**

```json
{
  "success": true,
  "message": "Connection successful. Found 15 tag(s).",
  "tagCount": 15,
  "sampleTags": ["v1.0.0", "v1.1.0", "v2.0.0"]
}
```

### Create OCI Registry Source

```
POST /api/stack-sources
```

```json
{
  "id": "my-oci-stacks",
  "name": "My OCI Stacks",
  "type": "OciRegistry",
  "registryUrl": "ghcr.io",
  "repository": "myorg/rsgo-stacks",
  "tagPattern": "v*",
  "registryUsername": "user",
  "registryPassword": "token"
}
```
