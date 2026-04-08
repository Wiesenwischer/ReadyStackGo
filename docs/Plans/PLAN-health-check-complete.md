<!-- GitHub Epic: #351 -->
# Phase: Complete Health Check Support (v0.60)

## Ziel

ReadyStackGo soll Health Checks vollständig unterstützen: TCP Port-Probing für Services ohne HTTP-Endpunkt (z.B. Redis, PostgreSQL), Docker HEALTHCHECK-Passthrough bei Container-Erstellung, und korrekte Verdrahtung der Health-Config durch die gesamte Deployment-Pipeline.

## Analyse

### Bestehende Architektur

**Was bereits existiert:**
- Domain Models mit TCP-Typ: `RsgoHealthCheck.IsTcpHealthCheck`, `ServiceHealthCheckConfig`, `HealthCheckConfig`
- Vollständige HTTP Health Check Implementation: `IHttpHealthChecker` → `HealthMonitoringService`
- Docker Compose Parser extrahiert HEALTHCHECK korrekt (`DockerComposeParser.ParseHealthCheck`)
- Converter überträgt HEALTHCHECK von Compose nach Manifest (`DockerComposeToRsgoConverter`)
- Health Monitoring UI mit Status-Anzeige im Dashboard und Health-Seite

**Was fehlt (3 Lücken):**

| Lücke | Beschreibung | Betroffene Dateien |
|-------|-------------|-------------------|
| **TCP Probing** | Kein `ITcpHealthChecker` — TCP-Typ fällt auf Docker-Status zurück | `HealthMonitoringService.cs` |
| **HEALTHCHECK Passthrough** | `CreateContainerParameters.Healthcheck` wird nie gesetzt | `DockerService.cs:222-269` |
| **Request Model** | `CreateContainerRequest` hat kein Healthcheck-Feld | `IDockerService.cs:181-228`, `DeploymentEngine.cs` |

### Datenfluss (Ist-Zustand)

```
Manifest/Compose → RsgoHealthCheck → ServiceTemplate.HealthCheck
    → DeploymentStep ❌ (kein HealthCheck-Feld)
    → CreateContainerRequest ❌ (kein HealthCheck-Feld)
    → CreateContainerParameters ❌ (Healthcheck nie gesetzt)
    → Docker Container (HEALTHCHECK fehlt)
```

### Datenfluss (Soll-Zustand)

```
Manifest/Compose → RsgoHealthCheck → ServiceTemplate.HealthCheck
    → DeploymentStep.HealthCheck ✓
    → CreateContainerRequest.HealthCheck ✓
    → CreateContainerParameters.Healthcheck ✓ (Docker.DotNet.Models.HealthConfig)
    → Docker Container (HEALTHCHECK konfiguriert)

Monitoring: HealthMonitoringService
    → IsHttp? → IHttpHealthChecker ✓ (besteht)
    → IsTcp?  → ITcpHealthChecker ✓ (neu)
    → Docker? → Container Inspect ✓ (besteht)
```

### Betroffene Bounded Contexts

- **Domain**: Keine Änderungen nötig — `RsgoHealthCheck`, `ServiceHealthCheck`, `HealthCheckConfig` sind bereits vollständig modelliert
- **Application**: `HealthMonitoringService` um TCP-Branch erweitern, `IDockerService.CreateContainerRequest` um Health-Config erweitern
- **Infrastructure**: `ITcpHealthChecker` implementieren, `DockerService.CreateAndStartContainerAsync` um HEALTHCHECK erweitern, `DeploymentEngine` Healthcheck-Daten durchreichen
- **API**: Keine Änderungen nötig (Health API gibt bereits korrekte Daten zurück)
- **WebUI**: Keine Änderungen nötig (Health UI zeigt bereits alle Status-Typen an)

## AMS UI Counterpart

- [x] **Nein** — nur Backend/Infrastructure betroffen (kein UI-Code)

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [x] **Feature 1: TCP Health Checker** – `ITcpHealthChecker` Service mit async Socket-Probing
  - Neue Dateien:
    - `Application/Services/ITcpHealthChecker.cs` (Interface)
    - `Infrastructure/Services/Health/TcpHealthChecker.cs` (Implementation)
  - Pattern-Vorlage: `IHttpHealthChecker` / `HttpHealthChecker` (gleiche Architektur)
  - Logik: `TcpClient.ConnectAsync(host, port)` mit Timeout (default 5s)
  - Rückgabe: `Healthy` bei erfolgreicher Verbindung, `Unhealthy` bei Timeout/Fehler
  - DI-Registrierung in `Infrastructure/DependencyInjection.cs`
  - Abhängig von: –

- [x] **Feature 2: HealthMonitoringService TCP-Branch** – TCP-Probing in Monitoring einbauen
  - Datei: `Application/Services/Impl/HealthMonitoringService.cs`
  - Neben dem bestehenden `if (healthConfig?.IsHttp == true)` einen `else if (healthConfig?.IsTcp == true)` Block
  - Port-Auflösung: `healthConfig.Port` oder erster exponierter Port des Service
  - Host-Auflösung: Container-IP aus Docker Inspect (wie bei HTTP)
  - Abhängig von: Feature 1

- [x] **Feature 3: CreateContainerRequest Health Config** – Health-Check-Felder zum Request-Model hinzufügen
  - Datei: `Application/Services/IDockerService.cs` → `CreateContainerRequest`
  - Neues Property: `HealthCheckConfig? HealthCheck` (oder `RsgoHealthCheck?`)
  - Felder: Test (command list), Interval, Timeout, Retries, StartPeriod
  - Abhängig von: –

- [x] **Feature 4: Docker HEALTHCHECK Passthrough** – HEALTHCHECK bei Container-Erstellung setzen
  - Datei: `Infrastructure.Docker/DockerService.cs` → `CreateAndStartContainerAsync`
  - Mapping: `CreateContainerRequest.HealthCheck` → `Docker.DotNet.Models.HealthConfig`
  - `HealthConfig.Test`, `.Interval`, `.Timeout`, `.Retries`, `.StartPeriod`
  - TimeSpan-Strings (z.B. "30s") zu Nanosekunden konvertieren (Docker API Format)
  - Abhängig von: Feature 3

- [x] **Feature 5: DeploymentEngine Healthcheck-Verdrahtung** – Healthcheck-Daten durch Pipeline leiten
  - Dateien:
    - `Infrastructure/Services/Deployment/DeploymentEngine.cs` → Container-Erstellung
    - `Infrastructure/Services/Deployment/DeploymentStep.cs` (oder äquivalent) → Healthcheck-Feld
  - Daten aus `ServiceTemplate.HealthCheck` extrahieren und an `CreateContainerRequest` weitergeben
  - Nur Docker-Typ HEALTHCHECK weiterleiten (HTTP/TCP werden vom Monitoring-Service gehandelt)
  - Abhängig von: Feature 3, Feature 4

- [x] **Feature 6: Tests** – Umfassende Testabdeckung
  - Unit Tests:
    - `TcpHealthCheckerTests` — Verbindungserfolg, Timeout, Fehler, Port-Validierung
    - `HealthMonitoringService` TCP-Branch — Mock `ITcpHealthChecker`
    - `DockerService` HEALTHCHECK Mapping — Korrekte HealthConfig-Erstellung
    - TimeSpan-Konvertierung für Docker Nanosekunden
  - Integration Tests:
    - Container mit HEALTHCHECK erstellen und Status prüfen
  - Abhängig von: Features 1-5

- [ ] **Dokumentation & Website** – Release Notes, ggf. Manifest-Doku aktualisieren
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**: TcpHealthChecker (mock TCP), HealthMonitoringService TCP-Branch, DockerService HealthConfig Mapping, TimeSpan→Nanosekunden Konvertierung
- **Integration Tests**: Container mit HEALTHCHECK erstellen, TCP Health Check gegen laufenden Container
- **E2E Tests**: Nicht nötig — Health UI ändert sich nicht, nur die Datenquelle

## Offene Punkte

- [x] Soll der TCP Health Checker bei SSH-Tunnel-Environments übersprungen werden (wie HTTP)? → **Ja, überspringen** (konsistent mit HTTP)
- [x] Default TCP Timeout: 5 Sekunden wie HTTP oder kürzer (z.B. 3s)? → **5 Sekunden** (konsistent)
- [x] Soll bei `type: docker` + fehlendem HEALTHCHECK im Image eine Warnung geloggt werden? → **Nein** (zu noisy)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| TCP Check Mechanismus | TcpClient, Socket, NetworkStream | TcpClient | Einfachste API mit async Support und Timeout |
| HEALTHCHECK Scope | Nur Docker-Typ, Alle Typen | Nur Docker-Typ | HTTP/TCP werden vom RSGO Monitoring-Service gehandelt, Docker HEALTHCHECK ist Container-Level |
| Port-Auflösung bei TCP | Explizit (Pflicht), Fallback auf ersten Port | Fallback | Weniger Konfigurationsaufwand, konsistent mit Docker-Verhalten |
