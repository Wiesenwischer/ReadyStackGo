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
  Publish that draft — GitHub UI or `gh release edit --draft=false`
       │
       ▼
  Tag v* is created automatically
       │
       ├──► Docker workflow builds & pushes to Docker Hub
       └──► Cloudflare Pages deploys documentation
```

## Step-by-Step Release

### 1. Merge to Main

All features and fixes are merged to `main` via Pull Requests from `feature/<name>` or
`bugfix/<name>` branches (`main` is the only permanent branch — see
[Git Workflow](../Development/Git-Workflow.md)).

### 2. Review Release Draft

Go to [GitHub Releases](../../releases) and review the draft release:
- Release Drafter automatically categorizes PRs
- Version is calculated from PR labels (major/minor/patch)
- Edit title and notes if needed

### 3. Publish Release

**Always publish the existing draft** — either via **"Publish release"** in the GitHub UI, or
from the CLI:

```bash
# Find the draft (it already carries the next version as tag_name; the tag does not exist yet)
gh api repos/Wiesenwischer/ReadyStackGo/releases \
  --jq '.[] | select(.draft==true) | "id=\(.id) tag=\(.tag_name)"'

# Replace the auto-generated PR list with handwritten highlights, then publish
gh release edit v0.10.0 --notes-file release-notes.md --target main --draft=false --latest
```

Either way, a Git tag (e.g. `v0.10.0`) is created and triggers the Docker and Cloudflare
workflows.

> **Do not use `gh release create`.** Release Drafter keeps a draft up to date on every push to
> `main`. Creating a separate release leaves that draft behind, so the release list ends up with
> two entries carrying the same tag — one published, one draft. `gh release edit`/`delete <tag>`
> are then ambiguous between them, and cleaning up requires the numeric release id
> (`gh api -X DELETE repos/.../releases/<id>`).

The drafter's raw "What's Changed" PR list is not sufficient as release notes: write the notes
to a file first, with highlights per PR (see v0.81.0 / v0.82.0 for the expected style).

### 4. Verify Deployment

After publishing:
- Check [Docker Hub](https://hub.docker.com/r/wiesenwischer/readystackgo) for new image
- Check [Documentation Site](https://readystackgo.pages.dev) for updates
- Confirm no draft was left behind:
  `gh api repos/Wiesenwischer/ReadyStackGo/releases --jq '[.[]|select(.draft==true)]|length'`
  (a fresh draft for the *next* version only appears after the next merge to `main`)

## Docker Images

### Production (Docker Hub)

Triggered by release tags (`v*`):

| Tag | Description |
|-----|-------------|
| `wiesenwischer/readystackgo:latest` | Latest stable release |
| `wiesenwischer/readystackgo:0.10.0` | Full semantic version |
| `wiesenwischer/readystackgo:0.10` | Minor version (always latest patch) |

### Development (GitHub Container Registry)

Triggered by push to `main` (after PR merge; tag pushes are ignored — those are handled by the
release workflow):

| Tag | Description |
|-----|-------------|
| `ghcr.io/wiesenwischer/readystackgo:latest` | Latest build from `main` |

The dev image is overwritten with each build. Use the image label to identify the commit:
```bash
docker inspect ghcr.io/wiesenwischer/readystackgo:latest \
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
3. Create a PR to `main`
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
