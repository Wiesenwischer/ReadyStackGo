<!-- GitHub Epic: #367 -->
# Phase: Product Updates & Release Notes (v0.63)

## Ziel

Sichtbar machen, wenn eine neuere Version eines installierten Produkts verfügbar ist, und die Release Notes direkt im UI zugänglich machen. Entscheidung "upgraden ja/nein" bekommt damit Kontext.

Heute bleibt ein auf v1.0.2 installiertes Produkt auch nach Sync einer v1.1.0 stumm — weder Badge, noch Notification, noch Release-Notes. Diese Phase schließt die Lücke.

## Analyse

### Bestehende Architektur

- **ProductDeployment-Aggregate** ([ProductDeployment.cs:32](../../src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs#L32)) hält `ProductVersion` (installiert) und `PreviousVersion`. Kein Feld für "verfügbare/neueste Version" — das kommt aus dem Katalog.
- **ProductDefinition** ([ProductDefinition.cs:53](../../src/ReadyStackGo.Domain/StackManagement/Stacks/ProductDefinition.cs#L53)) trägt pro Katalog-Eintrag genau eine `ProductVersion`. Sync-Zyklen überschreiben, es gibt keine Versions-Historie pro Produkt.
- **Source-Sync** ([SyncStackSourcesHandler.cs](../../src/ReadyStackGo.Application/UseCases/StackSources/SyncStackSources/SyncStackSourcesHandler.cs)) lädt `ProductDefinition`s. Eine Source kann heute mehrere Versionen desselben Produkts nebeneinander halten (anders als "immer nur eine" — das ist eine Einschränkung, die wir adressieren müssen).
- **RSGO-Self-Update-Muster** ([GetVersionHandler.cs:59-92](../../src/ReadyStackGo.Application/UseCases/System/GetVersion/GetVersionHandler.cs#L59-L92)) zeigt das exakte Pattern, das wir für Produkte spiegeln: `IVersionCheckService.GetLatestVersionAsync()` → `IsNewerVersion()` (SemVer) → `ExistsAsync(type, metadataKey, metadataValue)` gegen Duplikat → einmalige `Notification` mit `metadata["latestVersion"]`.
- **Notification-Factory** ([NotificationFactory.cs](../../src/ReadyStackGo.Application/Notifications/NotificationFactory.cs)) hat `CreateProductDeploymentResult` als nächstliegende Vorlage. Kein Helper für ProductUpdateAvailable — der kommt in dieser Phase dazu. Enum `NotificationType` ([Notification.cs](../../src/ReadyStackGo.Application/Notifications/Notification.cs)) wird um `ProductUpdateAvailable` erweitert.
- **ProductDeployment UI** ([ProductDeploymentDetail.tsx:128-130](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx#L128)) zeigt die installierte Version als Badge; der "View in Catalog"-Button verweist zurück auf den Katalog. An diese Stelle kommt der Update-Badge.

### Betroffene Bounded Contexts

- **Domain**
  - Neues Value Object `AvailableVersion` (Version + optional `ReleaseNotesUrl` + optional `ChangelogMarkdown`) oder Felder direkt an `ProductDefinition`.
  - Neuer Domain-Service/-Query `ProductUpdateAvailability` (vergleicht installiert vs. neueste im Katalog).
  - Neuer Notification-Type `ProductUpdateAvailable`.
- **Application**
  - Query `GetProductUpdateStatus(productDeploymentId)` → `{ currentVersion, latestVersion?, hasUpdate, releaseNotesUrl?, changelogMarkdown? }`.
  - Background-Check beim Source-Sync: nach erfolgreichem `SyncStackSources`/`SyncSingleSource` wird pro betroffenem `ProductDeployment` geprüft, ob es eine neuere Version gibt; falls ja → einmalige Notification.
  - `NotificationFactory.CreateProductUpdateAvailable(...)` mit Metadata-Key `productDeploymentId:latestVersion` für Dedup.
- **Infrastructure**
  - Stack-Source-Loader (Git, OCI) liest zusätzlich `releaseNotesUrl` aus dem YAML und sucht nach `CHANGELOG.md` neben der Stack-Definition; beides wird an der `ProductDefinition` gespeichert.
- **API**
  - `GET /api/product-deployments/{id}/update-status` → JSON wie oben.
  - `GET /api/product-deployments/{id}/release-notes?version=X.Y.Z` → entweder serverseitig gefetchte Markdown-Source (bei CHANGELOG.md) oder nur die URL (bei `releaseNotesUrl`) — frontend entscheidet dann zwischen Embed und externem Link.
- **WebUi (rsgo-generic)**
  - `ProductDeploymentDetail`: Update-Badge neben Versions-Badge; Klick öffnet Release-Notes-Viewer oder externen Link.
  - `ProductDeployments`/Dashboard-Liste: Indikator (Punkt/Badge) auf Deployments mit verfügbarem Update.
  - Neue Komponente `ReleaseNotesViewer` — rendert Markdown (reuse `@rsgo/core` Markdown-Util falls vorhanden, sonst `react-markdown` oder `marked` — Library-Entscheidung siehe "Offene Punkte").

### YAML-Schema-Änderung

Ergänzt in `ProductDefinition` YAML:
```yaml
productVersion: "1.1.0"
releaseNotesUrl: "https://github.com/org/product/releases/tag/v1.1.0"   # optional
# Konvention: liegt CHANGELOG.md im gleichen Verzeichnis → wird automatisch
# beim Sync eingelesen und bevorzugt vor releaseNotesUrl im Viewer angezeigt.
```

## AMS UI Counterpart

**Ja — AMS-Counterpart wird als eigenes PLAN file angelegt** (`C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-product-updates.md`). Shared Hooks (`useProductUpdateStatus`, `useReleaseNotes`) kommen in `@rsgo/core`; Pages/Komponenten werden im AMS-Distribution mit ConsistentUI/Lit reimplementiert.

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: YAML-Schema + Source-Loader**
  - `ProductDefinition` bekommt `ReleaseNotesUrl?` und `ChangelogMarkdown?`.
  - Git-Source-Loader und OCI-Source-Loader lesen beides beim Sync.
  - Betroffene Dateien: `src/ReadyStackGo.Domain/StackManagement/Stacks/ProductDefinition.cs`, alle Source-Loader unter `src/ReadyStackGo.Infrastructure/StackSources/`.
  - Pattern-Vorlage: bestehender `ProductDefinition`-Parser.
  - Abhängig von: —
- [ ] **Feature 2: Update-Status-Query**
  - Query `GetProductUpdateStatus` + Handler; vergleicht installiert vs. neueste verfügbare Version via SemVer (reuse `IsNewerVersion` aus [VersionCheckService.cs:94](../../src/ReadyStackGo.Infrastructure/Services/VersionCheckService.cs#L94) — extrahieren oder wiederverwenden).
  - API-Endpoint `GET /api/product-deployments/{id}/update-status`.
  - Betroffene Dateien: `src/ReadyStackGo.Application/UseCases/ProductDeployments/GetUpdateStatus/...`, `src/ReadyStackGo.Api/Endpoints/ProductDeployments/GetUpdateStatusEndpoint.cs`.
  - Abhängig von: Feature 1.
- [ ] **Feature 3: Release-Notes-Endpoint**
  - `GET /api/product-deployments/{id}/release-notes?version=X.Y.Z` liefert `{ mode: "markdown" | "url", content: "...", url?: "..." }`.
  - Betroffene Dateien: `src/ReadyStackGo.Api/Endpoints/ProductDeployments/GetReleaseNotesEndpoint.cs`, Query-Handler.
  - Abhängig von: Feature 1.
- [ ] **Feature 4: Update-Notification + Dedup**
  - Neuer `NotificationType.ProductUpdateAvailable` + `NotificationFactory.CreateProductUpdateAvailable`.
  - Hook am Ende von `SyncStackSourcesHandler`/`SyncSingleSourceEndpoint`: iteriert aktive `ProductDeployment`s, prüft `GetProductUpdateStatus`, ruft bei neuer Version `AddAsync` (mit `ExistsAsync`-Dedup auf `{productDeploymentId}:{latestVersion}`).
  - Betroffene Dateien: `src/ReadyStackGo.Application/Notifications/Notification.cs`, `NotificationFactory.cs`, Sync-Handler.
  - Pattern-Vorlage: [GetVersionHandler.cs:59-92](../../src/ReadyStackGo.Application/UseCases/System/GetVersion/GetVersionHandler.cs#L59).
  - Abhängig von: Feature 2.
- [ ] **Feature 5: UI — `@rsgo/core` Hooks + API-Client**
  - `notificationsApi`-Parallele: `productUpdatesApi.getStatus(id)`, `productUpdatesApi.getReleaseNotes(id, version)`.
  - Hooks: `useProductUpdateStatus(id)`, `useReleaseNotes(id, version)`.
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/core/src/api/productUpdates.ts`, `hooks/useProductUpdateStatus.ts`, `hooks/useReleaseNotes.ts`.
  - Abhängig von: Features 2–3.
- [ ] **Feature 6: UI — Badge + Viewer + Dashboard**
  - Update-Badge neben Versions-Badge in `ProductDeploymentDetail.tsx`.
  - Neue Komponente `ReleaseNotesViewer` (Modal oder Seiten-Sektion).
  - Dashboard/Overview: Indikator auf Zeilen mit `hasUpdate: true`.
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx`, `src/ReadyStackGo.WebUi/packages/ui-generic/src/components/ReleaseNotesViewer.tsx`, Dashboard-Liste.
  - Abhängig von: Feature 5.
- [ ] **Feature 7: AMS UI Counterpart** — separates PLAN file im AMS Repo, parallel zu Features 5-6 mit ConsistentUI/Lit-Komponenten.
- [ ] **Dokumentation & Website** — Wiki-Seite "Product Updates", Public-Website-Update (DE/EN), Roadmap-Eintrag, Beispiel-YAML mit `releaseNotesUrl` in `docs/Reference/`.
- [ ] **Phase abschließen** — Alle Tests grün, v0.63-Release-Notes, PR gegen main.

## Test-Strategie

- **Unit Tests**
  - `ProductDefinitionParser`: parst `releaseNotesUrl`, findet `CHANGELOG.md` beim Sync; kein Wert → null-Felder.
  - SemVer-Compare: v1.0.2 < v1.1.0, v1.10.0 > v1.9.9, Prerelease (v1.1.0-rc1) vs. Stable, identische Version → kein Update.
  - `NotificationFactory.CreateProductUpdateAvailable`: Severity, Title/Message, Metadata-Keys.
  - Dedup: `ExistsAsync({productDeploymentId}:{latestVersion})` unterdrückt Zweit-Sync.
- **Integration Tests**
  - `/api/product-deployments/{id}/update-status`: Response-Shape, 404 bei unbekannter ID, leerer Katalog → `hasUpdate: false`.
  - `/api/product-deployments/{id}/release-notes?version=X`: Markdown-Mode bei CHANGELOG.md vorhanden, URL-Mode bei nur `releaseNotesUrl`, 404 sonst.
- **E2E Tests** (Playwright)
  - Zwei Produkte im Katalog (v1.0.0 + v1.1.0), ein Deployment auf v1.0.0 → nach Sync: Update-Badge auf Detail-Seite, Dashboard-Indicator, ein Notification-Eintrag.
  - Klick auf Badge öffnet `ReleaseNotesViewer` mit gerendertem Markdown.
  - Zweiter Sync → keine zweite Notification (Dedup verifiziert).

## Offene Punkte

- [ ] **Markdown-Library**: `react-markdown` (sicher, modular) vs. `marked` (schlank, weniger Deps). Entscheidung beim ersten Implementierungs-PR.
- [ ] **Release-Notes-Aggregation**: Wenn zwischen installiert (v1.0.2) und neuester (v1.3.0) mehrere Versionen liegen — alle Changelogs anzeigen oder nur die der Ziel-Version? Empfehlung: nur Ziel-Version; Aggregation als Folge-Feature.
- [ ] **Multi-Version im Katalog**: Heute hält `ProductDefinition` eine Version. Muss der Sync mehrere Versions-Einträge pro Produkt speichern? Klärung beim Refinement von Feature 1.
- [ ] **Sicherheit Release-Notes-Viewer**: Externe URLs sollen nicht serverseitig gefetcht werden (SSRF-Risiko). CHANGELOG.md aus eigenen Sources ist ok; externe URLs werden nur als Link gerendert, nicht im Viewer embedded.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Milestone | v0.60 / v1.0 / v0.63 | **v0.63** | v0.60 bereits als "Complete Health Check Support" vergeben und geschlossen; v0.61/v0.62 ebenso. v0.63 ist der nächste freie. |
| Release-Notes-Quelle | URL / Markdown / Git-Tag / CHANGELOG.md | **URL + CHANGELOG.md** | Vom User gewählt. Source-agnostisch, einfach, CHANGELOG.md als etablierte Konvention. |
| UI-Scope | Badge / Badge+Notif / Komplett | **Komplett** | Vom User gewählt: Badge + Notification + Dashboard + Release-Notes-Viewer. |
| Update-Scope | Strikt SemVer-newer / Alle ≠ installiert | **Strikt newer** | Vom User gewählt. Konsistent zum RSGO-self-update, kein Downgrade-Noise. |
| AMS-Counterpart | Ja / Deferred / Nein / Teilweise | **Ja (eigener PLAN)** | UI-Komponenten betroffen; separates PLAN im AMS-Repo, shared Hooks in `@rsgo/core`. |
