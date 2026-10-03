# Phase: Preconfigured Stack Sources (v0.24)

## Ziel

RSGO soll nicht mehr nur mit einem leeren lokalen Verzeichnis starten, sondern ab dem ersten Start eine kuratierte Auswahl an Stack-Quellen bieten. Im Wizard kann der User aus einem eingebetteten Katalog Quellen auswählen. Über den erweiterten Add-Source-Flow können jederzeit weitere Quellen aus dem Katalog hinzugefügt werden. Import/Export ermöglicht Team-Sharing.

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
- Wizard: 4 Steps (Admin, Organization, Environment, Install)

### Betroffene Bounded Contexts

- **Application**: Neuer `ISourceRegistryService`, Import/Export Commands, AddFromRegistry Command
- **Infrastructure**: Registry-Service (embedded JSON), Import/Export Service
- **API**: Neue Endpoints für Registry-Listing, Import/Export, AddFromRegistry
- **WebUI**: Wizard-Step 4 erweitern, Add-Source-Flow mit "From Catalog", Catalog Empty State

## Features / Schritte

### Feature 1: Source Registry (Curated Catalog Backend)

- [x] **Feature 1: Source Registry Model & Data** – Kuratierter Katalog von bekannten Stack-Quellen

  **Konzept:** Eine JSON-Datei (`source-registry.json`) wird im Docker-Image eingebettet und enthält eine kuratierte Liste von Stack-Quellen (öffentliche Git-Repos) mit Metadaten. Kein Remote-Update — neue Einträge kommen über neue RSGO-Versionen.

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
    "featured": true,
    "stackCount": 15
  }
  ```

  **Hinweis:** Keine Credentials — Registry enthält nur öffentliche Repos.

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.Application/Services/ISourceRegistryService.cs`
  - Neu: `src/ReadyStackGo.Infrastructure/Services/StackSources/SourceRegistryService.cs`
  - Neu: `src/ReadyStackGo.Infrastructure/Services/StackSources/source-registry.json` (embedded resource)
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/ListRegistryEndpoint.cs`
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/ListRegistrySources/`
  - Pattern-Vorlage: `IVersionCheckService` (Service-Interface-Pattern)
  - Abhängig von: -

---

### Feature 2: Wizard-Step für Source-Auswahl

- [x] **Feature 2: Wizard Source Selection Step** – Im Wizard aus dem Katalog Quellen auswählen

  **Konzept:** Neuer Wizard-Step nach Environment-Setup (wird Step 4, Install wird Step 5). Zeigt die Registry-Einträge als Karten mit Checkboxen. Lokale `stacks/` Source ist immer dabei und vorausgewählt. User wählt weitere aus. Beim Install-Step werden die gewählten Sources angelegt und ein erster Sync gestartet.

  **UI-Design:**
  - Karten mit Name, Description, Category-Badge
  - Checkbox pro Karte (Multi-Select)
  - "Featured"/"Official" Badge für hervorgehobene Quellen
  - Lokale Source immer sichtbar, nicht abwählbar
  - "Skip" möglich (dann nur lokale Source)

  **Wizard-Flow:**
  ```
  Step 1: Admin    → Step 2: Organization → Step 3: Environment
  → Step 4: Stack Sources (NEU) → Step 5: Install (bestehend, erweitert)
  ```

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.WebUi/src/pages/Wizard/StackSourcesStep.tsx`
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Wizard/index.tsx` (neuer Step, totalSteps 4→5)
  - Edit: `src/ReadyStackGo.Api/Endpoints/Wizard/InstallEndpoint.cs` (Sources aus Request anlegen)
  - Edit: `src/ReadyStackGo.Application/UseCases/Wizard/Install/` (Sources-Parameter hinzufügen)
  - Pattern-Vorlage: Bestehende Wizard-Steps (`AdminStep.tsx`, `EnvironmentStep.tsx`)
  - Abhängig von: Feature 1

---

### Feature 3: Erweiterter Add-Source-Flow ("From Catalog")

- [x] **Feature 3: Add From Catalog** – Dritte Option im Add-Source-Flow

  **Konzept:** Der bestehende "Add Stack Source" Type-Selector bekommt eine dritte Option: "From Catalog". Diese zeigt die Registry-Einträge als Auswahlliste. Bei Auswahl wird die Git-Source mit vorausgefüllten Feldern angelegt — kein manuelles Tippen nötig.

  **UI-Flow:**
  ```
  Settings → Stack Sources → Add → Type-Auswahl:
    ├── Local Directory     (bestehend)
    ├── Git Repository      (bestehend)
    └── From Catalog (NEU)  → Registry-Einträge als Liste
                             → Auswahl → Source wird angelegt → Sync startet
  ```

  **Details:**
  - Registry-Einträge die bereits als Source existieren werden mit "Already added" markiert
  - Matching über `gitUrl` (exakter Vergleich)
  - One-Click: Auswahl → API-Call `POST /api/stack-sources/from-registry` → Source angelegt → Redirect zu Source-Liste
  - Kein separates Formular nötig (alle Felder aus Registry vorausgefüllt)

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/AddFromCatalog.tsx`
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/AddStackSourceSelect.tsx` (dritte Option)
  - Edit: `src/ReadyStackGo.WebUi/src/App.tsx` (neue Route `/settings/stack-sources/add/catalog`)
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/AddFromRegistry/AddFromRegistryCommand.cs`
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/AddFromRegistry/AddFromRegistryHandler.cs`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/AddFromRegistryEndpoint.cs`
  - Pattern-Vorlage: `AddGitSource.tsx`, `CreateStackSourceCommand`
  - Abhängig von: Feature 1

---

### Feature 4: Empty State im Stack Catalog

- [x] **Feature 4: Catalog Empty State** – Hinweis bei leerem Catalog

  **Konzept:** Wenn der Stack Catalog leer ist (keine Products geladen), wird ein Empty State angezeigt mit einem Hinweis und Link zum Hinzufügen von Sources.

  **UI-Design:**
  ```
  ┌──────────────────────────────────────┐
  │     📦 No stacks available           │
  │                                       │
  │  Add stack sources to discover        │
  │  deployable stacks.                   │
  │                                       │
  │  [Add Sources]  [Browse Catalog]      │
  └──────────────────────────────────────┘
  ```

  - "Add Sources" → `/settings/stack-sources/add`
  - "Browse Catalog" → `/settings/stack-sources/add/catalog` (From Catalog Flow)

  **Betroffene Dateien:**
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Catalog/StackCatalog.tsx` (Empty State hinzufügen)
  - Pattern-Vorlage: Bestehende Empty States (z.B. Health Dashboard "No deployments to monitor")
  - Abhängig von: Feature 3

---

### Feature 5: Import/Export

- [x] **Feature 5: Import/Export** – Source-Konfigurationen exportieren und importieren

  **Konzept:** Nutzer können ihre Stack-Source-Konfiguration als JSON exportieren (Download) und JSON-Dateien importieren (Upload → Sources werden angelegt). Nützlich für Team-Sharing und Backup. Credentials werden nicht exportiert/importiert.

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
  - Credentials werden weder exportiert noch importiert (Sicherheit, einfach halten)
  - Import-Preview zeigt was hinzugefügt wird

  **Betroffene Dateien:**
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/ExportSources/`
  - Neu: `src/ReadyStackGo.Application/UseCases/StackSources/ImportSources/`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/ExportSourcesEndpoint.cs`
  - Neu: `src/ReadyStackGo.Api/Endpoints/StackSources/ImportSourcesEndpoint.cs`
  - Edit: `src/ReadyStackGo.WebUi/src/pages/Settings/StackSources/StackSourcesList.tsx` (Export/Import Buttons)
  - Neu: `src/ReadyStackGo.WebUi/src/components/settings/ImportSourcesModal.tsx`
  - Pattern-Vorlage: Bestehende CRUD Commands
  - Abhängig von: -

---

- [x] **Dokumentation & Website** – Wiki, Public Website (DE/EN), Roadmap aktualisieren
  - Docs: Preconfigured Sources Anleitung, Wizard-Dokumentation aktualisieren
  - Public Website: Feature Highlight

- [x] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests:**
  - SourceRegistryService: Embedded Registry laden, Einträge filtern, Fehlerhandling
  - Import/Export: Serialisierung, Duplikaterkennung über gitUrl, Credentials nicht im Export
  - AddFromRegistry: Validierung, Source-Erstellung aus Registry-Eintrag
- **Integration Tests:**
  - Registry-Endpoint: Korrekte Antwort, Category/Tag-Filterung
  - AddFromRegistry-Endpoint: Source-Erstellung, Duplikat-Erkennung
  - Import-Endpoint: Upload, Duplikat-Handling, Fehler bei ungültigem Format
  - Export-Endpoint: Download, Credentials ausgeschlossen
- **E2E Tests:**
  - Wizard: Neuer Step für Source-Auswahl, Skip-Möglichkeit
  - Add From Catalog: Registry-Einträge anzeigen, One-Click-Add, "Already added" Badge
  - Catalog Empty State: Hinweis bei leerem Catalog, Links funktionieren
  - Import/Export: Export → Download, Upload → Preview → Import

## Offene Punkte

Alle geklärt.

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Registry-Quelle | A) Nur embedded JSON, B) Embedded + Remote-Update, C) Nur Remote | A | Einfach, offline-fähig, Updates über neue RSGO-Versionen |
| Wizard-Integration | A) Neuer Wizard-Step, B) Nur Settings | A | Bessere Onboarding-Experience, User sieht direkt verfügbare Quellen |
| Community-Stacks-Repo | A) Eigenes Repo erstellen, B) Nur Third-Party-Repos, C) Platzhalter | A | Offizielle kuratierte Quelle als Referenz, zeigt Best Practices |
| UI-Wege | A) Alle 4, B) Wizard+Marketplace, C) Nur Marketplace | Wizard + Add-From-Catalog + Empty State | Kein separater Marketplace — stattdessen erweiterter Add-Source-Flow. Marketplace als späteres Feature vorgemerkt. |
| Credentials bei Registry | A) Keine Credentials, B) Optional | A | Registry nur für öffentliche Repos, Credentials-Support ggf. als späteres Extra-Feature |
| Import/Export Credentials | A) Ohne Credentials, B) Optional, C) Verschlüsselt | A | Einfach halten, keine Sicherheitsrisiken durch Klartext-Credentials in Export-Dateien |

## Spätere Features (nicht in v0.24)

- **Marketplace-Seite**: Eigene Seite zum Browsen aller Registry-Einträge mit Filter, Search, Karten-Layout
- **Credentials in Import/Export**: Optional verschlüsselter Export von Credentials
- **Remote Registry-Update**: Registry-JSON von GitHub nachladen (wie Version-Check)
