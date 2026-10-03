# Phase: Health Check Details (Epic)

## Ziel

ASP.NET Core Services liefern über `/hc` Endpoints detaillierte Health Check Reports mit einzelnen Check-Einträgen (Database, Disk, Cache, Redis, etc.). Diese Daten werden heute zwar im `HttpHealthChecker` geparst, aber sofort verworfen. Ziel ist es, diese Daten vollständig zu erfassen, zu speichern und in der UI anzuzeigen — sowohl als Inline-Expansion im Health Dashboard als auch auf einer dedizierten Service-Detail-Seite.

**Vorbild**: ASP.NET Core Health UI (Xabaril/AspNetCore.Diagnostics.HealthChecks) — zeigt pro Service die einzelnen Checks mit Status, Description, Duration und Data an.

## Analyse

### Ist-Zustand

1. **`HttpHealthChecker.ParseHealthResponse()`** (Infrastructure) parst bereits JSON-Responses mit `entries`/`results`:
   - Extrahiert `status` pro Entry
   - Gibt `Dictionary<string, string>? Details` zurück (nur Name → Status)
   - **Problem**: Description, Duration, Data, Tags, Exception werden ignoriert

2. **`HealthMonitoringService.PerformHttpHealthCheckAsync()`** (Application) erhält `HttpHealthCheckResult` mit `Details`, aber:
   - Mappt nur `IsHealthy` → `HealthStatus` + `Reason`
   - **Verwirft `Details` komplett** — sie erreichen nie `ServiceHealth`

3. **`ServiceHealth`** (Domain) hat kein Feld für Health Check Entries

4. **`ServiceHealthDto`** (Application) hat kein Feld für Entries

5. **UI** zeigt pro Service nur: Name, Status-Badge, ContainerName, RestartCount, Reason

### ASP.NET Core HealthReport JSON-Format

Standard-Format (mit üblichem ResponseWriter):
```json
{
  "status": "Degraded",
  "totalDuration": "00:00:00.4512345",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "SQL Server connection OK",
      "duration": "00:00:00.0123456",
      "data": { "server": "sql01", "database": "ams_project" },
      "tags": ["db", "critical"],
      "exception": null
    },
    "disk": {
      "status": "Degraded",
      "description": "Disk space low on /data",
      "duration": "00:00:00.0001234",
      "data": { "freeSpace": "5GB", "totalSpace": "100GB" },
      "tags": ["infrastructure"],
      "exception": null
    },
    "redis": {
      "status": "Unhealthy",
      "description": "Connection refused",
      "duration": "00:00:05.0012345",
      "data": {},
      "tags": ["cache"],
      "exception": "System.Net.Sockets.SocketException: Connection refused"
    }
  }
}
```

Nicht alle Services liefern alle Felder — der Parser muss tolerant sein.

### Bestehende Architektur

| Layer | Datei | Rolle |
|-------|-------|-------|
| Domain | `ServiceHealth.cs` | Value Object für einzelnen Service |
| Domain | `HealthSnapshot.cs` | Aggregate Root mit `SelfHealth` |
| Application | `IHttpHealthChecker.cs` | Interface + `HttpHealthCheckResult` Record |
| Application | `HealthMonitoringService.cs` | Koordiniert Health Collection |
| Application | `HealthDtos.cs` | DTOs für API/SignalR |
| Application | `HealthSnapshotMapper.cs` | Domain → DTO Mapping |
| Infrastructure | `HttpHealthChecker.cs` | HTTP-Calls + JSON-Parsing |
| WebUI | `HealthServiceRow.tsx` | Service-Zeile im Dashboard |
| WebUI | `HealthStackCard.tsx` | Expandierbarer Stack-Card |
| WebUI | `DeploymentDetail.tsx` | Deployment-Detailseite |

### Betroffene Bounded Contexts

- **Domain**: Neues `HealthCheckEntry` Value Object, `ServiceHealth` erweitern
- **Application**: `HttpHealthCheckResult` erweitern, `ServiceHealthDto` erweitern, Mapping anpassen
- **Infrastructure**: `HttpHealthChecker.ParseHealthResponse()` vollständiges Parsing
- **API**: Keine neuen Endpoints nötig — Daten kommen über bestehende Health-Endpoints/SignalR
- **WebUI**: `HealthServiceRow` mit Expansion, neue Service Health Detail Page

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [x] **Feature 1: Full HealthReport Parsing** — `HttpHealthChecker` vollständig parsen
  - Betroffene Dateien:
    - `Infrastructure/Services/Health/HttpHealthChecker.cs` — `ParseHealthResponse()` erweitern
    - `Application/Services/IHttpHealthChecker.cs` — `HttpHealthCheckResult` erweitern, neues `HealthCheckEntryResult` Record
  - Pattern-Vorlage: Bestehende `ParseHealthResponse()` Methode
  - Abhängig von: -
  - Details:
    - Neues Record `HealthCheckEntryResult { Name, Status, Description?, Duration?, Data?, Tags?, Exception? }`
    - `HttpHealthCheckResult.Details` von `Dictionary<string, string>?` ändern zu `List<HealthCheckEntryResult>?`
    - Parser erweitern: description, duration (TimeSpan-String), data (Dictionary), tags (string[]), exception (string)
    - Tolerant parsen — fehlende Felder sind OK (nullable)
    - Auch `totalDuration` auf Top-Level parsen

- [x] **Feature 2: Domain Model erweitern** — `HealthCheckEntry` Value Object + `ServiceHealth` erweitern
  - Betroffene Dateien:
    - `Domain/Deployment/Health/HealthCheckEntry.cs` — Neues Value Object
    - `Domain/Deployment/Health/ServiceHealth.cs` — `HealthCheckEntries` Property + `ResponseTimeMs` hinzufügen
  - Pattern-Vorlage: Bestehende Value Objects wie `BusEndpointHealth`, `DatabaseHealth`
  - Abhängig von: -
  - Details:
    - `HealthCheckEntry` : ValueObject — Name, Status (HealthStatus), Description?, DurationMs?, Data (IReadOnlyDictionary?), Tags (IReadOnlyList?), Exception?
    - `ServiceHealth` erweitern: `HealthCheckEntries: IReadOnlyList<HealthCheckEntry>?`, `ResponseTimeMs: int?`
    - Factory Methods anpassen: `Create()` mit optionalem `entries` Parameter

- [x] **Feature 3: Health Data Pipeline durchgängig machen** — Von HttpChecker → Domain → DTO
  - Betroffene Dateien:
    - `Application/Services/Impl/HealthMonitoringService.cs` — `PerformHttpHealthCheckAsync()` + `CollectServiceHealthAsync()` erweitern
    - `Application/UseCases/Health/HealthDtos.cs` — `HealthCheckEntryDto`, `ServiceHealthDto` erweitern
    - `Application/UseCases/Health/HealthSnapshotMapper.cs` — Mapping für Entries
  - Pattern-Vorlage: Bestehendes Mapping in `HealthSnapshotMapper.MapToStackHealthDto()`
  - Abhängig von: Feature 1, Feature 2
  - Details:
    - `PerformHttpHealthCheckAsync()`: `HttpHealthCheckResult.Entries` → `HealthCheckEntry` Value Objects mappen
    - `CollectServiceHealthAsync()`: Entries + ResponseTimeMs an `ServiceHealth.Create()` übergeben
    - Neues `HealthCheckEntryDto { Name, Status, Description?, DurationMs?, Data?, Tags?, Exception? }`
    - `ServiceHealthDto` erweitern: `HealthCheckEntries: List<HealthCheckEntryDto>?`, `ResponseTimeMs: int?`
    - `HealthSnapshotMapper`: Domain entries → DTO entries mappen

- [x] **Feature 4: Inline Health Check Detail im Dashboard** — Service-Zeile expandierbar
  - Betroffene Dateien:
    - `WebUi/src/api/health.ts` — TypeScript Interfaces erweitern
    - `WebUi/src/components/health/HealthServiceRow.tsx` — Expandierbare Detail-Ansicht
    - `WebUi/src/components/health/HealthCheckEntryRow.tsx` — Neue Komponente für einzelnen Check
  - Pattern-Vorlage: Bestehende `HealthStackCard.tsx` Expand-Pattern
  - Abhängig von: Feature 3
  - Details:
    - `HealthCheckEntryDto` Interface in `health.ts`
    - `ServiceHealthDto` erweitern um `healthCheckEntries?: HealthCheckEntryDto[]`, `responseTimeMs?: number`
    - `HealthServiceRow`: Click auf Service-Zeile expandiert zu Health Check Entries
    - Jeder Entry: Name, Status-Badge (farblich), Description, Duration, Exception (wenn vorhanden)
    - Data-Dictionary als collapsible Key-Value-Liste
    - Tags als kleine Badges

- [x] **Feature 5: Persistenz der Health Check Entries** — EF Core Mapping für Entries
  - Betroffene Dateien:
    - `Infrastructure.DataAccess/Configurations/HealthSnapshotConfiguration.cs` — EF Core Mapping erweitern
    - `Infrastructure.DataAccess/ReadyStackGoDbContext.cs` — ggf. DbSet erweitern
  - Pattern-Vorlage: Bestehende `HealthSnapshot` Persistenz, JSON-Columns für komplexe Daten
  - Abhängig von: Feature 2
  - Details:
    - `HealthCheckEntry` als Owned Entity oder JSON-Column auf `ServiceHealth` speichern
    - JSON-Serialisierung für `Data` Dictionary (kein relationales Mapping nötig)
    - Tags als JSON-Array
    - Migration: `EnsureCreated()` wird Schema automatisch erweitern (pre-v1.0)

- [x] **Feature 6: Eigener API-Endpoint für Service Health Detail** — Dedizierter Endpoint
  - Betroffene Dateien:
    - `Application/UseCases/Health/GetServiceHealth/GetServiceHealthQuery.cs` — Neues Query
    - `Application/UseCases/Health/GetServiceHealth/GetServiceHealthHandler.cs` — Neuer Handler
    - `Api/Endpoints/Health/GetServiceHealthEndpoint.cs` — Neuer FastEndpoint
  - Pattern-Vorlage: `GetStackHealth` Query/Handler/Endpoint
  - Abhängig von: Feature 3, Feature 5
  - Details:
    - `GET /api/health/{environmentId}/deployments/{deploymentId}/services/{serviceName}`
    - Gibt `ServiceHealthDto` mit vollständigen `HealthCheckEntries` zurück
    - Optional `forceRefresh=true` Parameter für on-demand HTTP Health Check

- [x] **Feature 7: Dedizierte Service Health Detail Page** — Vollständige Analyse-Ansicht
  - Betroffene Dateien:
    - `WebUi/src/pages/Monitoring/ServiceHealthDetail.tsx` — Neue Page
    - `WebUi/src/api/health.ts` — Neuer API-Call `getServiceHealth()`
    - `WebUi/src/App.tsx` oder Router — Route registrieren
    - `WebUi/src/components/health/HealthServiceRow.tsx` — Link zur Detail-Page
    - `WebUi/src/pages/Deployments/DeploymentDetail.tsx` — Link zur Detail-Page
  - Pattern-Vorlage: `DeploymentDetail.tsx` als Layout-Vorlage
  - Abhängig von: Feature 4, Feature 6
  - Details:
    - Route: `/health/:deploymentId/:serviceName`
    - Header: Service Name, Status Badge, Container Info, Response Time
    - Health Check Entries als Cards mit vollständiger Detail-Ansicht:
      - Status, Description, Duration
      - Data als formatierte Key-Value-Tabelle
      - Tags als Badges
      - Exception als Code-Block (wenn vorhanden)
    - Breadcrumb: Deployments > {stackName} > Health > {serviceName}
    - Live-Update via SignalR (bestehender `useHealthHub`)
    - Link "View in Health Dashboard" zurück

- [x] **Feature 8: Tests** — Unit Tests für Parser, Domain, Mapping
  - Betroffene Dateien:
    - `tests/UnitTests/Infrastructure/Health/HttpHealthCheckerTests.cs` — Parser-Tests
    - `tests/UnitTests/Domain/Health/HealthCheckEntryTests.cs` — Neues Value Object
    - `tests/UnitTests/Application/Health/HealthSnapshotMapperTests.cs` — Mapping-Tests
  - Abhängig von: Feature 1-3
  - Details:
    - Parser: Vollständiges JSON, minimales JSON (nur Status), fehlende Felder, ungültiges JSON, leere Entries
    - Domain: Value Object Equality, Factory Methods, null-Handling
    - Mapping: Domain → DTO mit und ohne Entries, mit und ohne Data/Tags

- [x] **Dokumentation & Website** — Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie

### Unit Tests
- **HttpHealthChecker Parser**: Verschiedene JSON-Formate (ASP.NET Standard, Custom, minimal, mit/ohne entries, mit/ohne data/tags/exception/duration)
- **HealthCheckEntry Value Object**: Equality, Create mit null-Werten, Edge Cases
- **HealthSnapshotMapper**: ServiceHealth mit/ohne Entries → ServiceHealthDto Mapping
- **HealthMonitoringService**: HttpResult mit Entries → ServiceHealth korrekt gemappt

### Integration Tests
- HTTP Health Check gegen echten ASP.NET Container (TestContainers)
- End-to-End: Health Collection → DTO → Entries vorhanden

### E2E Tests
- Dashboard: Stack expandieren → Service expandieren → Health Check Entries sichtbar
- Service Detail Page: Navigation, Darstellung, Live-Update
- Edge Case: Service ohne HTTP Health Check → keine Entries angezeigt

## Offene Punkte

(alle geklärt — siehe Entscheidungen)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Entry-Detailgrad | Status-only, +Description/Error, Full (Data/Tags) | Full | User-Wunsch: volle Transparenz wie ASP.NET Health UI |
| UI-Layout | Inline-only, Detail-Page-only, Beides | Beides | Inline für Quick-View, Detail-Page für Analyse |
| Persistence | In-Memory only, DB-Persist | DB-Persist | Ermöglicht History-Ansicht der Check-Entries über Zeit |
| totalDuration | Anzeigen, Nicht anzeigen | Nicht anzeigen | Kein Mehrwert auf Stack-Level, parsen aber nicht in UI anzeigen |
| Detail-Page Endpoint | Bestehender GetStackHealth, Eigener Endpoint | Eigener Endpoint | Dedizierter Endpoint für Service-Level Health mit Entries — vermeidet Überladung des Stack-Endpoints |
| `HttpHealthCheckResult.Details` Breaking Change | Neues Feld, Details umbenennen | Neues Feld `Entries` | `Details` als deprecated markieren, neues `Entries: List<HealthCheckEntryResult>?` |
