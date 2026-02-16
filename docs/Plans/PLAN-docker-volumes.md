# Phase: Docker Volumes Management

## Ziel

Docker Volumes per Environment auflisten, Details anzeigen, erstellen/löschen und verwaiste Volumes erkennen. Damit erhält der Admin Überblick und Kontrolle über den persistenten Speicher seiner Stacks — analog zur bestehenden Container-Ansicht.

## Analyse

### Bestehende Architektur

| Komponente | Pfad | Relevanz |
|---|---|---|
| `IDockerService` | `Application/Services/IDockerService.cs` | Interface erweitern um Volume-Methoden |
| `DockerService` | `Infrastructure.Docker/DockerService.cs` | Docker.DotNet Volume-API aufrufen |
| `DockerNamingUtility` | `Infrastructure.Docker/DockerNamingUtility.cs` | `CreateVolumeName()` für Stack-Volume-Zuordnung |
| `VolumeDefinition` | `Domain/StackManagement/Stacks/VolumeDefinition.cs` | Bestehend, Read-Only — nicht ändern |
| `ContainerDto` | `Application/UseCases/Containers/ContainerDto.cs` | Pattern-Vorlage für VolumeDto |
| `ListContainersEndpoint` | `Api/Endpoints/Containers/ListContainersEndpoint.cs` | Pattern-Vorlage für Endpoint |
| `Containers.tsx` | `WebUi/src/pages/Monitoring/Containers.tsx` | Pattern-Vorlage für UI-Seite |
| `containers.ts` | `WebUi/src/api/containers.ts` | Pattern-Vorlage für API-Client |
| `AppSidebar.tsx` | `WebUi/src/layout/AppSidebar.tsx` | Neuer Menüeintrag "Volumes" |

### Docker.DotNet Volume-API

Docker.DotNet bietet `client.Volumes`:
- `ListAsync()` → `VolumesListResponse` (Name, Driver, Mountpoint, Labels, Scope, CreatedAt, UsageData)
- `CreateAsync(VolumesCreateParameters)` → `VolumeResponse`
- `RemoveAsync(name, force)` → void
- `InspectAsync(name)` → `VolumeResponse` (Details inkl. Options, Status)

### Orphaned Volume Detection

Ein Volume gilt als "orphaned" wenn:
1. Es nicht als Mount in einem laufenden oder gestoppten Container referenziert wird
2. Es kein `rsgo.stack`-Label hat ODER der referenzierte Stack nicht mehr existiert

Erkennung: `client.Containers.ListAllAsync()` → alle Container-Mounts sammeln → mit Volume-Liste abgleichen.

### Betroffene Bounded Contexts

- **Domain**: Keine neuen Entities nötig — Volumes sind Docker-Runtime-Objekte, kein persistenter Domain-State
- **Application**: Neues `VolumeDto`, neue Query/Command-Handler (ListVolumes, CreateVolume, RemoveVolume)
- **Infrastructure.Docker**: `IDockerService` erweitern, `DockerService` implementieren
- **API**: Neue Endpoints unter `/api/volumes`
- **WebUI**: Neue Seite `/volumes`, API-Client, Sidebar-Eintrag

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten — von innen nach außen:

- [ ] **Feature 1: Volume List Backend** – IDockerService + Endpoints für Volume-Liste
  - Neue Dateien:
    - `Application/UseCases/Volumes/VolumeDto.cs`
    - `Application/UseCases/Volumes/ListVolumesQuery.cs`
    - `Application/UseCases/Volumes/ListVolumesHandler.cs`
    - `Api/Endpoints/Volumes/ListVolumesEndpoint.cs`
  - Geänderte Dateien:
    - `Application/Services/IDockerService.cs` (neue Methoden: `ListVolumesAsync`)
    - `Infrastructure.Docker/DockerService.cs` (Implementierung)
  - Pattern-Vorlage: `ListContainersEndpoint` + `ListContainersQuery`
  - Abhängig von: –

- [ ] **Feature 2: Volume Details Backend** – Inspect-Endpoint für einzelnes Volume
  - Neue Dateien:
    - `Application/UseCases/Volumes/GetVolumeQuery.cs`
    - `Application/UseCases/Volumes/GetVolumeHandler.cs`
    - `Api/Endpoints/Volumes/GetVolumeEndpoint.cs`
  - Geänderte Dateien:
    - `Application/Services/IDockerService.cs` (`InspectVolumeAsync`)
    - `Infrastructure.Docker/DockerService.cs`
  - VolumeDto erweitern: Size, Mountpoint, Driver, DriverOpts, Labels, CreatedAt, UsageData
  - Abhängig von: Feature 1

- [ ] **Feature 3: Orphaned Volume Detection** – Volumes ohne Container-Referenz markieren
  - Geänderte Dateien:
    - `Infrastructure.Docker/DockerService.cs` (Container-Mounts mit Volume-Liste abgleichen)
    - `Application/UseCases/Volumes/VolumeDto.cs` (`IsOrphaned` Flag, `ReferencedByContainers` Liste)
  - Logik: Alle Container (inkl. gestoppte) inspizieren → Mounts sammeln → Volume-Liste annotieren
  - Abhängig von: Feature 1

- [ ] **Feature 4: Create/Delete Volume Backend** – CRUD-Endpoints
  - Neue Dateien:
    - `Application/UseCases/Volumes/CreateVolumeCommand.cs`
    - `Application/UseCases/Volumes/CreateVolumeHandler.cs`
    - `Application/UseCases/Volumes/RemoveVolumeCommand.cs`
    - `Application/UseCases/Volumes/RemoveVolumeHandler.cs`
    - `Api/Endpoints/Volumes/CreateVolumeEndpoint.cs`
    - `Api/Endpoints/Volumes/RemoveVolumeEndpoint.cs`
  - Geänderte Dateien:
    - `Application/Services/IDockerService.cs` (`CreateVolumeAsync`, `RemoveVolumeAsync`)
    - `Infrastructure.Docker/DockerService.cs`
  - Sicherheit: Delete nur für Volumes ohne Container-Referenz (oder mit `force` Flag)
  - Abhängig von: Feature 1

- [ ] **Feature 5: Volumes UI – Liste** – Neue Seite mit Volume-Übersicht
  - Neue Dateien:
    - `WebUi/src/api/volumes.ts` (API-Client)
    - `WebUi/src/pages/Monitoring/Volumes.tsx` (Seite)
  - Geänderte Dateien:
    - `WebUi/src/layout/AppSidebar.tsx` (Menüeintrag "Volumes" nach "Containers")
    - `WebUi/src/App.tsx` (Route `/volumes` mit `EnvironmentGuard`)
    - `WebUi/src/icons/index.ts` (neues Storage/Cylinder-Icon exportieren)
  - Neue Dateien:
    - `WebUi/src/icons/storage.svg` (neues dediziertes Volume-Icon)
  - UI: Tabelle mit Name, Driver, Container-Count, Orphaned-Badge, Created (Size erst in Details)
  - Filter: Orphaned-Only Toggle
  - Pattern-Vorlage: `Containers.tsx`
  - Abhängig von: Feature 1, 3

- [ ] **Feature 6: Volumes UI – Details & Actions** – Detail-Ansicht, Create/Delete Dialoge
  - Geänderte Dateien:
    - `WebUi/src/pages/Monitoring/Volumes.tsx` (Detail-Panel oder Modal)
  - Detail-Ansicht: Mountpoint, Labels, Driver Options, referenzierende Container
  - Create-Dialog: Name, Driver (optional), Labels (optional)
  - Delete: Bestätigungsdialog, Warnung wenn noch referenziert
  - Bulk-Delete: "Delete All Orphaned" Button mit Bestätigungsdialog (listet alle betroffenen Volumes)
  - Abhängig von: Feature 2, 4, 5

- [ ] **Tests** – Unit + Integration
  - Neue Dateien:
    - `tests/ReadyStackGo.UnitTests/Application/Volumes/ListVolumesHandlerTests.cs`
    - `tests/ReadyStackGo.UnitTests/Application/Volumes/CreateVolumeHandlerTests.cs`
    - `tests/ReadyStackGo.UnitTests/Application/Volumes/RemoveVolumeHandlerTests.cs`
  - Unit Tests: Handler-Logik, Orphaned-Detection, Delete-Schutz
  - Edge Cases: Leere Volume-Liste, Volume mit laufendem Container (Delete verhindern), Docker-Verbindungsfehler
  - Abhängig von: Feature 1-4

- [ ] **Dokumentation & Website** – Wiki, Public Website (DE/EN), Roadmap
  - Abhängig von: Tests

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main
  - Abhängig von: alle

## Test-Strategie

- **Unit Tests**: Handler-Logik (List, Create, Remove), Orphaned-Detection-Algorithmus, Validierung (leerer Name, ungültige Zeichen)
- **Integration Tests**: Endpoint-Tests mit gemocktem IDockerService (analog zu bestehenden Container-Endpoint-Tests)
- **Edge Cases**: Volume löschen das noch von Container referenziert wird, Volume mit sehr langem Namen, Docker-Verbindung nicht erreichbar

## Offene Punkte

- [x] Icon für Sidebar → **Neues dediziertes Storage/Cylinder-Icon** erstellen
- [x] Volume-Size → **On-Demand** in Detail-Ansicht laden (Liste bleibt schnell)
- [x] Bulk-Delete → **Ja**, "Delete All Orphaned" Button mit Bestätigungsdialog (listet betroffene Volumes auf)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Domain-Modell | A) Neues Volume Aggregate, B) Nur DTOs (Runtime-Daten) | **B) Nur DTOs** | Volumes sind Docker-Runtime-State, nicht persistiert in unserer DB. Kein DDD-Aggregate nötig. |
| API-Route | A) `/api/volumes?environment=X`, B) `/api/environments/{id}/volumes` | – | Noch offen — bestehende Container-API nutzt Option A |
| Orphaned-Detection | A) Clientseitig (Frontend), B) Serverseitig (Backend) | **B) Serverseitig** | Backend hat Zugriff auf alle Container-Mounts, effizientere Abfrage |
| Sidebar-Position | A) Nach "Containers", B) Unter "Containers" als Sub-Item | – | Noch offen |
| Sidebar-Icon | A) Bestehendes Icon, B) Neues Storage-Icon | **B) Neues Icon** | Dediziertes Cylinder/Storage-Icon für bessere visuelle Unterscheidung |
| Volume-Size laden | A) Immer in Liste, B) On-Demand in Details | **B) On-Demand** | Liste bleibt schnell, Size wird erst bei Detail-Ansicht via Inspect geladen |
| Bulk-Delete Orphaned | A) Nur einzeln, B) "Delete All Orphaned" Button | **B) Bulk-Delete** | Mit Bestätigungsdialog der alle betroffenen Volumes auflistet |
