# Phase: Container Management Improvements

## Ziel

Die Container-Seite um Delete-Aktion, Stack/Product-Zuordnung und verschiedene Ansichten (List, Stack, Product) erweitern. Verwaiste Container (RSGO-Labels aber kein Deployment) werden erkannt und markiert. Externe Container ohne RSGO-Labels erscheinen als "Unmanaged" Gruppe.

## Analyse

### Bestehende Architektur

| Komponente | Pfad | Relevanz |
|---|---|---|
| `IDockerService` | `Application/Services/IDockerService.cs` | `RemoveContainerAsync()` bereits vorhanden |
| `DockerService` | `Infrastructure.Docker/DockerService.cs:265` | Remove-Implementierung existiert (force + non-force) |
| `StopContainerCommand` | `Application/UseCases/Containers/StopContainer/` | Pattern-Vorlage für RemoveContainer |
| `StopContainerEndpoint` | `Api/Endpoints/Containers/StopContainerEndpoint.cs` | Pattern-Vorlage für Endpoint |
| `ContainerDto` | `Application/UseCases/Containers/ContainerDto.cs` | Enthält bereits `Labels` Dictionary |
| `IDeploymentRepository` | `Domain/Deployment/Deployments/IDeploymentRepository.cs` | `GetByStackName()` für Orphan-Erkennung |
| `IProductCache` | `Application/Services/IProductCache.cs` | `GetStack(stackId)` für Product-Zuordnung |
| `StackId` | `Domain/StackManagement/Stacks/StackId.cs` | `TryParse()` zum Parsen von Deployment.StackId |
| `StackDefinition` | `Domain/StackManagement/Stacks/StackDefinition.cs` | `ProductName`, `ProductDisplayName` |
| `Containers.tsx` | `WebUi/src/pages/Monitoring/Containers.tsx` | Hauptdatei für UI-Umbau |
| `containers.ts` | `WebUi/src/api/containers.ts` | API-Client, aktuell: list, start, stop |
| `VolumeDetail.tsx` | `WebUi/src/pages/Monitoring/VolumeDetail.tsx` | Pattern-Vorlage für Delete-Confirmation |

### Container-Labels (gesetzt bei Deployment)

```
rsgo.stack       = <stackName>        → Stack-Zuordnung
rsgo.context     = <serviceName>      → Service innerhalb des Stacks
rsgo.environment = <environmentId>    → Environment-Zuordnung
rsgo.lifecycle   = service | init     → Container-Typ
```

### Bestehende API-Endpoints

| Method | Route | Permission | Status |
|--------|-------|-----------|--------|
| GET | `/api/containers?environment=...` | Deployments.Read | Existiert |
| POST | `/api/containers/{id}/start?environment=...` | Deployments.Update | Existiert |
| POST | `/api/containers/{id}/stop?environment=...` | Deployments.Update | Existiert |
| DELETE | `/api/containers/{id}?environment=...&force=...` | Deployments.Delete | **NEU** |
| GET | `/api/containers/context?environment=...` | Deployments.Read | **NEU** |

### Container-zu-Product Mapping

```
Container (Docker)
  └─ Label: rsgo.stack = "wordpress"
      └─ Deployment (SQLite, GetByStackName)
          └─ StackId = "builtin:wordpress:5.9:wordpress"
              └─ StackId.TryParse() → StackId Record
                  └─ IProductCache.GetStack(stackId)
                      └─ StackDefinition.ProductName / ProductDisplayName
```

### Orphan-Erkennung

Ein Container gilt als **orphaned** wenn:
1. Er ein `rsgo.stack` Label hat (wurde von RSGO deployed)
2. Kein aktives Deployment (Status != Removed) für diesen Stack-Namen existiert

Container **ohne** `rsgo.stack` Label sind **nicht orphaned** — sie sind "unmanaged" (extern, z.B. Portainer, Traefik).

## Features / Schritte

- [x] **Feature 1: RemoveContainer Backend** — Use Case + Endpoint
  - Neue Dateien:
    - `Application/UseCases/Containers/RemoveContainer/RemoveContainerCommand.cs`
    - `Application/UseCases/Containers/RemoveContainer/RemoveContainerHandler.cs`
    - `Api/Endpoints/Containers/RemoveContainerEndpoint.cs`
  - Pattern-Vorlage: `StopContainer*` (1:1 Kopie mit Anpassungen)
  - Handler: Safety-Check (running container ohne force → Fehler), dann `_dockerService.RemoveContainerAsync()`
  - Endpoint: `DELETE /api/containers/{id}`, Permission `Deployments.Delete`
  - Abhängig von: –

- [x] **Feature 2: GetContainerContext Backend** — Use Case + Endpoint
  - Neue Dateien:
    - `Application/UseCases/Containers/GetContainerContext/GetContainerContextQuery.cs`
    - `Application/UseCases/Containers/GetContainerContext/GetContainerContextHandler.cs`
    - `Api/Endpoints/Containers/GetContainerContextEndpoint.cs`
  - DTOs:
    - `StackContextInfo { StackName, DeploymentExists, DeploymentId?, ProductName?, ProductDisplayName? }`
    - `GetContainerContextResult { Stacks: Dictionary<string, StackContextInfo> }`
  - Handler-Logik:
    1. `ListContainersAsync()` → unique `rsgo.stack` Labels extrahieren
    2. Pro Stack: `GetByStackName()` → Deployment vorhanden?
    3. Falls Deployment: `StackId.TryParse(deployment.StackId)` → `_productCache.GetStack()` → ProductName
  - Dependencies: `IDockerService`, `IDeploymentRepository`, `IProductCache`
  - Abhängig von: –

- [x] **Feature 3: Backend-Tests**
  - `RemoveContainerHandlerTests` (5 Tests):
    - `StoppedContainer_ReturnsSuccess`
    - `RunningContainer_WithoutForce_ReturnsError`
    - `RunningContainer_WithForce_ReturnsSuccess`
    - `NonExistentContainer_ReturnsError`
    - `DockerServiceThrows_ReturnsError`
  - `GetContainerContextHandlerTests` (6 Tests):
    - `AllStacksHaveDeployments_AllContextsPopulated`
    - `ContainersWithoutLabels_NotIncluded`
    - `StackWithoutDeployment_DeploymentExistsFalse`
    - `StackWithDeployment_ProductInfoResolved`
    - `StackWithDeployment_ProductNotInCache_ProductNameNull`
    - `DockerServiceThrows_ReturnsError`
  - Abhängig von: Feature 1, Feature 2

- [x] **Feature 4: Frontend — API-Client + Containers.tsx**
  - `api/containers.ts` erweitern:
    - `remove(environmentId, id, force)` via `apiDelete`
    - `getContext(environmentId)` → `ContainerContextResult`
    - Neue TypeScript-Interfaces: `StackContextInfo`, `ContainerContextResult`
  - `Containers.tsx` komplett überarbeiten:
    - **Action Buttons**: Icon-only (Play/Stop/Trash) in subtlem Grau statt rotem/grünem Hintergrund
    - **Remove Confirmation**: Inline "Remove?" + ✓/✗ (VolumeDetail-Pattern)
    - **View Toggle**: List / Stacks / Products — Umschaltung im Header
    - **List View**: Tabelle mit Stack + Product Spalten, orphaned Badge
    - **Stack View**: Gruppierung nach `rsgo.stack`, Unmanaged-Gruppe am Ende
    - **Product View**: Gruppierung nach Product, Stacks darunter, Unmanaged am Ende
  - Abhängig von: Feature 1, Feature 2

- [x] **Phase abschließen** — Build, Tests, PR

## View-Konzept

### View Toggle (Header)

Drei Buttons im Header-Bereich: `[≡ List] [▦ Stacks] [⊞ Products]`
Aktiver Button hervorgehoben. State in `useState`, kein localStorage nötig.

### List View (Default)

Flache Tabelle, Grid `sm:grid-cols-10`:

| Name (2) | Stack (2) | Product (2) | Image (2, hidden mobile) | Status (1) | Actions (1) |
|-----------|-----------|-------------|--------------------------|------------|-------------|
| wp-app | wordpress ↗ | WordPress | wordpress:6.4 | ● healthy | ▶ ■ 🗑 |
| wp-db | wordpress ↗ | WordPress | mysql:8 | ● healthy | ▶ ■ 🗑 |
| redis | redis-test ⚠ orphaned | – | redis:7 | ● running | ▶ ■ 🗑 |
| portainer | – | – | portainer/portainer | ● running | ▶ ■ 🗑 |

- Stack-Spalte: Link zu `/deployments/{stackName}` wenn Deployment existiert, amber "orphaned" Badge wenn nicht, "–" ohne Label
- Product-Spalte: `productDisplayName` aus Context, "–" wenn nicht zuordbar
- Actions: Icon-Buttons `p-1.5 rounded text-gray-500 hover:bg-gray-100`

### Stack View (Gruppiert)

```
┌─ wordpress ──────────────────── Running (2 containers) ───┐
│  wp-app     | wordpress:6.4 | healthy  | 8080:80 | ▶ ■ 🗑 │
│  wp-db      | mysql:8       | healthy  | –       | ▶ ■ 🗑 │
└───────────────────────────────────────────────────────────┘

┌─ redis-test ────────────── ⚠ Orphaned (1 container) ─────┐
│  redis       | redis:7     | running  | 6379    | ▶ ■ 🗑 │
└───────────────────────────────────────────────────────────┘

┌─ Unmanaged ──────────────────── 2 containers ────────────┐
│  portainer  | portainer/.. | running  | 9443    | ▶ ■ 🗑 │
│  traefik    | traefik:v3   | running  | 80      | ▶ ■ 🗑 │
└───────────────────────────────────────────────────────────┘
```

Gruppen-Header: Stack-Name (Link wenn Deployment existiert), Status-Badge, Container-Count.

### Product View (Gruppiert nach Produkt)

```
┌─ WordPress ──────────────────────────────────────────────┐
│  ┌ wordpress (v6.4) ── Running ────────────────────────┐ │
│  │  wp-app  | healthy | 8080:80 | ▶ ■ 🗑              │ │
│  │  wp-db   | healthy | –       | ▶ ■ 🗑              │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘

┌─ Unknown Product ────────────────────────────────────────┐
│  ┌ redis-test ── ⚠ Orphaned ──────────────────────────┐ │
│  │  redis | running | 6379 | ▶ ■ 🗑                   │ │
│  └─────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘

┌─ Unmanaged ──────────────────────────────────────────────┐
│  portainer | running | 9443 | ▶ ■ 🗑                     │
│  traefik   | running | 80   | ▶ ■ 🗑                     │
└──────────────────────────────────────────────────────────┘
```

Orphaned Stacks → "Unknown Product" Gruppe. Unmanaged → eigene Gruppe am Ende.

## Test-Strategie

- **Unit Tests**: RemoveContainerHandler (Safety-Check, Force-Flag, Fehlerfälle) + GetContainerContextHandler (Label-Parsing, Deployment-Lookup, Product-Cache, Fehlerfälle)
- **E2E Tests**: Später per `document-feature` Skill
- **Manuell**: Docker-Container starten, Container-Seite prüfen, alle 3 Views durchklicken, Remove testen

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Remove Permission | Deployments.Update, Deployments.Delete | Deployments.Delete | Destruktive Aktion, konsistent mit RemoveVolume/RemoveDeployment |
| Orphan-Erkennung | Separater Endpoint, In ListContainers, Frontend-only | Separater Context-Endpoint | ListContainers bleibt schnell (Docker-only), Context-Daten parallel geladen |
| Externe Container | Ausblenden, Als "orphaned" zeigen, Als "Unmanaged" | Unmanaged-Gruppe | Nicht von RSGO verwaltet = nicht verwaist, eigene Kategorie |
| Action Buttons | Farbige Text-Buttons, Icon-Buttons grau | Icon-Buttons grau | Weniger visuelles Rauschen, konsistenter mit modernen UIs |
| Ansichten | Nur List, List+Stack, Alle drei | Alle drei (List+Stack+Product) | Maximale Flexibilität bei der Container-Übersicht |
