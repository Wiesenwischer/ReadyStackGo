---
title: GitHub Workflows
description: Overview of CI/CD workflows in ReadyStackGo
---

ReadyStackGo uses GitHub Actions for Continuous Integration and Deployment. All workflows are located in the `.github/workflows/` directory.

## CI (ci.yml)

**Trigger:** Push to `main`, `develop`, `feature/**` and Pull Requests

The main workflow for Continuous Integration:

### Build & Test Job
- .NET 9.0 Build and Restore
- Unit Tests, Integration Tests, Domain Tests
- Frontend: npm install, ESLint, TypeScript Check, Build
- GitVersion for automatic versioning
- Artifacts: Test results and build output

### E2E Tests Job
- Playwright-based End-to-End tests
- Runs after successful Build & Test
- Artifacts: Playwright Report (30 days)

---

## Docker Build (docker.yml)

**Trigger:** After successful CI workflow on `main`/`develop`, or on tags (`v*`)

Builds and pushes Docker images to Docker Hub:

- Multi-platform build (linux/amd64, linux/arm64)
- Automatic versioning via GitVersion
- SBOM (Software Bill of Materials) generation
- Image tags:
  - `latest` (main/tags only)
  - `develop` (develop branch only)
  - Semantic version (e.g., `0.6.1`)
- Updates Docker Hub README

**Required Secrets:**
- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

---

## Cloudflare Pages (cloudflare-pages.yml)

**Trigger:** Push to `main` with changes in `src/ReadyStackGo.PublicWeb/**`, or manually

Deploys the PublicWeb documentation site to Cloudflare Pages:

- Node.js 20 setup
- npm ci and build (Astro/Starlight)
- Deploy to Cloudflare Pages

**Required Secrets:**
- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`

---

## Release Drafter (release-drafter.yml)

**Trigger:** Push to `main` and Pull Requests

Automatically creates release drafts based on PR labels:

- Categorizes changes (Features, Bug Fixes, etc.)
- Generates release notes from PR titles
- Configuration in `.github/release-drafter.yml`

---

## Sync Changelog (sync-changelog.yml)

**Trigger:** Push to `main` with changes to `CHANGELOG.md`, or manually

Synchronizes CHANGELOG.md with PublicWeb release notes:

- Parses CHANGELOG.md (Keep a Changelog format)
- Generates release notes for DE and EN
- Automatically creates a Pull Request

---

## Third-Party Licenses (licenses.yml)

**Trigger:** Push to `main`/`develop` with changes to `*.csproj` or `package.json`, or manually

Generates license information for all dependencies:

- .NET dependencies via `dotnet-project-licenses`
- npm dependencies via `license-checker`
- Creates `THIRD-PARTY-LICENSES.md`
- Automatically creates a Pull Request

**Prerequisite:** Repository setting "Allow GitHub Actions to create and approve pull requests" must be enabled.

---

## Wiki Sync (wiki.yml)

**Trigger:** Push to `main` with changes in `docs/**`, or manually

Synchronizes the `/docs` folder structure with the GitHub Wiki:

- Copies all docs files to the wiki
- Generates `Home.md` as the home page
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
       │ (success)               │
       ▼                         ▼
  ┌─────────┐             ┌────────────┐
  │ Docker  │             │ E2E Tests  │
  └─────────┘             └────────────┘

Push to main (docs changes)
       │
       ├──► Wiki Sync
       │
       └──► Cloudflare Pages (if PublicWeb changed)

Push to main (CHANGELOG.md)
       │
       └──► Sync Changelog
```

## Secrets Overview

| Secret | Usage |
|--------|-------|
| `DOCKERHUB_USERNAME` | Docker Hub login |
| `DOCKERHUB_TOKEN` | Docker Hub access token |
| `CLOUDFLARE_API_TOKEN` | Cloudflare Pages API token |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare Account ID |
| `GITHUB_TOKEN` | Automatically provided by GitHub |
