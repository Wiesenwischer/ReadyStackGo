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

**Trigger:** Nach erfolgreichem CI-Workflow auf `main`/`develop`, oder bei Tags (`v*`)

Baut und pusht Docker Images zu Docker Hub:

- Multi-Platform Build (linux/amd64, linux/arm64)
- Automatische Versionierung via GitVersion
- SBOM (Software Bill of Materials) Generierung
- Image Tags:
  - `latest` (nur main/tags)
  - `develop` (nur develop Branch)
  - Semantische Version (z.B. `0.6.1`)
- Aktualisiert Docker Hub README

**Benötigte Secrets:**
- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

---

## Cloudflare Pages (cloudflare-pages.yml)

**Trigger:** Push auf `main` bei Änderungen in `src/ReadyStackGo.PublicWeb/**`, oder manuell

Deployed die PublicWeb Dokumentationsseite zu Cloudflare Pages:

- Node.js 20 Setup
- npm ci und Build (Astro/Starlight)
- Deploy zu Cloudflare Pages

**Benötigte Secrets:**
- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`

---

## Release Drafter (release-drafter.yml)

**Trigger:** Push auf `main` und Pull Requests

Erstellt automatisch Release-Entwürfe basierend auf PR-Labels:

- Kategorisiert Änderungen (Features, Bug Fixes, etc.)
- Generiert Release Notes aus PR-Titeln
- Konfiguration in `.github/release-drafter.yml`

---

## Sync Changelog (sync-changelog.yml)

**Trigger:** Push auf `main` bei Änderungen an `CHANGELOG.md`, oder manuell

Synchronisiert CHANGELOG.md mit den PublicWeb Release Notes:

- Parst CHANGELOG.md (Keep a Changelog Format)
- Generiert Release Notes für DE und EN
- Erstellt automatisch einen Pull Request

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
       │ (success)               │
       ▼                         ▼
  ┌─────────┐             ┌────────────┐
  │ Docker  │             │ E2E Tests  │
  └─────────┘             └────────────┘

Push zu main (docs änderungen)
       │
       ├──► Wiki Sync
       │
       └──► Cloudflare Pages (wenn PublicWeb geändert)

Push zu main (CHANGELOG.md)
       │
       └──► Sync Changelog
```

## Secrets-Übersicht

| Secret | Verwendung |
|--------|------------|
| `DOCKERHUB_USERNAME` | Docker Hub Login |
| `DOCKERHUB_TOKEN` | Docker Hub Access Token |
| `CLOUDFLARE_API_TOKEN` | Cloudflare Pages API Token |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare Account ID |
| `GITHUB_TOKEN` | Automatisch von GitHub bereitgestellt |
