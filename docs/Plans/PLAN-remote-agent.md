# Phase: Remote Agent Environment (Phase 3–4 — Remote Environments)

## Ziel

RSGO-zu-RSGO Konnektivität über ein **Pull-Modell**: Jede RSGO-Installation kann als Agent fungieren und sich beim Controller registrieren. Der Agent pollt periodisch nach Befehlen und meldet Health-Daten + Command-Ergebnisse zurück.

### Übersicht Environment-Typen

| # | Typ | Remote braucht | Wer initiiert | Phase |
|---|-----|---------------|--------------|-------|
| 0 | **DockerSocket** | Nichts (lokal) | — | Implementiert |
| 1 | **SshTunnel** | Nur SSH-Zugang | Controller → SSH → Docker | Separater Plan |
| 2 | **DockerTcp** | Exponierter Docker Port + TLS | Controller → Docker API | Separater Plan |
| 3 | **RemoteAgent** | RSGO-Installation | Agent → Controller (Pull) | **Dieser Plan** |

## Analyse / Voraussetzungen

- **Abhängig von Phase 1 (SSH Tunnel)**: ConnectionConfig Polymorphismus muss bereits implementiert sein
- API Key Auth bereits implementiert mit `env_id` Claim — kann für Agent-Secret wiederverwendet werden
- `EnvironmentType.DockerAgent = 2` existiert als Platzhalter
- Portainer Edge Agent Pattern als Vorbild: Agent pollt Controller, bekommt Pending Commands zurück

---

## Architektur

```
┌─────────────────────────┐                              ┌─────────────────────────┐
│    RSGO Controller      │                              │     RSGO Agent          │
│    (UI, Management)     │                              │     (Remote Host)       │
│                         │                              │                         │
│  ┌───────────────────┐  │   Agent pollt periodisch     │  ┌───────────────────┐  │
│  │ Agent Registry    │  │ ◄──── GET /api/agent/checkin │  │ Agent Service     │  │
│  │ - AgentId         │  │                              │  │ - ControllerUrl   │  │
│  │ - Status          │  │ ──── Pending Commands ─────► │  │ - AgentSecret     │  │
│  │ - LastCheckin     │  │      (Deploy, Stop, ...)     │  │ - CheckinInterval │  │
│  │ - Capabilities    │  │                              │  └───────────────────┘  │
│  └───────────────────┘  │                              │                         │
│                         │   Agent reported Ergebnisse   │  ┌───────────────────┐  │
│  ┌───────────────────┐  │ ◄──── POST /api/agent/report │  │ Local Docker      │  │
│  │ Command Queue     │  │      (Health, Deploy-Status,  │  │ Operations        │  │
│  │ - Pending         │  │       Logs, Containers)       │  │ via Socket        │  │
│  │ - InProgress      │  │                              │  └───────────────────┘  │
│  │ - Completed       │  │                              │                         │
│  └───────────────────┘  │                              │                         │
└─────────────────────────┘                              └─────────────────────────┘
```

**Vorteile:**
- **Firewall-freundlich**: Agent macht nur Outbound-Requests (kein Port auf Agent nötig)
- **NAT-kompatibel**: Agent kann hinter NAT sitzen
- **Selbst-registrierend**: Agent meldet sich beim Controller an
- **Offline-tolerant**: Wenn Agent nicht pollt → nach Timeout als offline markiert
- **Bidirektional**: Agent pushed Health + Status aktiv zum Controller

---

## Kommunikationsprotokoll

### Check-in (Agent → Controller, periodisch)

```
GET /api/agent/checkin
Headers:
  Authorization: Bearer <agent-secret>
  X-Agent-Id: <agent-id>
  X-Agent-Version: <rsgo-version>

Response 200:
{
  "checkinInterval": 30,
  "pendingCommands": [
    {
      "commandId": "cmd-123",
      "type": "DeployStack",
      "payload": { ... vollständige Stack-Definition ... }
    }
  ]
}
```

### Report (Agent → Controller, nach Command-Ausführung + periodisch Health)

```
POST /api/agent/report
Headers:
  Authorization: Bearer <agent-secret>
  X-Agent-Id: <agent-id>

Body:
{
  "commandResults": [
    { "commandId": "cmd-123", "status": "Completed", "result": { ... } }
  ],
  "health": {
    "deployments": [
      {
        "deploymentId": "...",
        "status": "Running",
        "services": [
          { "name": "frontend", "healthy": true, "containerState": "running" }
        ]
      }
    ],
    "dockerInfo": { "version": "24.0.7", "containers": 12, "containersRunning": 10 }
  }
}
```

### Agent-Registrierung (einmalig)

```
POST /api/agent/register
Headers:
  Authorization: Bearer <agent-secret>

Body:
{
  "name": "Edge Server Berlin",
  "capabilities": ["docker"],
  "rsgoVersion": "0.49.0",
  "dockerVersion": "24.0.7",
  "os": "linux",
  "arch": "amd64"
}

Response 200:
{ "agentId": "ag-abc123", "environmentId": "env-xyz789", "checkinInterval": 30 }
```

### Command-Typen

| Command | Payload | Agent-Aktion |
|---------|---------|-------------|
| `DeployStack` | Vollständige Stack-Definition + Variablen | Stack lokal deployen via Docker |
| `StopContainers` | DeploymentId | Container stoppen |
| `StartContainers` | DeploymentId | Container starten |
| `RestartContainers` | DeploymentId | Container neustarten |
| `RemoveDeployment` | DeploymentId | Stack + Container entfernen |
| `ChangeOperationMode` | DeploymentId + Mode | Maintenance ein/aus |
| `GetLogs` | ContainerId + Tail | Container Logs zurückmelden |

### Offline-Erkennung (Portainer-Formel)

```
isOnline = (Now - LastCheckinUtc) <= (CheckinInterval × 2) + 20s
```

| CheckinInterval | Offline nach |
|-----------------|-------------|
| 5s | 30s |
| 30s (Default) | 80s |
| 60s | 140s |

---

## Phase 3 — Domain & Infrastruktur

### Features

- [ ] **Feature 1: Agent Domain Model**
  - Neues Aggregate: `Agent` mit `AgentId`, `Name`, `SecretHash`, `Status` (Online/Offline/Pending), `LastCheckinUtc`, `RsgoVersion`, `DockerVersion`, `Capabilities`
  - Value Object: `AgentSecret` (Generierung + Hashing, analog API Key)
  - Domain Events: `AgentRegistered`, `AgentCheckedIn`, `AgentWentOffline`
  - Zuordnung: `Environment` bekommt optionale `AgentId` Referenz
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Agents/Agent.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/Agents/AgentId.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/Agents/AgentSecret.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/Agents/AgentStatus.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/Agents/IAgentRepository.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/Environments/Environment.cs` (AgentId Feld)

- [ ] **Feature 2: Command Queue**
  - Neues Entity: `AgentCommand` mit `CommandId`, `AgentId`, `Type`, `Payload` (JSON), `Status` (Pending/InProgress/Completed/Failed), `CreatedAtUtc`, `CompletedAtUtc`, `Result` (JSON)
  - Repository: `IAgentCommandRepository` mit `GetPendingByAgent(agentId)`, `MarkInProgress()`, `MarkCompleted()`
  - EF Core Konfiguration + SQLite-Tabelle
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Agents/AgentCommand.cs` (neu)
    - `src/ReadyStackGo.Domain/Deployment/Agents/AgentCommandType.cs` (neu)
    - `src/ReadyStackGo.Infrastructure.DataAccess/Configurations/AgentConfiguration.cs` (neu)
    - `src/ReadyStackGo.Infrastructure.DataAccess/Configurations/AgentCommandConfiguration.cs` (neu)
    - `src/ReadyStackGo.Infrastructure.DataAccess/Repositories/AgentRepository.cs` (neu)
    - `src/ReadyStackGo.Infrastructure.DataAccess/Repositories/AgentCommandRepository.cs` (neu)

- [ ] **Feature 3: Controller-Endpoints**
  - `POST /api/agent/register` — Agent registriert sich, bekommt AgentId + EnvironmentId
  - `GET /api/agent/checkin` — Agent pollt Pending Commands, Controller updated LastCheckin
  - `POST /api/agent/report` — Agent meldet Command-Results + Health-Daten
  - Auth: Agent-Secret als Bearer Token (bestehender API Key Scheme)
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/Endpoints/Agent/RegisterEndpoint.cs` (neu)
    - `src/ReadyStackGo.Api/Endpoints/Agent/CheckinEndpoint.cs` (neu)
    - `src/ReadyStackGo.Api/Endpoints/Agent/ReportEndpoint.cs` (neu)
    - `src/ReadyStackGo.Application/UseCases/Agents/RegisterAgent/` (neu)
    - `src/ReadyStackGo.Application/UseCases/Agents/AgentCheckin/` (neu)
    - `src/ReadyStackGo.Application/UseCases/Agents/AgentReport/` (neu)

- [ ] **Feature 4: Agent Background Service**
  - `AgentBackgroundService` im Agent-RSGO — periodischer Check-in Loop
  - Konfiguration: `RSGO_CONTROLLER_URL`, `RSGO_AGENT_SECRET` (Environment Variables oder Settings UI)
  - Ablauf: Check-in → Pending Commands → lokal ausführen → Report senden
  - Command Execution: Nutzt bestehende `IDockerService` + `IDeploymentService` lokal
  - Auto-Register beim ersten Check-in
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/BackgroundServices/AgentBackgroundService.cs` (neu)
    - `src/ReadyStackGo.Application/Services/IAgentService.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/AgentService.cs` (neu)

- [ ] **Feature 5: Deployment-Routing (Controller-Seite)**
  - Wenn User auf Remote Agent Environment deployed → Command in Queue statt direkter Docker-Call
  - Strategy Pattern: `IDeploymentStrategy` mit `LocalDeploymentStrategy` und `RemoteAgentStrategy`
  - Health Aggregation: Agent-reported Health in Controller-DB, für UI anzeigen
  - Offline-Detection: Background Service prüft `LastCheckinUtc` gegen Portainer-Formel
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Services/IDeploymentStrategy.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/LocalDeploymentStrategy.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/RemoteAgentStrategy.cs` (neu)
    - `src/ReadyStackGo.Api/BackgroundServices/AgentMonitorBackgroundService.cs` (neu)

- [ ] **Feature 6: Unit Tests Phase 3**
  - Agent Domain Model (Creation, Registration, Checkin, Status Transitions, Offline Detection)
  - Command Queue (Lifecycle: Pending → InProgress → Completed/Failed)
  - AgentSecret (Generation, Hashing, Verification)
  - Deployment Routing (Local vs Remote Agent Strategy)
  - Controller Endpoint Handlers (Register, Checkin, Report)

---

## Phase 4 — UI

### Features

- [ ] **Feature 7: Agent Management UI (Controller)**
  - Neue Seite: Settings → Agents
  - Agent-Secret generieren (einmalig anzeigen, dann nur Hash gespeichert)
  - Agent-Liste: Name, Status (Online/Offline), Last Checkin, Version, zugeordnetes Environment
  - Agent-Detail: Command History, Health-Daten, Capabilities
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/core/src/api/agents.ts` (neu)
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useAgentStore.ts` (neu)
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Agents/` (neu)

- [ ] **Feature 8: Environment UI erweitern**
  - Environments-Liste: Type-Badge (Local / SSH / TCP / Remote Agent)
  - Remote Agent Environments zeigen Agent-Status (Online/Offline Indicator)
  - Deployment auf Remote Agent zeigt "Queued → In Progress → Completed" States
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/Environments.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/AddEnvironment.tsx`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useEnvironmentStore.ts`

- [ ] **Feature 9: Agent-Konfiguration UI (Agent-Seite)**
  - Settings-Seite auf dem Agent-RSGO: Controller URL + Secret eingeben
  - Oder: Environment Variables `RSGO_CONTROLLER_URL` + `RSGO_AGENT_SECRET`
  - Status-Anzeige: Verbunden / Nicht verbunden / Auth-Fehler
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/AgentSettings.tsx` (neu)

- [ ] **Feature 10: Unit Tests Phase 4**
  - Agent Management Handlers (Create Secret, List, Delete)
  - Environment-Agent Zuordnung

- [ ] **Dokumentation & Website** — Bilingual Docs (DE/EN) mit Screenshots
- [ ] **Phase abschließen** – Integration PR gegen main

---

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Agent-Architektur | Separater Agent-Binary, RSGO als Agent | RSGO als Agent | Kein separater Binary nötig, RSGO hat bereits API + Auth + Docker-Zugriff |
| Kommunikationsmodell | Controller → Agent (Push), Agent → Controller (Pull) | Agent → Controller (Pull) | Firewall-freundlich, NAT-kompatibel, kein Port auf Agent nötig. Portainer Edge Agent Pattern. |
| Agent-Secret-Speicherung | Klartext, Hash | Gehasht (wie API Key) | Agent sendet Secret als Bearer Token. Controller verifiziert gegen Hash. Klartext nur einmalig bei Erstellung angezeigt. |
| Deployment-Orchestrierung | Controller sendet Befehl, Agent hat eigenen Katalog | Controller sendet fertigen Befehl via Command Queue | Stack-Definition vollständig im DeployStack-Command. Agent braucht keinen eigenen Katalog. |
| Health Polling | Gleich wie lokal, Konfigurierbar | Konfigurierbar pro Agent (Default 30s) | Portainer-Pattern: Pro-Agent Intervall. Offline-Erkennung: `(Intervall × 2) + 20s`. |
| Offline-Erkennung | Eigene Formel, Portainer-Formel | Portainer-Formel: `(interval × 2) + 20s` | Bewährt. Bei 30s Default → 80s bis offline. |
| Agent-Registrierung | Manuell, Self-Register | Self-Register beim ersten Checkin | Agent bekommt Secret + Controller-URL, registriert sich selbst. Environment wird automatisch erstellt. |
