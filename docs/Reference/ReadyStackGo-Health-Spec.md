# ReadyStackGo – Health & Operation Mode Specification

## 1. Goals

The health system of ReadyStackGo (RSGO) should:

- Quickly show on **Org / Environment / Stack** level:
  - Is everything running normally?
  - Is something _planned_ to be restricted (Migration / Maintenance)?
  - Is something _unplanned_ broken?
- Support **first-party stacks** (ams.project etc.) with deep integration (NServiceBus, custom health endpoints).
- Sensibly aggregate **third-party stacks** (Keycloak, Postgres, external products), even if they only provide Docker status or generic HTTP health.

Important principle:

> **RSGO is the source of truth for the operation mode (Normal, Migration, Maintenance).
> Containers provide technical states (up/down, errors, bus status).**

---

## 2. Core Concepts

### 2.1 HealthStatus

Enum for the technical state:

- `Healthy`
- `Degraded`
- `Unhealthy`
- `Unknown`

Used for:
- Overall status of a stack
- Bus
- Infra
- Self (Container/Services)

### 2.2 OperationMode

Enum for the _operation mode_ of a stack deployment:

- `Normal`
  → Normal operation, no planned restriction.
- `Migrating`
  → Planned migration/upgrade running (e.g., DB migrations, stack upgrade).
- `Maintenance`
  → Stack deliberately put into maintenance mode (planned).
- `Stopped`
  → Stack intentionally stopped (not available, but no error).
- `Failed` (optional)
  → Last deploy/upgrade/migration failed, manual intervention required.

**Important:**
`OperationMode` is **controlled by RSGO**, not by the containers.

### 2.3 DeploymentStatus & MigrationStatus

Additional states in the Deployment domain:

- `DeploymentStatus`:
  - `Idle`
  - `Deploying`
  - `Upgrading`
  - `RollingBack`
  - `Failed`

- `MigrationStatus`:
  - `None`
  - `Running`
  - `Succeeded`
  - `Failed`

These values help to set OperationMode consistently.

---

## 3. Health Domain Model

### 3.1 HealthSnapshot

RSGO creates regular health snapshots per Org/Env/Stack:

```csharp
enum HealthStatus { Healthy, Degraded, Unhealthy, Unknown }

enum OperationMode { Normal, Migrating, Maintenance, Stopped, Failed }

class HealthSnapshot {
    Guid OrgId;
    Guid EnvironmentId;
    Guid StackId;

    DateTime CapturedAtUtc;
    HealthStatus Overall;
    OperationMode OperationMode;

    string? TargetVersion;       // e.g., "0.5.0" during upgrade
    string? CurrentVersion;      // e.g., "0.4.2"

    BusHealth? Bus;
    InfraHealth? Infra;
    SelfHealth Self;
}
```

### 3.2 BusHealth

Primarily used for NServiceBus-based stacks (first-party or other NSB apps):

```csharp
class BusHealth {
    HealthStatus Status;          // Healthy/Degraded/Unhealthy/Unknown

    string? TransportKey;         // e.g., "primary-sql"
    bool HasCriticalError;
    string? CriticalErrorMessage;

    DateTime? LastHealthPingProcessedUtc;
    TimeSpan? TimeSinceLastPing;
    TimeSpan? UnhealthyAfter;     // Config value, when it's considered "too old"

    // Optional: affected endpoints
    IReadOnlyList<BusEndpointHealth> Endpoints;
}

class BusEndpointHealth {
    string EndpointName;
    HealthStatus Status;
    DateTime? LastPingUtc;
    string? Reason;               // "NoMessagesRecently", "HeartbeatMissing", etc.
}
```

### 3.3 InfraHealth

Generic infrastructure checks (optional per stack):

```csharp
class InfraHealth {
    IReadOnlyList<DatabaseHealth> Databases;
    IReadOnlyList<DiskHealth> Disks;
    IReadOnlyList<ExternalServiceHealth> ExternalServices;
}

class DatabaseHealth {
    string Id;             // e.g., "ams_project_db"
    HealthStatus Status;
    int? LatencyMs;
    string? Error;
}

class DiskHealth {
    string Mount;          // e.g., "/"
    HealthStatus Status;
    double? FreePercent;
    string? Error;
}

class ExternalServiceHealth {
    string Id;             // e.g., "smtp"
    HealthStatus Status;
    string? Error;
}
```

### 3.4 SelfHealth

State of the containers/services controlled by the stack:

```csharp
class SelfHealth {
    IReadOnlyList<ServiceHealth> Services;
}

class ServiceHealth {
    string Name;                // e.g., "ams-api"
    HealthStatus Status;        // From container/health endpoint perspective
    string? ContainerId;
    string? Reason;             // "Restarting", "CrashLoop", "HealthCheckFailed"
    int? RestartCount;
}
```

---

## 4. Data Source & Integration

### 4.1 Container / Docker (Self)

- RSGO connects to the Environment host via Docker API:
  - Status of containers (running, exited, restarting)
  - Restart counts, exit codes
- Optional: HTTP health URL from manifest:

```yaml
services:
  - name: ams-api
    image: ...
    health:
      type: http
      url: http://ams-api:8080/health
      timeout: 5s
```

RSGO polls these endpoints and sets `ServiceHealth.Status` accordingly.

### 4.2 Bus (NServiceBus / EndpointHealth)

For NServiceBus-based applications:

- Each endpoint uses your package `Wiesenwischer.NServiceBus.EndpointHealth` and an ASP.NET Health endpoint (`/health`).
- This provides:
  - MessagePump status
  - `HasCriticalError`
  - `LastHealthPingProcessedUtc`
  - `TransportKey`

RSGO:

- knows from the manifest which services are endpoints and where their health URLs are
- retrieves these health endpoints
- aggregates bus status per `TransportKey` and stack.

For **non-NSB stacks**:

- `BusHealth` can simply be `null` or `Status=Unknown`
- or you allow generic "messaging checks" via manifest (e.g., RabbitMQ HTTP API), but this is optional/"Advanced".

### 4.3 Infra (DB, Disk, external services)

- For first-party stacks, the manifest can define DB/service checks:

```yaml
infra:
  databases:
    - id: ams_project_db
      connectionParam: DB_MAIN
  externalServices:
    - id: smtp
      url: smtp.example.com:587
```

- RSGO can:
  - make short connection pings to DBs (configurable)
  - query free disk space via agent/host metrics
- For third-party stacks:
  - InfraHealth can remain empty or contain only generic checks (e.g., "DB container is running").

---

## 5. Aggregation Logic

### 5.1 Overall Status

Pseudo-rule:

1. If `OperationMode == Migrating` or `Maintenance` or `Stopped`:
   - `overall` at least `Degraded`
   - real errors can elevate `overall` to `Unhealthy` (e.g., migration failed).
2. If `OperationMode == Normal`:
   - `overall` is the "maximum"/"worst" of Bus/Infra/Self
     - if one is `Unhealthy` → `overall = Unhealthy`
     - otherwise if one is `Degraded` → `overall = Degraded`
     - otherwise `overall = Healthy`
3. If no data available:
   - `overall = Unknown`

### 5.2 OperationMode is Set by RSGO

- During Deploy/Upgrade/Migration:
  - `OperationMode = Migrating`
  - `DeploymentStatus = Upgrading`
  - `MigrationStatus = Running`
- During planned maintenance:
  - `OperationMode = Maintenance`
- During deliberate stop:
  - `OperationMode = Stopped`
- On failed upgrade/migration:
  - `OperationMode = Failed`
  - `DeploymentStatus = Failed`
  - `MigrationStatus = Failed`

The health engine always reads `OperationMode` first and interprets container states in context:

- `OperationMode=Normal` + many broken containers → real incident.
- `OperationMode=Migrating` + services down → expected restriction (Degraded).

### 5.3 Container Lifecycle in Maintenance Mode

When switching to maintenance mode, containers are automatically stopped:

```
Normal → Maintenance: All stack containers are stopped
Maintenance → Normal: All stack containers are started
```

**Exception:** Containers with the label `rsgo.maintenance=ignore` are not stopped/started.
This is useful for infrastructure containers (e.g., databases) that should continue running during maintenance.

Example in docker-compose.yml:
```yaml
services:
  postgres:
    image: postgres:16
    labels:
      rsgo.stack: my-app
      rsgo.maintenance: ignore  # Container stays active during Maintenance
```

Container lifecycle management is done via Docker API and coordinated by the `ChangeOperationModeHandler`.

---

## 6. API

### 6.1 Health Endpoints in RSGO Core

- `GET /api/orgs/{orgId}/envs/{envId}/stacks/{stackId}/health`
  Response: `HealthSnapshot` as JSON

- `GET /api/orgs/{orgId}/envs/{envId}/health-summary`
  Response: List of all stacks + `overall` + `operationMode`

- Optional:
  `GET /api/orgs/{orgId}/envs/{envId}/stacks/{stackId}/health/history`
  → Last X snapshots to see trends/outages.

---

## 7. UI Behavior

### 7.1 Org / Env Overview

Example display for an organization:

- `Org A`
  - `Test` – 🟢 **Healthy** (2 stacks, all Healthy)
  - `Prod` – 🟠 **Degraded – Migration running (ams-project 0.4.2 → 0.5.0)**

### 7.2 Environment Detail

Table:

| Stack        | Overall                    | Mode        | Bus        | Infra             | Self               |
|--------------|----------------------------|-------------|------------|-------------------|--------------------|
| identity     | Healthy                    | Normal      | Healthy    | –                 | 3/3 Services ok    |
| ams-project  | Degraded (Migration 0.5.0) | Migrating   | Unhealthy  | DB: ERP busy      | 9/10 Services ok   |
| monitoring   | Healthy                    | Normal      | –          | –                 | 2/2 Services ok    |

### 7.3 Stack Detail

Header:

> 🟠 **Degraded – Migration running (0.4.2 → 0.5.0)**
> Step 2/4: Database migration.

Below that, tabs:

- Overview (Bus/Infra/Self summarized)
- Services (Container status, Health endpoints)
- Bus (NSB endpoints & TransportKeys)
- Infra (DB, Disk, external services)

---

## 8. Third-Party Applications (not developed by you)

### 8.1 Minimal Case: Only Docker Status

For external stacks that are just "regular" containers:

- Manifest contains only services with image/ports etc.
- No special health URLs, no NSB, no DB checks.
- RSGO can still:

  - Query container state (running, restarting, exited)
  - Derive `SelfHealth` from it
  - Calculate `overall` (from Self + OperationMode)

→ For these stacks, RSGO at least shows:

- "Are the containers running?"
- "Are we in Maintenance/Stopped?"
- Possibly `Unknown`, if no info available.

### 8.2 HTTP Health Endpoints from Third Parties

Many products already have HTTP health:

- Keycloak: e.g., `/health` or product-specific endpoints
- Databases via Admin API
- External web apps with `/health` or `/status`

In the manifest, you can also define `health` blocks for third-party services:

```yaml
services:
  - name: keycloak
    image: quay.io/keycloak/keycloak:latest
    health:
      type: http
      url: http://keycloak:8080/health
      timeout: 5s
```

RSGO treats them the same as your own services – just without NSB/bus specifics.

### 8.3 OperationMode for Third Parties

Since RSGO **does not migrate** external products itself (e.g., complex SAP/ERP applications):

- `OperationMode = Migrating` is usually **only set** if:
  - RSGO knows an **own** upgrade/migration workflow for this stack
  - **or** the admin manually puts the stack into a maintenance/migration mode.

You can offer for example:

- Button: "Put Stack in Maintenance"
  - `OperationMode = Maintenance`
  - optional: RSGO automatically stops certain services

This way you can also clearly signal for external stacks:
- "Degraded (Maintenance)" instead of "Unhealthy".

### 8.4 Integration Adapters (optional, later)

For important external products, you can later offer "adapter manifest extensions":

- Special health definitions:
  - `kind: "keycloak"` → RSGO knows where to check sensibly.
  - `kind: "prometheus"` → specific status endpoints.
- Possibly also simple lifecycle commands:
  - e.g., "Reload config", "Restart gracefully".

But this is not necessary for the first version – basic health functionality already works with:

- Docker status
- optional HTTP health URLs

---

## 9. Summary

- **RSGO controls the operation mode ("OperationMode")**:
  - Normal, Migrating, Maintenance, Stopped, Failed
- **Containers/Services provide technical health data**:
  - via Docker status, HTTP health, NSB EndpointHealth, etc.
- **Overall health** is a combination of:
  - OperationMode + Bus + Infra + Self
- **Third-party stacks**:
  - work at least with Docker status
  - can be integrated via optional HTTP health
  - can be manually put into Maintenance
- **First-party stacks**:
  - additionally use BusHealth (NServiceBus + WatchDog)
  - can run migrations/upgrades fully automated in RSGO mode "Migrating".

This concept allows:
- a simple, clear UX ("green/yellow/red + Migration/Maintenance"),
- clean integration of your existing EndpointHealth/WatchDog ideas,
- and a sensible health display even for stacks that don't come from you.
