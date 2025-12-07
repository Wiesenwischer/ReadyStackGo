# Release Management

This document describes the release process for ReadyStackGo.

## Release Process Overview

ReadyStackGo uses a **tag-based release process**. Releases are NOT automatically triggered by merging to `main`.

```
PR merged to main
       │
       ▼
  Release Drafter updates draft
       │
       ▼ (manual)
  Publish Release in GitHub UI
       │
       ▼
  Tag v* is created automatically
       │
       ├──► Docker workflow builds & pushes to Docker Hub
       └──► Cloudflare Pages deploys documentation
```

## Step-by-Step Release

### 1. Merge to Main

All features and fixes are merged to `main` via Pull Requests from `develop`.

### 2. Review Release Draft

Go to [GitHub Releases](../../releases) and review the draft release:
- Release Drafter automatically categorizes PRs
- Version is calculated from PR labels (major/minor/patch)
- Edit title and notes if needed

### 3. Publish Release

Click **"Publish release"** in GitHub UI:
- A Git tag (e.g., `v0.10.0`) is created automatically
- This triggers the Docker and Cloudflare workflows

### 4. Verify Deployment

After publishing:
- Check [Docker Hub](https://hub.docker.com/r/wiesenwischer/readystackgo) for new image
- Check [Documentation Site](https://readystackgo.pages.dev) for updates

## Docker Images

### Production (Docker Hub)

Triggered by release tags (`v*`):

| Tag | Description |
|-----|-------------|
| `wiesenwischer/readystackgo:latest` | Latest stable release |
| `wiesenwischer/readystackgo:0.10.0` | Full semantic version |
| `wiesenwischer/readystackgo:0.10` | Minor version (always latest patch) |

### Development (GitHub Container Registry)

Triggered by push to `develop` (after PR merge):

| Tag | Description |
|-----|-------------|
| `ghcr.io/wiesenwischer/readystackgo:develop` | Latest develop build |

The develop image is overwritten with each build. Use the image label to identify the commit:
```bash
docker inspect ghcr.io/wiesenwischer/readystackgo:develop \
  --format '{{index .Config.Labels "org.opencontainers.image.revision"}}'
```

## Manual Release (if needed)

If Release Drafter is not available, create a tag manually:

```bash
# Create and push tag
git tag v0.10.0
git push origin v0.10.0
```

This will trigger the same workflows as publishing via GitHub UI.

## Hotfix Process

For urgent fixes:
1. Create a hotfix branch from `main`
2. Apply the fix
3. Create a PR to `main` (bypassing `develop` if urgent)
4. Merge and publish release as normal

## Pre-Release Versions

For pre-releases, use appropriate suffixes:
- `v0.10.0-alpha.1`
- `v0.10.0-beta.1`
- `v0.10.0-rc.1`

Mark as "pre-release" in GitHub UI to prevent it from being shown as "latest".

## See Also

- [GitHub Workflows](../CI-CD/Workflows.md)
- [Git Workflow](../Development/Git-Workflow.md)
