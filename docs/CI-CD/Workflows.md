# GitHub Workflows

ReadyStackGo verwendet GitHub Actions für Continuous Integration und Deployment. Alle Workflows befinden sich im Verzeichnis `.github/workflows/`.

## CI (ci.yml)

**Trigger:** Push auf `main`, `develop`, `feature/**` und Pull Requests

Der Hauptworkflow für Continuous Integration:

### Build & Test Job
- .NET 9.0 Build und Restore
- Unit Tests, Integration Tests, Domain Tests
- Frontend: npm install, ESLint, TypeScript Check, Build
- GitVersion für automatische Versionierung
- Artefakte: Test-Results und Build-Output

### E2E Tests Job
- Playwright-basierte End-to-End Tests
- Läuft nach erfolgreichem Build & Test
- Artefakte: Playwright Report (30 Tage)

---

## Docker Build (docker.yml)

**Trigger:** Tag `v*` (bei jedem Release)

Baut und pusht Docker Images zu Docker Hub:

- Multi-Platform Build (linux/amd64, linux/arm64)
- Automatische Versionierung via GitVersion
- SBOM (Software Bill of Materials) Generierung
- Image Tags:
  - `latest`
  - Semantische Version (z.B. `0.7.3`)
  - Minor Version (z.B. `0.7`)
- Aktualisiert Docker Hub README

**Benötigte Secrets:**
- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

---

## Docker Dev Build (docker-dev.yml)

**Trigger:** Nach erfolgreichem CI-Workflow auf `develop`

Baut und pusht Dev-Images zu GitHub Container Registry (ghcr.io):

- Multi-Platform Build (linux/amd64, linux/arm64)
- Image Tag: `develop` (wird bei jedem Build überschrieben)
- Git Commit SHA als Label (`org.opencontainers.image.revision`)

**Image:** `ghcr.io/wiesenwischer/readystackgo:develop`

**Commit SHA auslesen:**
```bash
docker inspect ghcr.io/wiesenwischer/readystackgo:develop \
  --format '{{index .Config.Labels "org.opencontainers.image.revision"}}'
```

**Keine zusätzlichen Secrets erforderlich** (nutzt `GITHUB_TOKEN`)

---

## Cloudflare Pages (cloudflare-pages.yml)

**Trigger:** Tag `v*` (bei jedem Release), oder manuell

Deployed die PublicWeb Dokumentationsseite zu Cloudflare Pages:

- Node.js 20 Setup
- npm ci und Build (Astro/Starlight)
- Deploy zu Cloudflare Pages
- Release Notes werden zur Build-Zeit von GitHub API geholt

**Benötigte Secrets:**
- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`

---

## Release Drafter (release-drafter.yml)

**Trigger:** Push auf `main` (published Release) und Pull Requests (Draft aktualisieren)

Automatisiert den gesamten Release-Prozess:

- **Bei PRs:** Aktualisiert Draft Release, setzt Labels via Autolabeler
- **Bei Push auf main:** Veröffentlicht das Release mit Tag
- Kategorisiert Änderungen (Features, Bug Fixes, etc.)
- Generiert Release Notes aus PR-Titeln
- Berechnet Version aus PR-Labels (major/minor/patch)
- Konfiguration in `.github/release-drafter.yml`

---

## Third-Party Licenses (licenses.yml)

**Trigger:** Push auf `main`/`develop` bei Änderungen an `*.csproj` oder `package.json`, oder manuell

Generiert Lizenzinformationen für alle Dependencies:

- .NET Dependencies via `dotnet-project-licenses`
- npm Dependencies via `license-checker`
- Erstellt `THIRD-PARTY-LICENSES.md`
- Erstellt automatisch einen Pull Request

**Voraussetzung:** Repository-Einstellung "Allow GitHub Actions to create and approve pull requests" muss aktiviert sein.

---

## Wiki Sync (wiki.yml)

**Trigger:** Push auf `main` bei Änderungen in `docs/**`, oder manuell

Synchronisiert die `/docs` Ordnerstruktur mit dem GitHub Wiki:

- Kopiert alle Docs-Dateien ins Wiki
- Flacht Ordnerstruktur ab (z.B. `Architecture/Overview.md` → `Architecture-Overview.md`)
- Generiert `_Sidebar.md` für Navigation
- Committet und pusht automatisch

---

## Workflow-Abhängigkeiten

```
Push zu main/develop
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

Push zu main
       │
       ├──► Release Drafter (published Release + Tag)
       │           │
       │           └──► Tag v* triggert:
       │                    ├──► Docker (Version-Tag)
       │                    └──► Cloudflare Pages
       │
       └──► Wiki Sync (wenn docs/ geändert)
```

## Secrets-Übersicht

| Secret | Verwendung |
|--------|------------|
| `DOCKERHUB_USERNAME` | Docker Hub Login |
| `DOCKERHUB_TOKEN` | Docker Hub Access Token |
| `CLOUDFLARE_API_TOKEN` | Cloudflare Pages API Token |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare Account ID |
| `GITHUB_TOKEN` | Automatisch von GitHub bereitgestellt |
