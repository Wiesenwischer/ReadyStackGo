# ReadyStackGo – Health & Operation Mode Specification

## 1. Ziele

Das Health-System von ReadyStackGo (RSGO) soll:

- Auf **Org / Environment / Stack**-Ebene schnell zeigen:
  - Läuft alles normal?
  - Ist etwas _geplant_ eingeschränkt (Migration / Wartung)?
  - Ist etwas _ungeplant_ kaputt?
- **Erste-Party-Stacks** (ams.project etc.) mit tiefer Integration (NServiceBus, eigene Health-Endpunkte) unterstützen.
- **Drittanbieter-Stacks** (Keycloak, Postgres, Fremdprodukte) sinnvoll aggregieren, auch wenn sie nur Docker-Status oder generische HTTP-Health liefern.

Wichtiger Grundsatz:

> **RSGO ist die Quelle der Wahrheit für den Betriebsmodus (Normal, Migration, Wartung).  
> Container liefern technische Zustände (up/down, Fehler, Bus-Status).**

---

## 2. Kernkonzepte

### 2.1 HealthStatus

Enum für den technischen Zustand:

- `Healthy`
- `Degraded`
- `Unhealthy`
- `Unknown`

Wird für:
- Overall-Status eines Stacks
- Bus
- Infra
- Self (Container/Services)
verwendet.

### 2.2 OperationMode

Enum für den _Betriebsmodus_ eines Stack-Deployments:

- `Normal`  
  → Normaler Betrieb, keine geplante Einschränkung.
- `Migrating`  
  → Geplante Migration/Upgrade läuft (z. B. DB-Migrationen, Stack-Upgrade).
- `Maintenance`  
  → Stack bewusst in Wartungsmodus versetzt (geplant).
- `Stopped`  
  → Stack absichtlich gestoppt (nicht verfügbar, aber kein Fehler).
- `Failed` (optional)  
  → Letzter Deploy/Upgrade/Migration fehlgeschlagen, manueller Eingriff nötig.

**Wichtig:**  
`OperationMode` wird **von RSGO gesteuert**, nicht von den Containern.

### 2.3 DeploymentStatus & MigrationStatus

Zusätzliche Zustände in der Deployment-Domain:

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

Diese Werte helfen, OperationMode konsistent zu setzen.

---

## 3. Health-Domain-Model

### 3.1 HealthSnapshot

RSGO erzeugt regelmäßig Health-Snapshots pro Org/Env/Stack:

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

    string? TargetVersion;       // z.B. "0.5.0" bei Upgrade
    string? CurrentVersion;      // z.B. "0.4.2"

    BusHealth? Bus;
    InfraHealth? Infra;
    SelfHealth Self;
}
```

### 3.2 BusHealth

Verwendet v. a. für NServiceBus-basierte Stacks (First-Party oder andere NSB-Apps):

```csharp
class BusHealth {
    HealthStatus Status;          // Healthy/Degraded/Unhealthy/Unknown

    string? TransportKey;         // z.B. "primary-sql"
    bool HasCriticalError;
    string? CriticalErrorMessage;

    DateTime? LastHealthPingProcessedUtc;
    TimeSpan? TimeSinceLastPing;
    TimeSpan? UnhealthyAfter;     // Config-Wert, ab wann "zu alt"

    // Optional: betroffene Endpunkte
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

Generische Infrastruktur-Checks (optional pro Stack):

```csharp
class InfraHealth {
    IReadOnlyList<DatabaseHealth> Databases;
    IReadOnlyList<DiskHealth> Disks;
    IReadOnlyList<ExternalServiceHealth> ExternalServices;
}

class DatabaseHealth {
    string Id;             // z.B. "ams_project_db"
    HealthStatus Status;
    int? LatencyMs;
    string? Error;
}

class DiskHealth {
    string Mount;          // z.B. "/"
    HealthStatus Status;
    double? FreePercent;
    string? Error;
}

class ExternalServiceHealth {
    string Id;             // z.B. "smtp"
    HealthStatus Status;
    string? Error;
}
```

### 3.4 SelfHealth

Zustand der vom Stack kontrollierten Container/Services:

```csharp
class SelfHealth {
    IReadOnlyList<ServiceHealth> Services;
}

class ServiceHealth {
    string Name;                // z.B. "ams-api"
    HealthStatus Status;        // Aus Container-/Health-Endpoint-Sicht
    string? ContainerId;
    string? Reason;             // "Restarting", "CrashLoop", "HealthCheckFailed"
    int? RestartCount;
}
```

---

## 4. Datenquelle & Integration

### 4.1 Container / Docker (Self)

- RSGO verbindet sich per Docker API mit dem Environment-Host:
  - Status von Containern (running, exited, restarting)
  - Restart-Counts, Exit-Codes
- Optional: HTTP-Health-URL aus Manifest:

```yaml
services:
  - name: ams-api
    image: ...
    health:
      type: http
      url: http://ams-api:8080/health
      timeout: 5s
```

RSGO pollt diese Endpoints und setzt `ServiceHealth.Status` entsprechend.

### 4.2 Bus (NServiceBus / EndpointHealth)

Für NServiceBus-basierte Anwendungen:

- Jeder Endpoint verwendet dein Paket `Wiesenwischer.NServiceBus.EndpointHealth` und einen ASP.NET Health-Endpoint (`/health`).
- Dieser liefert:
  - MessagePump-Status
  - `HasCriticalError`
  - `LastHealthPingProcessedUtc`
  - `TransportKey`

RSGO:

- kennt per Manifest, welche Services Endpoints sind und wo deren Health-URLs liegen
- ruft diese Health-Endpoints ab
- aggregiert pro `TransportKey` und Stack den Bus-Status.

Für **Nicht-NSB-Stacks**:

- `BusHealth` kann einfach `null` oder `Status=Unknown` sein
- oder du erlaubst generische „Messaging-Checks“ über Manifest (z. B. RabbitMQ HTTP API), ist aber optional/„Advanced“.

### 4.3 Infra (DB, Disk, externe Dienste)

- Für First-Party-Stacks kann das Manifest DB-/Service-Checks definieren:

```yaml
infra:
  databases:
    - id: ams_project_db
      connectionParam: DB_MAIN
  externalServices:
    - id: smtp
      url: smtp.example.com:587
```

- RSGO kann:
  - kurze Verbindungs-Pings zu DBs machen (konfigurierbar)
  - freien Speicherplatz über Agent/Host-Metrics abfragen
- Für Drittanbieter-Stacks:
  - kann InfraHealth leer bleiben oder nur generische Checks enthalten (z. B. „DB-Container läuft“).

---

## 5. Aggregationslogik

### 5.1 Overall-Status

Pseudo-Regel:

1. Wenn `OperationMode == Migrating` oder `Maintenance` oder `Stopped`:
   - `overall` mindestens `Degraded`
   - echte Fehler können `overall` auf `Unhealthy` heben (z. B. Migration fehlgeschlagen).
2. Wenn `OperationMode == Normal`:
   - `overall` ist das „Maximum“/„Schlechteste“ aus Bus/Infra/Self
     - hat einer `Unhealthy` → `overall = Unhealthy`
     - sonst wenn einer `Degraded` → `overall = Degraded`
     - sonst `overall = Healthy`
3. Wenn keine Daten verfügbar:
   - `overall = Unknown`

### 5.2 OperationMode wird von RSGO gesetzt

- Bei Deploy/Upgrade/Migration:
  - `OperationMode = Migrating`
  - `DeploymentStatus = Upgrading`
  - `MigrationStatus = Running`
- Bei geplanter Wartung:
  - `OperationMode = Maintenance`
- Bei bewusstem Stopp:
  - `OperationMode = Stopped`
- Bei fehlgeschlagenem Upgrade/Migration:
  - `OperationMode = Failed`
  - `DeploymentStatus = Failed`
  - `MigrationStatus = Failed`

Die Health-Engine liest immer zuerst `OperationMode` und interpretiert Container-Zustände im Kontext:

- `OperationMode=Normal` + viele kaputte Container → echte Störung.
- `OperationMode=Migrating` + Dienste down → erwartete Einschränkung (Degraded).

---

## 6. API

### 6.1 Health-Endpoints im RSGO-Core

- `GET /api/orgs/{orgId}/envs/{envId}/stacks/{stackId}/health`  
  Antwort: `HealthSnapshot` als JSON

- `GET /api/orgs/{orgId}/envs/{envId}/health-summary`  
  Antwort: Liste aller Stacks + `overall` + `operationMode`

- Optional:  
  `GET /api/orgs/{orgId}/envs/{envId}/stacks/{stackId}/health/history`  
  → Letzte X Snapshots, um Trends/Ausfälle zu sehen.

---

## 7. UI-Verhalten

### 7.1 Org / Env Übersicht

Beispielanzeige für eine Organisation:

- `Org A`  
  - `Test` – 🟢 **Healthy** (2 Stacks, alle Healthy)  
  - `Prod` – 🟠 **Degraded – Migration läuft (ams-project 0.4.2 → 0.5.0)**  

### 7.2 Environment-Detail

Tabelle:

| Stack        | Overall                    | Mode        | Bus        | Infra             | Self               |
|--------------|----------------------------|------------|-----------|-------------------|--------------------|
| identity     | Healthy                    | Normal     | Healthy   | –                 | 3/3 Services ok    |
| ams-project  | Degraded (Migration 0.5.0) | Migrating  | Unhealthy | DB: ERP busy      | 9/10 Services ok   |
| monitoring   | Healthy                    | Normal     | –         | –                 | 2/2 Services ok    |

### 7.3 Stack-Detail

Header:

> 🟠 **Degraded – Migration läuft (0.4.2 → 0.5.0)**  
> Schritt 2/4: Datenbankmigration.

Darunter Tabs:

- Overview (Bus/Infra/Self zusammengefasst)
- Services (Container-Status, Health-Endpoints)
- Bus (NSB-Endpunkte & TransportKeys)
- Infra (DB, Disk, externe Services)

---

## 8. Drittanbieter-Anwendungen (nicht von euch entwickelt)

### 8.1 Minimalfall: Nur Docker-Status

Für fremde Stacks, die nur „normale“ Container sind:

- Manifest enthält nur Services mit Image/Ports etc.
- Keine speziellen Health-URLs, kein NSB, keine DB-Checks.
- RSGO kann trotzdem:

  - Container-State abfragen (running, restarting, exited)
  - daraus `SelfHealth` ableiten
  - `overall` berechnen (aus Self + OperationMode)

→ Für diese Stacks zeigt RSGO zumindest:

- „Laufen die Container?“
- „Sind wir im Maintenance/Stopped?“
- Ggf. `Unknown`, wenn keine Info.

### 8.2 HTTP-Health-Endpoints von Drittanbietern

Viele Produkte haben bereits HTTP-Health:

- Keycloak: z. B. `/health` oder produkt-spezifische Endpunkte
- Datenbanken via Admin-API
- Fremd-Webapps mit `/health` oder `/status`

Im Manifest kannst du für Drittanbieter-Services auch `health`-Blöcke definieren:

```yaml
services:
  - name: keycloak
    image: quay.io/keycloak/keycloak:latest
    health:
      type: http
      url: http://keycloak:8080/health
      timeout: 5s
```

RSGO behandelt sie genauso wie eure eigenen Services – nur ohne NSB-/Bus-Spezifika.

### 8.3 OperationMode bei Drittanbietern

Da RSGO Fremdprodukte **nicht selbst migriert** (z. B. komplexe SAP-/ERP-Anwendungen):

- `OperationMode = Migrating` wird i. d. R. **nur gesetzt**, wenn:
  - RSGO einen **eigenen** Upgrade-/Migrations-Workflow für diesen Stack kennt  
  - **oder** der Admin den Stack manuell in einen Wartungs-/Migrationsmodus versetzt.

Du kannst z. B. anbieten:

- Button: „Stack in Maintenance versetzen“
  - `OperationMode = Maintenance`
  - optional: RSGO stoppt dazu automatisch bestimmte Services

So kannst du auch bei fremden Stacks sauber signalisieren:
- „Degraded (Wartung)“ statt „Unhealthy“.

### 8.4 Integrations-Adapter (optional, später)

Für wichtige Fremdprodukte kannst du später „Adapter-Manifest-Erweiterungen“ anbieten:

- Spezielle Health-Definitionen:
  - `kind: "keycloak"` → RSGO weiß, wo sinnvoll zu prüfen ist.
  - `kind: "prometheus"` → bestimmte Status-Endpunkte.
- Evtl. auch einfache Lifecycle-Commands:
  - z. B. „Reload config“, „Restart gracefully“.

Aber das ist nicht nötig für den ersten Wurf – grundlegende Health-Funktionalität funktioniert schon mit:

- Docker-Status
- optionalen HTTP-Health-URLs

---

## 9. Zusammenfassung

- **RSGO steuert den Betriebsmodus („OperationMode“)**:
  - Normal, Migrating, Maintenance, Stopped, Failed
- **Container/Services liefern technische Health-Daten**:
  - via Docker-Status, HTTP-Health, NSB-EndpointHealth, etc.
- **Overall-Health** ist eine Kombination aus:
  - OperationMode + Bus + Infra + Self
- **Drittanbieter-Stacks**:
  - funktionieren mindestens mit Docker-Status
  - können über optionale HTTP-Health integriert werden
  - können manuell in Maintenance gesetzt werden
- **Erste-Party-Stacks**:
  - nutzen zusätzlich BusHealth (NServiceBus + WatchDog)
  - können Migrationen/Upgrades voll automatisiert im RSGO-Modus „Migrating“ laufen lassen.

Dieses Konzept erlaubt:
- eine einfache, klare UX („grün/gelb/rot + Migration/Wartung“),
- saubere Integration deiner bestehenden EndpointHealth/WatchDog-Ideen,
- und eine sinnvolle Health-Anzeige auch für Stacks, die nicht von euch kommen.
