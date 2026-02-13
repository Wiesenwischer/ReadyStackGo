# Phase: Preconfigured Stack Sources & Marketplace (v0.24)

## Ziel

RSGO soll nicht mehr nur mit einem leeren lokalen Verzeichnis starten, sondern ab dem ersten Start eine reichhaltige Auswahl an Stack-Quellen bieten. Nutzer können aus einem kuratierten Katalog bekannter Git-Repos wählen, vorkonfigurierte Standard-Quellen nutzen und ihre Source-Konfiguration exportieren/importieren. Ein Marketplace in der UI macht das Entdecken neuer Stack-Quellen einfach.

## Analyse

### Bestehende Architektur

**Stack Sources (Domain):**
- `StackSource` Aggregate Root mit `StackSourceType` (LocalDirectory, GitRepository)
- CRUD via MediatR Commands/Queries (`CreateStackSourceCommand`, etc.)
- Persistenz via EF Core SQLite (`StackSourceRepository`)

**Initialization:**
- `DatabaseProductSourceService.EnsureInitializedAsync()` erstellt Default-Source wenn DB leer
- Default: Lokale Source `stacks/` mit FilePattern `*.yml;*.yaml`
- Auto-Sync beim ersten Laden

**Provider-Pattern:**
- `IProductSourceProvider` Interface mit `CanHandle()` und `LoadProductsAsync()`
- Implementierungen: `LocalDirectoryProductSourceProvider`, `GitRepositoryProductSourceProvider`
- Git: Shallow-Clone, Branch-Support, Auth-Support, SSL-Config

**API:**
- CRUD Endpoints unter `/api/stack-sources`
- Sync Endpoints (`/api/stack-sources/sync`, `/api/stack-sources/sync/{id}`)

**WebUI:**
- Settings → Stack Sources mit Liste, Add (Local/Git), Toggle, Sync, Delete

### Betroffene Bounded Contexts

- **Domain**: Neues `SourceRegistry` Konzept (Curated Catalog Einträge)
- **Application**: Neue Commands/Queries für Registry, Import/Export
- **Infrastructure**: Registry-Fetching (GitHub/embedded), Import/Export Service
- **API**: Neue Endpoints für Registry-Browse, Import/Export
- **WebUI**: Marketplace-Seite, Import/Export-UI, erweiterte Add-Source-Flow

## Features / Schritte

### Feature 1: Source Registry (Curated Catalog Backend)

- [ ] **Feature 1: Source Registry Model & Data** – Kuratierter Katalog von bekannten Stack-Quellen

  **Konzept:** Eine JSON-Datei (`source-registry.json`) wird im Docker-Image mitgeliefert und enthält eine kuratierte Liste von Stack-Quellen (Git-Repos) mit Metadaten. Optional kann eine neuere Version von GitHub geladen werden.

  **Registry-Eintrag Format:**
  ```json
  {
    "id": "rsgo-community-stacks",
    "name": "RSGO Community Stacks",
    "description": "Official community-maintained stack collection",
    "type": "git-repository",
    "gitUrl": "https://github.com/Wiesenwischer/rsgo-community-stacks.git",
    "gitBranch": "main",
    "category": "official",
    "tags": ["community", "official", "curated"],
    "icon": "star",
    "featured": true,
    "stackCount": 15
  }
  ```

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.Application/Services/ISourceRegistryService.cs`
  - Neu: `src/ReadyStackGo.Infrastructure/Services/StackSources/SourceRegistryService.cs`
  - Neu: `src/ReadyStackGo.Infrastructure/Services/StackSources/source-registry.json` (embedded resource)
  - Pattern-Vorlage: `IVersionCheckService` (GitHub API fetching pattern)
  - Abhängig von: -

---

### Feature 2: Bundled Default Sources

- [ ] **Feature 2: Bundled Default Sources** – Mehrere Sources beim ersten Start vorkonfigurieren

  **Konzept:** Beim ersten Start (leere DB) werden nicht nur die lokale `stacks/` Source, sondern auch ausgewählte Registry-Einträge als Sources angelegt. Der User wird im Wizard oder nach dem Login gefragt, welche er aktivieren möchte.

  **Änderungen:**
  - `DatabaseProductSourceService.EnsureInitializedAsync()` erweitern: Neben lokaler Source auch featured Registry-Einträge als Git-Sources anlegen (standardmäßig disabled, User aktiviert nach Wahl)
  - Alternativ: Wizard-Step 4 (Install) erweitern mit optionaler Source-Auswahl

  **Betroffene Dateien:**
  - Edit: `src/ReadyStackGo.Infrastructure/Services/StackSources/DatabaseProductSourceService.cs`
  - Ggf. Edit: `src/ReadyStackGo.WebUi/src/pages/Wizard/index.tsx` (optionaler neuer Step)
  - Pattern-Vorlage: Bestehende `EnsureInitializedAsync()` Logik
  - Abhängig von: Feature 1

---

### Feature 3: Marketplace UI

- [ ] **Feature 3: Marketplace UI** – Browse und One-Click-Add von Registry-Einträgen

  **Konzept:** Neue Seite/Section unter Settings → Stack Sources oder als eigene Seite "Marketplace" / "Browse Sources". Zeigt die Registry-Einträge als Karten an. Bereits hinzugefügte Sources werden markiert. One-Click "Add" erstellt die Source und startet den Sync.

  **UI-Design:**
  - Karten-Layout (ähnlich Stack Catalog) mit Name, Description, Category, Tags
  - Filter nach Category/Tags
  - Search
  - "Add" Button → erstellt Git-Source mit vorkonfigurierten Werten
  - Badge "Installed" wenn Source bereits existiert (Matching über `gitUrl`)
  - Badge "Featured" / "Official" für kuratierte Quellen

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/SourceMarketplace.tsx`
  - Neu: `src/ReadyStackGo.WebUi/src/api/sourceRegistry.ts`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/ListRegistryEndpoint.cs`
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/ListRegistrySources/`
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/StackSourcesList.tsx` (Link zum Marketplace)
  - Edit: `src/ReadyStackGo.WebUi/src/App.tsx` (neue Route)
  - Pattern-Vorlage: `StackCatalog.tsx` (Karten-Layout), `StackSourcesList.tsx`
  - Abhängig von: Feature 1

---

### Feature 4: Import/Export

- [ ] **Feature 4: Import/Export** – Source-Konfigurationen exportieren und importieren

  **Konzept:** Nutzer können ihre Stack-Source-Konfiguration als JSON exportieren (Download) und JSON-Dateien importieren (Upload → Sources werden angelegt). Nützlich für Team-Sharing und Backup.

  **Export-Format:**
  ```json
  {
    "version": "1.0",
    "exportedAt": "2026-02-13T...",
    "sources": [
      {
        "name": "My Stacks",
        "type": "git-repository",
        "gitUrl": "https://...",
        "gitBranch": "main",
        "enabled": true
      }
    ]
  }
  ```

  **Import-Verhalten:**
  - Sources mit gleicher `gitUrl` werden übersprungen (kein Duplikat)
  - Lokale Sources werden importiert wenn Pfad existiert
  - Credentials (gitPassword) werden NICHT exportiert (Sicherheit)
  - Import-Preview zeigt was hinzugefügt wird

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/ExportSources/`
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/ImportSources/`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/ExportSourcesEndpoint.cs`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/ImportSourcesEndpoint.cs`
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/StackSourcesList.tsx` (Export/Import Buttons)
  - Neu: `src/ReadyStackGo.WebUi/src/components/settings/ImportSourcesModal.tsx`
  - Pattern-Vorlage: Bestehende CRUD Commands, `TlsConfigService` (File-Upload Pattern)
  - Abhängig von: -

---

### Feature 5: Quick-Add von Registry-Sources

- [ ] **Feature 5: Quick-Add Flow** – Vereinfachter Add-Source-Flow für Registry-Einträge

  **Konzept:** Wenn ein User eine Source aus dem Marketplace/Registry hinzufügt, wird der komplette Add-Source-Flow übersprungen — die Felder sind bereits vorausgefüllt. Der User muss nur noch bestätigen (und ggf. Credentials für private Repos eintragen).

  **Änderungen:**
  - `AddGitSource.tsx` erweitern: Query-Parameter `?registry=<id>` → Felder vorausfüllen
  - Alternativ: Direkte API-Call ohne Formular (Marketplace → "Add" → Source angelegt → Sync gestartet)

  **Betroffene Dateien:**
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/AddGitSource.tsx`
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/AddFromRegistry/`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/AddFromRegistryEndpoint.cs`
  - Pattern-Vorlage: `CreateStackSourceCommand`
  - Abhängig von: Feature 1, Feature 3

---

- [ ] **Dokumentation & Website** – Wiki, Public Website (DE/EN), Roadmap aktualisieren
  - Docs: Stack Sources Marketplace Anleitung
  - Public Website: Feature Highlight

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests:**
  - SourceRegistryService: Registry laden (embedded + remote), Fehlerhandling
  - Import/Export: Serialisierung, Duplikaterkennung, Credential-Filterung
  - AddFromRegistry: Validierung, Source-Erstellung
- **Integration Tests:**
  - Registry-Endpoint: Korrekte Antwort, Filterung
  - Import-Endpoint: Upload, Duplikat-Handling, Fehler bei ungültigem Format
  - Export-Endpoint: Download, Credential-Filterung
- **E2E Tests:**
  - Marketplace: Browse, Filter, Add Source, "Installed" Badge
  - Import/Export: Export → Download, Upload → Preview → Import
  - Quick-Add: Registry-Source mit One-Click hinzufügen

## Offene Punkte

- [ ] Soll die Registry-Datei nur lokal (embedded) sein oder auch remote von GitHub geladen werden (wie Version-Check)?
- [ ] Soll der Wizard einen neuen Step für Source-Auswahl bekommen oder wird das nur über die Settings/Marketplace gemacht?
- [ ] Braucht es ein eigenes Community-Stacks-Repo auf GitHub (z.B. `Wiesenwischer/rsgo-community-stacks`) als erste kuratierte Quelle?
- [ ] Soll die Import-Funktion auch Credentials unterstützen (verschlüsselt) oder bewusst ohne Credentials?

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Registry-Quelle | A) Nur embedded JSON, B) Embedded + Remote-Update, C) Nur Remote | - | Noch offen |
| Source-Duplikat-Erkennung | A) Über gitUrl, B) Über Name, C) Über beides | - | Noch offen |
| Marketplace-Platzierung | A) Eigene Seite, B) Tab in Stack Sources, C) Integriert in Add-Source-Flow | - | Noch offen |
| Import-Credentials | A) Ohne Credentials, B) Optional verschlüsselt | - | Noch offen |
