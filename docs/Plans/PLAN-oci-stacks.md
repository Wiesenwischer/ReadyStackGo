<!-- GitHub Epic: #285 -->
# Phase: OCI Stack Bundles

## Ziel

OCI-basierte Stack-Verteilung: Versionierte Stack-Bundles in Container-Registries (Docker Hub, GHCR, Azure CR etc.) veröffentlichen und in ReadyStackGo als Stack Source nutzen. Ermöglicht CI/CD-getriebenes Stack-Publishing und deterministische Deployments via Lock Files.

## Analyse

### Bestehende Architektur

Das Stack Source System ist über `IProductSourceProvider` erweiterbar:

- **`StackSourceType`** Enum: `LocalDirectory`, `GitRepository` — wird um `OciRegistry` erweitert
- **`StackSource`** Aggregate Root: Type-spezifische Properties (Path, GitUrl etc.) — wird um Registry-Properties erweitert
- **`IProductSourceProvider`** Interface: `CanHandle()` + `LoadProductsAsync()` — neuer `OciRegistryProductSourceProvider`
- **`StackSourceConfig`** (rsgo.stacks.json): Polymorphe Serialisierung via `[JsonDerivedType]` — neue `OciRegistrySourceEntry`
- **`RegistryAccessChecker`**: Implementiert bereits Docker Registry v2 Token-Flow (Bearer realm, anonymous/authenticated) — kann für OCI-Zugriff wiederverwendet werden

### Referenz-Implementierungen

| Pattern | Bestehende Implementierung | OCI Pendant |
|---|---|---|
| Factory Method | `StackSource.CreateGitRepository()` | `StackSource.CreateOciRegistry()` |
| Provider | `GitRepositoryProductSourceProvider` | `OciRegistryProductSourceProvider` |
| Config Entry | `GitRepositorySourceEntry` | `OciRegistrySourceEntry` |
| Handler Switch | `CreateStackSourceHandler` case `"gitrepository"` | case `"ociregistry"` |
| UI Page | `AddGitSource.tsx` | `AddOciRegistrySource.tsx` |

### Betroffene Bounded Contexts

- **Domain**: `StackSourceType.OciRegistry`, `StackSource` Properties (RegistryUrl, Repository, RegistryUsername, RegistryPassword, TagPattern), `OciLockFile` Value Object
- **Application**: `CreateStackSourceCommand/Handler` Erweiterung, `IProductSourceProvider` neue Implementierung
- **Infrastructure**: `OciRegistryProductSourceProvider`, `OciRegistryClient` (Docker v2 API), `OciRegistrySourceEntry`, `OciBundleParser`
- **API**: `CreateSourceEndpoint` / `UpdateSourceEndpoint` — OciRegistry-Felder
- **WebUI (rsgo-generic)**: `AddOciRegistrySource.tsx`, `AddStackSourceSelect.tsx` Erweiterung

## AMS UI Counterpart

> RSGO has two UI distributions with different design systems:
> - **rsgo-generic**: React + Tailwind CSS (reference implementation, `packages/ui-generic`)
> - **AMS UI**: ConsistentUI/Lit web components (separate repo `ReadyStackGo.Ams`)
>
> Shared logic lives in `@rsgo/core` (hooks, API calls, state). Pages/layouts must be reimplemented per distribution.

- [x] **Ja (deferred)** — AMS-Counterpart wird nach Fertigstellung in rsgo-generic geplant

## Features / Schritte

### Phase 1 — OCI Registry Source

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: Domain — OciRegistry StackSourceType** – Enum-Erweiterung + StackSource Properties + Factory Method
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/StackManagement/Sources/StackSourceType.cs` — neuer Enum-Wert `OciRegistry`
    - `src/ReadyStackGo.Domain/StackManagement/Sources/StackSource.cs` — neue Properties: `RegistryUrl`, `Repository`, `RegistryUsername`, `RegistryPassword` (encrypted), `TagPattern`; Factory Method `CreateOciRegistry()`; `UpdateRegistryCredentials()` Methode
  - Pattern-Vorlage: `StackSource.CreateGitRepository()` (Zeile 69-95)
  - Abhängig von: -

- [ ] **Feature 2: Infrastructure — OCI Registry Client** – HTTP Client für Docker Registry v2 API
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/StackSources/OciRegistryClient.cs` (neu)
  - Funktionalität:
    - `ListTagsAsync(registryUrl, repository)` — GET `/v2/{name}/tags/list` mit Pagination
    - `GetManifestAsync(registryUrl, repository, tag)` — GET `/v2/{name}/manifests/{reference}` (Accept: `application/vnd.oci.image.manifest.v1+json`, `application/vnd.docker.distribution.manifest.v2+json`)
    - `PullLayerAsync(registryUrl, repository, digest)` — GET `/v2/{name}/blobs/{digest}`
    - Token-Auth via `RegistryAccessChecker`-Pattern (Bearer realm + service + scope)
    - Docker Hub Sonderbehandlung (`registry-1.docker.io`)
  - Pattern-Vorlage: `RegistryAccessChecker` (Token-Flow in Zeile 118-186)
  - Abhängig von: -

- [ ] **Feature 3: Infrastructure — OciRegistryProductSourceProvider** – Sync-Logik
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/StackSources/OciRegistryProductSourceProvider.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/DependencyInjection.cs` — DI Registration
  - Funktionalität:
    - `CanHandle()` → `source.Type == StackSourceType.OciRegistry`
    - `LoadProductsAsync()`:
      1. Tags listen via `OciRegistryClient.ListTagsAsync()`
      2. Tags nach `TagPattern` filtern (Glob-Matching)
      3. StackId + Version aus Tag parsen (Konvention: `{stackId}-{semver}` oder nur `{semver}` bei single-stack repos)
      4. Für jeden relevanten Tag: Manifest lesen → Layer-Digest für `stack.yaml` finden → Layer pullen → YAML parsen via `IRsgoManifestParser`
      5. Optional: `meta.json` Layer lesen für DisplayName, Category, Tags
      6. `ProductDefinition` erzeugen mit SourceId
    - Caching: Heruntergeladene Manifeste in `~/.rsgo/oci-cache/{sourceId}/{tag}/` speichern
    - ETag/Digest-basierter Skip: Nur neu pullen wenn sich der Manifest-Digest geändert hat
  - Pattern-Vorlage: `GitRepositoryProductSourceProvider` (Clone → Parse Delegation)
  - Abhängig von: Feature 1, Feature 2

- [ ] **Feature 4: Configuration — OciRegistrySourceEntry** – JSON-Persistierung
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Configuration/StackSourceConfig.cs` — neue `OciRegistrySourceEntry` Klasse + `[JsonDerivedType(typeof(OciRegistrySourceEntry), "oci-registry")]`
    - `src/ReadyStackGo.Infrastructure/Services/StackSources/DatabaseProductSourceService.cs` (oder `ProductSourceService.cs`) — Mapping StackSource ↔ OciRegistrySourceEntry
  - Pattern-Vorlage: `GitRepositorySourceEntry` (Zeile 40-46)
  - Abhängig von: Feature 1

- [ ] **Feature 5: Application — CreateStackSource Handler Extension** – OciRegistry Case
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/StackSources/CreateStackSource/CreateStackSourceCommand.cs` — neue Request-Properties: `RegistryUrl`, `Repository`, `RegistryUsername`, `RegistryPassword`, `TagPattern`
    - `src/ReadyStackGo.Application/UseCases/StackSources/CreateStackSource/CreateStackSourceHandler.cs` — neuer Case `"ociregistry"` / `"oci-registry"`
  - Validierung:
    - `RegistryUrl` required (z.B. `docker.io`, `ghcr.io`)
    - `Repository` required (z.B. `wiesenwischer/rsgo-stacks`)
    - `TagPattern` optional (Default: `*` = alle Tags)
    - URL-Format-Validierung (hostname, kein Schema nötig)
  - Pattern-Vorlage: `CreateStackSourceHandler` Git-Case (Zeile 57-81)
  - Abhängig von: Feature 1

- [ ] **Feature 6: API — Endpoint Support** – OciRegistry-Felder in Create/Update
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/Endpoints/StackSources/CreateSourceEndpoint.cs`
    - `src/ReadyStackGo.Api/Endpoints/StackSources/UpdateSourceEndpoint.cs` (falls vorhanden)
  - Abhängig von: Feature 5

- [ ] **Feature 7: WebUI — AddOciRegistrySource Page** – Formular für OCI Registry
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/StackSources/AddOciRegistrySource.tsx` (neu)
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/StackSources/AddStackSourceSelect.tsx` — neue Option "OCI Registry"
    - `src/ReadyStackGo.WebUi/packages/core/src/api/stackSources.ts` — Request-Typ erweitern
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useStackSourceStore.ts` — falls Anpassung nötig
    - Router-Datei — Route `/settings/stack-sources/add/oci-registry`
  - UI-Felder:
    - Source ID (auto-generated or manual)
    - Display Name
    - Registry Host (z.B. `docker.io`, `ghcr.io`) — Dropdown mit Presets + Custom
    - Repository (z.B. `wiesenwischer/rsgo-stacks`)
    - Tag Pattern (optional, Default: `*`)
    - Username / Password (optional, für private Registries)
    - "Test Connection" Button → ruft ListTags auf und zeigt Ergebnis
  - Pattern-Vorlage: `AddGitSource.tsx`
  - Abhängig von: Feature 5, Feature 6

- [ ] **Feature 8: Unit Tests Phase 1**
  - Tests für:
    - `StackSource.CreateOciRegistry()` — Validierung, Properties
    - `OciRegistryClient` — Tag-Listing, Manifest-Parsing, Token-Auth (mit Mock-HTTP)
    - `OciRegistryProductSourceProvider` — Tag-Parsing, Filter, ProductDefinition-Erzeugung
    - `CreateStackSourceHandler` — OciRegistry Case, Validierung
    - Edge Cases: Leere Tag-Liste, ungültige Tags, Auth-Fehler, Timeout, Docker Hub Sonderbehandlung
  - Abhängig von: Feature 1-7

### Phase 2 — OCI Stack Bundle Format + Import

- [ ] **Feature 9: OCI Stack Bundle Format Specification** – Definiert wie Bundles in der Registry aussehen
  - Bundle-Struktur (als OCI Image Layers):
    - Layer 1: `stack.yaml` (RSGO Manifest, Pflicht)
    - Layer 2: `lock.json` (Image-Digests, Pflicht)
    - Layer 3: `meta.json` (Marketplace-Metadaten, Optional)
  - Konvention: Media Types `application/vnd.rsgo.stack.manifest.v1+yaml`, `application/vnd.rsgo.stack.lock.v1+json`, `application/vnd.rsgo.stack.meta.v1+json`
  - Alternativ: Standard Docker Image mit Dateien unter `/rsgo/` + OCI Labels
  - Abhängig von: Phase 1

- [ ] **Feature 10: Lock File Model** – Domain-Modell für deterministische Deployments
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/StackManagement/Stacks/OciLockFile.cs` (neu) — Value Object
  - Struktur:
    ```
    OciLockFile { ApiVersion, StackName, StackVersion, Images[] }
    OciLockImage { Name, Image, Tag, Digest, Role? }
    ```
  - Parsing: JSON deserialization aus Layer-Inhalt
  - Abhängig von: Feature 9

- [ ] **Feature 11: Lock-file Based Deployment** – Image@Digest statt Image:Tag
  - Betroffene Dateien:
    - Deployment-Engine (`IDeploymentEngine` / `DeploymentEngine`) — Image-Ref-Resolution erweitern
    - `ProductDefinition` oder `StackDefinition` — optionale LockFile-Referenz
  - Logik:
    1. Wenn LockFile vorhanden → für jeden Service den Digest aus `lock.json` verwenden
    2. Image-Referenz wird `image@sha256:abc123` statt `image:1.0.0`
    3. Fallback auf Tag wenn kein Lock-Eintrag gefunden
  - Abhängig von: Feature 10

- [ ] **Feature 12: CI/CD Tooling & Documentation**
  - Dockerfile-Template für OCI Stack Bundle erstellen
  - Beispiel-Pipeline (GitHub Actions) dokumentieren
  - ORAS CLI Beispiele für Push/Pull
  - Abhängig von: Feature 9

- [ ] **Feature 13: Unit Tests Phase 2**
  - Tests für: Lock File Parsing, Digest-basiertes Deployment, Bundle-Extraktion
  - Edge Cases: Fehlende lock.json, unbekannte Digests, Format-Versionen
  - Abhängig von: Feature 9-12

- [ ] **Dokumentation & Website** – Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main, Spec-Datei aufräumen

## Test-Strategie

### Unit Tests
- **Domain**: `StackSource.CreateOciRegistry()` Validierung, `OciLockFile` Parsing
- **OciRegistryClient**: HTTP-Mocks für Docker v2 API (Tag-Listing, Manifest, Blob), Token-Flow, Docker Hub Redirect
- **OciRegistryProductSourceProvider**: Tag-Pattern-Matching, StackId/Version-Parsing aus Tags, Cache-Verhalten
- **CreateStackSourceHandler**: OciRegistry Validierung (fehlende URL, ungültiges Format)
- **Lock File Deployment**: Digest-Resolution, Fallback auf Tag

### Integration Tests
- **OciRegistryClient** gegen echte Registry (Docker Hub public repos, z.B. `library/alpine`)
- **Full Stack Source Flow**: Source erstellen → Sync → Products im Cache

### E2E Tests
- Stack Source hinzufügen (OCI Registry) → Sync → Stacks im Katalog sichtbar
- Deployment mit Lock File → Container verwendet Digest

## Offene Punkte

- [ ] Soll `TagPattern` Glob-Syntax (`ams-*`) oder Regex unterstützen?
- [ ] Soll Phase 2 custom OCI Media Types verwenden oder Standard Docker Images mit `/rsgo/`-Pfad?
- [ ] Soll der Registry Client Pagination für Tag-Listen unterstützen (>100 Tags)?
- [ ] Braucht es einen separaten "Credentials Test" Endpoint oder reicht der initiale Sync als Validierung?

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Registry API | Docker Registry v2, ORAS | Docker Registry v2 | Kompatibel mit Docker Hub, GHCR, Azure CR — breiteste Unterstützung, kein ORAS-Dependency nötig |
| Bundle-Format | Custom OCI Artifact, Docker Image mit /rsgo/ | - | Noch offen — Phase 2 |
| Tag-Parsing | `{stackId}-{version}`, nur `{version}` | Beides | Ein Repo für alle Stacks (Multi-Stack: `{stackId}-{version}`) + Pro Stack ein Repo (Single-Stack: nur `{version}`) |
| Auth-Speicherung | Plaintext in JSON, Encrypted | Encrypted | Analog zu GitPassword — verschlüsselt in DB/Config |
| Cache-Strategie | Kein Cache, File Cache mit ETag | File Cache | `~/.rsgo/oci-cache/` mit Manifest-Digest-Prüfung — vermeidet redundante Downloads |
