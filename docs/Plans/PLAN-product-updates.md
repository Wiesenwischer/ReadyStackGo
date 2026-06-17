<!-- GitHub Epic: #367 -->
# Phase: Product Updates & Release Notes

> **Plan-Refresh 2026-06-17 (gegen Code-Stand v0.73):** Datei-/Zeilenreferenzen und Annahmen
> aktualisiert. Wichtigste Änderung: Die **Update-Erkennung existiert bereits** als
> `CheckProductUpgrade` (Query + API + Frontend-Hook) und wird wiederverwendet — Feature 2
> entfällt damit weitgehend. Der echte Rest-Scope ist **Release-Notes-Daten + -Anzeige** und
> die **Sync-Notification**. Milestone v0.63 ist überholt (aktuell v0.73) → nächsten freien
> Milestone wählen (z. B. v0.74).

## Ziel

Sichtbar machen, wenn eine neuere Version eines installierten Produkts verfügbar ist, und die Release Notes direkt im UI zugänglich machen. Entscheidung "upgraden ja/nein" bekommt damit Kontext.

Heute bleibt ein auf v1.0.2 installiertes Produkt auch nach Sync einer v1.1.0 stumm — weder Badge, noch Notification, noch Release-Notes. Diese Phase schließt die Lücke.

## Analyse

### Bestehende Architektur (Stand v0.73)

- **ProductDeployment-Aggregate** ([ProductDeployment.cs:33](../../src/ReadyStackGo.Domain/Deployment/ProductDeployments/ProductDeployment.cs#L33)) hält `ProductVersion` (installiert, Z. 33) und `PreviousVersion` (Z. 67). Kein Feld für "verfügbare/neueste Version" — das kommt aus dem Katalog.
- **ProductDefinition** ([ProductDefinition.cs:53](../../src/ReadyStackGo.Domain/StackManagement/Stacks/ProductDefinition.cs#L53)) trägt eine `ProductVersion` (Z. 53). **Mehrere Versionen koexistieren bereits**: `Id` = `SourceId:Name:ProductVersion` (Z. 18-20) und der Katalog-Cache gruppiert pro `GroupId` und wählt die neueste via SemVer. Der frühere offene Punkt "nur eine Version pro Produkt" ist damit **gelöst**.
- **Update-Erkennung existiert bereits**: [`CheckProductUpgradeHandler`](../../src/ReadyStackGo.Application/UseCases/Deployments/CheckProductUpgrade/CheckProductUpgradeHandler.cs) nutzt [`IProductSourceService.GetAvailableUpgradesAsync(groupId, currentVersion)`](../../src/ReadyStackGo.Application/Services/IProductSourceService.cs#L68) und liefert `UpgradeAvailable`, `CurrentVersion`, `LatestVersion`, `LatestProductId`, `AvailableVersions[]`, `NewStacks/RemovedStacks`. Exponiert über `GET /api/environments/{envId}/product-deployments/{id}/upgrade/check` und im Frontend via `checkProductUpgrade()` / `CheckProductUpgradeResponse` ([deployments.ts:598](../../src/ReadyStackGo.WebUi/packages/core/src/api/deployments.ts#L598)). → **Diese API wird für den Update-Status wiederverwendet**, statt eine neue `update-status`-Query zu bauen.
- **SemVer-Vergleich**: kein `VersionCheckService.IsNewerVersion` mehr. Die Logik liegt in `InMemoryProductCache.SemVerComparer` (private) und wird von `GetAvailableUpgradesAsync` genutzt. Für eigene Vergleiche diese Quelle wiederverwenden/extrahieren.
- **RSGO-Self-Update-Muster** ([GetVersionHandler.cs](../../src/ReadyStackGo.Application/UseCases/System/GetVersion/GetVersionHandler.cs)) als Notification-Vorlage: `GetLatestVersionAsync()` → `UpdateAvailable` → `ExistsAsync(NotificationType.UpdateAvailable, "latestVersion", latestVersion)` gegen Duplikat → einmalige `Notification` mit `metadata["latestVersion"]` (Z. 30, 46, 65-72).
- **Notification** ([Notification.cs:20](../../src/ReadyStackGo.Application/Notifications/Notification.cs#L20)): Enum `NotificationType` hat `UpdateAvailable` (Z. 22) und `ProductDeploymentResult` (Z. 25); **kein** `ProductUpdateAvailable` — wird ergänzt. [`NotificationFactory.cs`](../../src/ReadyStackGo.Application/Notifications/NotificationFactory.cs) hat `CreateProductDeploymentResult` (Z. 66) als nächstliegende Vorlage; ein `CreateProductUpdateAvailable`-Helper kommt dazu.
- **Source-Loader** liegen unter [`src/ReadyStackGo.Infrastructure/Services/StackSources/`](../../src/ReadyStackGo.Infrastructure/Services/StackSources/) (`GitRepositoryProductSourceProvider`, `OciRegistryProductSourceProvider`, `LocalDirectoryProductSourceProvider`, `ProductSourceService`, `DatabaseProductSourceService`) — **nicht** unter `Infrastructure/StackSources/`.
- **ProductDeployment UI** ([ProductDeploymentDetail.tsx](../../src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx)) zeigt die installierte Version als Badge; der "View in Catalog"-Button verweist auf den Katalog. Ein Update-Badge ist hier noch **nicht** vorhanden (der Upgrade-Check wird bisher nur im Upgrade-Flow genutzt) → kommt hier dazu, gespeist aus `checkProductUpgrade()`.

### Betroffene Bounded Contexts

- **Domain**
  - Neues Value Object `AvailableVersion` (Version + optional `ReleaseNotesUrl` + optional `ChangelogMarkdown`) oder Felder direkt an `ProductDefinition`.
  - Neuer Domain-Service/-Query `ProductUpdateAvailability` (vergleicht installiert vs. neueste im Katalog).
  - Neuer Notification-Type `ProductUpdateAvailable`.
- **Application**
  - **Kein neuer Update-Status-Query** — `CheckProductUpgrade` liefert das bereits. Stattdessen dessen `CheckProductUpgradeResponse` (und `AvailableProductVersion`) um `releaseNotesUrl?` / hat-Changelog-Flag erweitern, damit das UI Badge **und** Release-Notes-Zugang aus einem Call bekommt.
  - Background-Check beim Source-Sync: nach erfolgreichem `SyncStackSources`/`SyncSingleSource` pro betroffenem `ProductDeployment` `GetAvailableUpgradesAsync` aufrufen; bei neuerer Version → einmalige Notification.
  - `NotificationFactory.CreateProductUpdateAvailable(...)` mit Metadata-Key `{productDeploymentId}:{latestVersion}` für Dedup (Vorlage: `CreateProductDeploymentResult`).
- **Infrastructure**
  - Source-Loader unter `Infrastructure/Services/StackSources/` (Git/OCI/LocalDirectory) lesen zusätzlich `releaseNotesUrl` aus dem YAML und suchen nach `CHANGELOG.md` neben der Stack-/Produkt-Definition; beides wird an der `ProductDefinition` gespeichert.
- **API**
  - **Update-Status: vorhandenes** `GET /api/environments/{envId}/product-deployments/{id}/upgrade/check` wiederverwenden (um Release-Notes-Felder erweitert).
  - **Neu:** `GET /api/environments/{envId}/product-deployments/{id}/release-notes?version=X.Y.Z` → `{ mode: "markdown" | "url", content?, url? }` (CHANGELOG.md serverseitig aus eigener Source vs. externe URL nur als Link, siehe SSRF unter Offene Punkte).
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
  - `ProductDefinition` bekommt `ReleaseNotesUrl?` und `ChangelogMarkdown?` (neue optionale Konstruktor-Parameter, Konstruktor Z. 119-158 + Properties).
  - Git-, OCI- und LocalDirectory-Source-Loader lesen beides beim Sync.
  - Betroffene Dateien: `src/ReadyStackGo.Domain/StackManagement/Stacks/ProductDefinition.cs`, Source-Loader unter `src/ReadyStackGo.Infrastructure/Services/StackSources/`, Manifest-Parsing in `src/ReadyStackGo.Infrastructure/Parsing/`.
  - Pattern-Vorlage: bestehendes `ProductDefinition`-Parsing.
  - Abhängig von: —
- [ ] **Feature 2: Update-Status — vorhandenes `CheckProductUpgrade` erweitern (kein neuer Query)**
  - `CheckProductUpgradeResponse` + `AvailableProductVersion` um `releaseNotesUrl?` / `hasChangelog` erweitern; Handler füllt sie aus der `ProductDefinition` der Zielversion.
  - SemVer-Vergleich nicht neu bauen — `GetAvailableUpgradesAsync` / `InMemoryProductCache.SemVerComparer` liefern die Reihenfolge.
  - Betroffene Dateien: `src/ReadyStackGo.Application/UseCases/Deployments/CheckProductUpgrade/CheckProductUpgradeHandler.cs` + `CheckProductUpgradeQuery.cs`, Endpoint unter `src/ReadyStackGo.Api/Endpoints/Deployments/`.
  - Abhängig von: Feature 1.
- [ ] **Feature 3: Release-Notes-Endpoint**
  - `GET /api/environments/{envId}/product-deployments/{id}/release-notes?version=X.Y.Z` liefert `{ mode: "markdown" | "url", content?, url? }`.
  - Betroffene Dateien: neues Endpoint + Query-Handler unter `src/ReadyStackGo.Api/Endpoints/Deployments/` bzw. `src/ReadyStackGo.Application/UseCases/Deployments/`.
  - Abhängig von: Feature 1.
- [ ] **Feature 4: Update-Notification + Dedup**
  - Neuer `NotificationType.ProductUpdateAvailable` + `NotificationFactory.CreateProductUpdateAvailable`.
  - Hook am Ende von `SyncStackSourcesHandler`/`SyncSingleSourceEndpoint`: iteriert aktive `ProductDeployment`s, prüft via `GetAvailableUpgradesAsync(groupId, currentVersion)`, ruft bei neuer Version `AddAsync` (mit `ExistsAsync`-Dedup auf `{productDeploymentId}:{latestVersion}`).
  - Betroffene Dateien: `src/ReadyStackGo.Application/Notifications/Notification.cs`, `NotificationFactory.cs`, Sync-Handler.
  - Pattern-Vorlage: [GetVersionHandler.cs](../../src/ReadyStackGo.Application/UseCases/System/GetVersion/GetVersionHandler.cs) (UpdateAvailable-Dedup).
  - Abhängig von: Feature 2.
- [ ] **Feature 5: UI — `@rsgo/core` Hook + API-Client (Release Notes)**
  - Update-Status ist im Frontend bereits über `checkProductUpgrade()` ([deployments.ts:598](../../src/ReadyStackGo.WebUi/packages/core/src/api/deployments.ts#L598)) verfügbar — wiederverwenden (ggf. um die neuen Release-Notes-Felder erweitern).
  - **Neu:** `getProductReleaseNotes(envId, id, version)` + Hook `useReleaseNotes(id, version)`.
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/core/src/api/deployments.ts`, `hooks/useReleaseNotes.ts`.
  - Abhängig von: Features 2–3.
- [ ] **Feature 6: UI — Badge + Viewer + Dashboard**
  - Update-Badge neben Versions-Badge in `ProductDeploymentDetail.tsx`, gespeist aus `checkProductUpgrade()` (`upgradeAvailable`/`latestVersion`).
  - Neue Komponente `ReleaseNotesViewer` (Modal oder Seiten-Sektion).
  - Dashboard/Overview: Indikator auf Zeilen mit verfügbarem Update.
  - Betroffene Dateien: `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx`, `src/ReadyStackGo.WebUi/packages/ui-generic/src/components/ReleaseNotesViewer.tsx`, Dashboard-Liste.
  - Abhängig von: Feature 5.
- [ ] **Feature 7: AMS UI Counterpart** — separates PLAN file im AMS Repo, parallel zu Features 5-6 mit ConsistentUI/Lit-Komponenten.
- [ ] **Dokumentation & Website** — Wiki-Seite "Product Updates", Public-Website-Update (DE/EN), Roadmap-Eintrag, Beispiel-YAML mit `releaseNotesUrl` in `docs/Reference/`.
- [ ] **Phase abschließen** — Alle Tests grün, Release-Notes für den gewählten Milestone (nächster freier, z. B. v0.74), PR gegen main.

## Test-Strategie

- **Unit Tests**
  - `ProductDefinitionParser`: parst `releaseNotesUrl`, findet `CHANGELOG.md` beim Sync; kein Wert → null-Felder.
  - SemVer-Compare: v1.0.2 < v1.1.0, v1.10.0 > v1.9.9, Prerelease (v1.1.0-rc1) vs. Stable, identische Version → kein Update.
  - `NotificationFactory.CreateProductUpdateAvailable`: Severity, Title/Message, Metadata-Keys.
  - Dedup: `ExistsAsync({productDeploymentId}:{latestVersion})` unterdrückt Zweit-Sync.
- **Integration Tests**
  - `…/product-deployments/{id}/upgrade/check`: Release-Notes-Felder in der Response; bestehende Fälle (404, leerer Katalog → `upgradeAvailable: false`) bleiben grün.
  - `…/product-deployments/{id}/release-notes?version=X`: Markdown-Mode bei CHANGELOG.md vorhanden, URL-Mode bei nur `releaseNotesUrl`, 404 sonst.
- **E2E Tests** (Playwright)
  - Zwei Produkte im Katalog (v1.0.0 + v1.1.0), ein Deployment auf v1.0.0 → nach Sync: Update-Badge auf Detail-Seite, Dashboard-Indicator, ein Notification-Eintrag.
  - Klick auf Badge öffnet `ReleaseNotesViewer` mit gerendertem Markdown.
  - Zweiter Sync → keine zweite Notification (Dedup verifiziert).

## Offene Punkte

- [ ] **Markdown-Library**: `react-markdown` (sicher, modular) vs. `marked` (schlank, weniger Deps). Entscheidung beim ersten Implementierungs-PR.
- [ ] **Release-Notes-Aggregation**: Wenn zwischen installiert (v1.0.2) und neuester (v1.3.0) mehrere Versionen liegen — alle Changelogs anzeigen oder nur die der Ziel-Version? Empfehlung: nur Ziel-Version; Aggregation als Folge-Feature.
- [x] **Multi-Version im Katalog** (geklärt, v0.73): Der Katalog hält bereits mehrere Versionen pro Produkt (`ProductDefinition.Id = SourceId:Name:ProductVersion`; Cache gruppiert per `GroupId`, wählt neueste via `SemVerComparer`). `GetAvailableUpgradesAsync` liefert die verfügbaren Versionen — keine zusätzliche Persistenz nötig.
- [ ] **Sicherheit Release-Notes-Viewer**: Externe URLs sollen nicht serverseitig gefetcht werden (SSRF-Risiko). CHANGELOG.md aus eigenen Sources ist ok; externe URLs werden nur als Link gerendert, nicht im Viewer embedded.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Milestone | ~~v0.63~~ → neu wählen | **nächster freier (z. B. v0.74)** | v0.63 war zur ursprünglichen Planung der nächste freie; inzwischen ist v0.73 released. Beim Aufgreifen den nächsten freien Milestone setzen. |
| Release-Notes-Quelle | URL / Markdown / Git-Tag / CHANGELOG.md | **URL + CHANGELOG.md** | Vom User gewählt. Source-agnostisch, einfach, CHANGELOG.md als etablierte Konvention. |
| UI-Scope | Badge / Badge+Notif / Komplett | **Komplett** | Vom User gewählt: Badge + Notification + Dashboard + Release-Notes-Viewer. |
| Update-Scope | Strikt SemVer-newer / Alle ≠ installiert | **Strikt newer** | Vom User gewählt. Konsistent zum RSGO-self-update, kein Downgrade-Noise. |
| AMS-Counterpart | Ja / Deferred / Nein / Teilweise | **Ja (eigener PLAN)** | UI-Komponenten betroffen; separates PLAN im AMS-Repo, shared Hooks in `@rsgo/core`. |
