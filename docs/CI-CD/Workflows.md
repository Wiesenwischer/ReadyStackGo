# GitHub Workflows

ReadyStackGo uses GitHub Actions for Continuous Integration and Deployment. All workflows are located in the `.github/workflows/` directory.

## CI (ci.yml)

**Trigger:** Push to `main`, `develop`, `feature/**` and Pull Requests

The main workflow for Continuous Integration:

### Build & Test Job
- .NET 9.0 build and restore
- Unit tests, integration tests, domain tests
- Frontend: npm install, ESLint, TypeScript check, build
- GitVersion for automatic versioning
- Artifacts: Test results and build output

### E2E Tests Job
- Playwright-based end-to-end tests
- Runs after successful build & test
- Artifacts: Playwright report (30 days)

---

## Docker Build (docker.yml)

**Trigger:** Tag `v*` (on every release)

Builds and pushes Docker images to Docker Hub:

- Multi-platform build (linux/amd64, linux/arm64)
- Automatic versioning via GitVersion
- SBOM (Software Bill of Materials) generation
- Image tags:
  - `latest`
  - Semantic version (e.g., `0.7.3`)
  - Minor version (e.g., `0.7`)
- Updates Docker Hub README

**Required Secrets:**
- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

---

## Docker Dev Build (docker-dev.yml)

**Trigger:** After successful CI workflow on `develop`

Builds and pushes dev images to GitHub Container Registry (ghcr.io):

- Multi-platform build (linux/amd64, linux/arm64)
- Image tag: `develop` (overwritten on each build)
- Git commit SHA as label (`org.opencontainers.image.revision`)

**Image:** `ghcr.io/wiesenwischer/readystackgo:develop`

**Read commit SHA:**
```bash
docker inspect ghcr.io/wiesenwischer/readystackgo:develop \
  --format '{{index .Config.Labels "org.opencontainers.image.revision"}}'
```

**No additional secrets required** (uses `GITHUB_TOKEN`)

---

## Cloudflare Pages (cloudflare-pages.yml)

**Trigger:** Tag `v*` (on every release), or manually

Deploys the PublicWeb documentation site to Cloudflare Pages:

- Node.js 20 setup
- npm ci and build (Astro/Starlight)
- Deploy to Cloudflare Pages
- Release notes are fetched from GitHub API at build time

**Required Secrets:**
- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`

---

## Release Drafter (release-drafter.yml)

**Trigger:** Push to `main` and Pull Requests

Creates release drafts that are manually published:

- **On PRs:** Updates draft release, sets labels via autolabeler
- **On push to main:** Updates draft (no auto-publish)
- Categorizes changes (Features, Bug Fixes, etc.)
- Generates release notes from PR titles
- Calculates version from PR labels (major/minor/patch)
- **Manually click "Publish release"** → Tag is created → Docker + Cloudflare trigger
- Configuration in `.github/release-drafter.yml`

---

## Third-Party Licenses (licenses.yml)

**Trigger:** Push to `main`/`develop` on changes to `*.csproj` or `package.json`, or manually

Generates license information for all dependencies:

- .NET dependencies via `dotnet-project-licenses`
- npm dependencies via `license-checker`
- Creates `THIRD-PARTY-LICENSES.md`
- Automatically creates a pull request

**Prerequisite:** Repository setting "Allow GitHub Actions to create and approve pull requests" must be enabled.

---

## Wiki Sync (wiki.yml)

**Trigger:** Push to `main` on changes in `docs/**`, or manually

Synchronizes the `/docs` folder structure with the GitHub Wiki:

- Copies all docs files to wiki
- Flattens folder structure (e.g., `Architecture/Overview.md` → `Architecture-Overview.md`)
- Generates `_Sidebar.md` for navigation
- Commits and pushes automatically

---

## Workflow Dependencies

```
Push to main/develop
       │
       ▼
    ┌─────┐
    │ CI  │ ─────────────────────┐
    └──┬──┘                      │
       │ (success, develop)      ▼
       ▼                  ┌────────────┐
  ┌────────────┐          │ E2E Tests  │
  │ Docker Dev │          └────────────┘
  │  (ghcr.io) │
  └────────────┘

Push to main
       │
       ├──► Release Drafter (update draft)
       │
       └──► Wiki Sync (if docs/ changed)

Manually click "Publish release"
       │
       └──► Tag v* is created
                 │
                 ├──► Docker (version tag)
                 └──► Cloudflare Pages
```

## Secrets Overview

| Secret | Usage |
|--------|-------|
| `DOCKERHUB_USERNAME` | Docker Hub Login |
| `DOCKERHUB_TOKEN` | Docker Hub Access Token |
| `CLOUDFLARE_API_TOKEN` | Cloudflare Pages API Token |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare Account ID |
| `GITHUB_TOKEN` | Automatically provided by GitHub |
