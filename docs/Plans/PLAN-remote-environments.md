# Phase: Remote Environments

## Ziel

Docker-Hosts auf entfernten Servern als Deployment-Ziele nutzen — über vier Environment-Typen mit unterschiedlichen Anforderungen und Trade-offs. Zusätzlich kann jede RSGO-Installation als **Agent** fungieren (Pull-Modell), um Deployments auf Remote-Hosts zu orchestrieren.

### Übersicht Environment-Typen

| # | Typ | Remote braucht | Wer initiiert | Phase |
|---|-----|---------------|--------------|-------|
| 0 | **DockerSocket** | Nichts (lokal) | — | Implementiert |
| 1 | **SshTunnel** | Nur SSH-Zugang | Controller → SSH → Docker | **Phase 1** |
| 2 | **DockerTcp** | Exponierter Docker Port + TLS | Controller → Docker API | Phase 2 |
| 3 | **RemoteAgent** | RSGO-Installation | Agent → Controller (Pull) | Phase 3–4 |

## Analyse

### Ist-Zustand

- `Environment` Aggregate Root mit `EnvironmentType` Enum: `DockerSocket = 0`, `DockerApi = 1`, `DockerAgent = 2`
- `ConnectionConfig` Value Object: enthält nur `SocketPath` (String)
- `DockerService.GetDockerClientAsync()`: Lädt Environment, parst Socket Path, cached `DockerClient` pro EnvironmentId
- `DockerService.ParseDockerUri()`: Unterstützt `unix://`, `npipe://`, `tcp://` — TCP ist geparst aber nicht produktiv genutzt
- Multi-Environment Spec (`docs/Reference/Multi-Environment.md`) definiert `DockerApiEnvironment` und `DockerAgentEnvironment` als v0.5+ Platzhalter
- Alle Deployments, Health Checks, Container-Operationen sind bereits environment-scoped
- Frontend: `AddEnvironment.tsx` zeigt nur Socket Path Feld, kein Typ-Selektor
- API Key Auth bereits implementiert mit `env_id` Claim

### Pattern-Vorbilder

- Value Object: `ConnectionConfig.cs` — sealed class mit Factory Methods, `ValueObject` Basisklasse
- Aggregate Root: `Environment.cs` — Factory Methods `CreateDockerSocket()`, `CreateDefault()`
- EF Config: `EnvironmentConfiguration.cs` — Owned Type für `ConnectionConfig`, Unique Index auf `(OrgId, Name)`
- Endpoint: `TestConnectionEndpoint.cs` — POST `/api/environments/test-connection` mit raw Docker Host URL
- Service: `DockerService.cs` — `ConcurrentDictionary<string, DockerClient>` Client-Cache
- Portainer Edge Agent: Agent pollt Controller `/endpoints/{id}/edge/status`, bekommt Pending Commands zurück

---

## Phase 1 — SSH Tunnel (Controller → Remote Docker via SSH)

Für Remote-Hosts **ohne RSGO-Installation** und **ohne exponierten Docker-Port**. Nur SSH-Zugang nötig — der häufigste Fall.

### Architektur

```
┌─────────────────────────┐                              ┌─────────────────────────┐
│    RSGO Controller      │         SSH Tunnel            │     Remote Host         │
│                         │                              │                         │
│  ┌───────────────────┐  │   localhost:random-port       │  ┌───────────────────┐  │
│  │ SshTunnelManager  │──│──────── SSH (Port 22) ──────►│  │ /var/run/docker   │  │
│  │ (SSH.NET)         │  │                              │  │ .sock             │  │
│  └────────┬──────────┘  │                              │  └───────────────────┘  │
│           │              │                              │                         │
│  ┌────────▼──────────┐  │                              │  ┌───────────────────┐  │
│  │ DockerService     │  │   tcp://localhost:random      │  │ Docker Daemon     │  │
│  │ (Docker.DotNet)   │──│──► durch SSH Tunnel ────────►│  │                   │  │
│  └───────────────────┘  │                              │  └───────────────────┘  │
└─────────────────────────┘                              └─────────────────────────┘
```

**Flow:**
1. User erstellt Environment Typ "SSH Tunnel" mit Host, Username, SSH Key
2. RSGO baut SSH-Tunnel auf: `localhost:random-port` → `remote:/var/run/docker.sock`
3. Docker.DotNet verbindet sich via `tcp://localhost:random-port` durch den Tunnel
4. Alle Docker-Operationen (Deploy, Health, Logs) laufen transparent durch den Tunnel

### Features

- [ ] **Feature 1: ConnectionConfig polymorph machen**
  - `ConnectionConfig` von einzelnem `SocketPath` zu polymorphem Value Object erweitern
  - Neuer Subtyp: `SshTunnelConfig` (Host, Port, Username, AuthMethod, RemoteSocketPath)
  - Bestehender Subtyp: `DockerSocketConfig` (SocketPath — Wrapper um bestehende Daten)
  - EF Core: JSON-Column für ConnectionConfig (statt Owned Type mit einzelner Column)
  - Migration: bestehende SocketPath-Daten → `DockerSocketConfig` JSON
  - `EnvironmentType` Enum erweitern: `SshTunnel = 3`
  - `Environment` Factory Method: `CreateSshTunnel(id, orgId, name, description, sshConfig)`
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Environments/ConnectionConfig.cs`
    - `src/ReadyStackGo.Domain/Deployment/Environments/EnvironmentType.cs`
    - `src/ReadyStackGo.Domain/Deployment/Environments/Environment.cs`
    - `src/ReadyStackGo.Infrastructure.DataAccess/Configurations/EnvironmentConfiguration.cs`

- [ ] **Feature 2: SSH Credential Storage**
  - Value Object: `SshCredential` mit AuthMethod (PrivateKey / Password), verschlüsseltem Secret
  - `CredentialEncryptionService`: AES-Verschlüsselung für SSH Keys/Passwords (reversibel, da Controller den Key für jeden Tunnel-Aufbau braucht)
  - Master Key aus Environment Variable `RSGO_ENCRYPTION_KEY` oder auto-generiert in Config
  - SSH Known Hosts: Auto-Accept mit Fingerprint-Speicherung beim ersten Connect
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Environments/SshCredential.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/CredentialEncryptionService.cs` (neu)

- [ ] **Feature 3: SshTunnelManager**
  - NuGet Package: `SSH.NET`
  - Service: `SshTunnelManager` — baut SSH-Tunnel auf, verwaltet Lifecycle
  - Local Port Forwarding: `localhost:random-port` → `remote:RemoteSocketPath`
  - Tunnel-Pool: `ConcurrentDictionary<EnvironmentId, SshTunnel>` (analog DockerClient Cache)
  - Auto-Reconnect bei Tunnel-Abbruch (Retry mit Backoff)
  - Tunnel wird bei erstem Zugriff aufgebaut (Lazy), nach Inaktivität abgebaut
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/SshTunnelManager.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/ISshTunnelManager.cs` (neu)

- [ ] **Feature 4: DockerService SSH-Routing**
  - `DockerService.GetDockerClientAsync()` erweitern: Wenn EnvironmentType == SshTunnel → Tunnel via SshTunnelManager aufbauen → DockerClient auf `tcp://localhost:tunnel-port`
  - Test Connection: Tunnel aufbauen → Docker System Info → Tunnel abbauen
  - Health Collector: Funktioniert transparent — Docker-Operationen gehen durch den Tunnel
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Docker/DockerService.cs`
    - `src/ReadyStackGo.Application/UseCases/Environments/TestConnection/TestConnectionHandler.cs`

- [ ] **Feature 5: SSH Environment API + UI**
  - `CreateEnvironmentCommand` erweitern: Type-Discriminator + SSH-spezifische Felder
  - Neuer Environment-Typ "SSH Tunnel" im AddEnvironment Type-Selector
  - Dynamisches Formular: SSH Host, Port (Default 22), Username, Auth Method (Key/Password), Private Key Textarea oder File Upload, Remote Socket Path (Default `/var/run/docker.sock`)
  - Test Connection Button: Baut Tunnel auf, testet Docker, zeigt Version
  - Environments-Liste: Type-Badge (Local / SSH)
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Environments/CreateEnvironment/`
    - `src/ReadyStackGo.Application/UseCases/Environments/UpdateEnvironment/`
    - `src/ReadyStackGo.Application/UseCases/Environments/TestConnection/`
    - `src/ReadyStackGo.Application/UseCases/Environments/EnvironmentDtos.cs`
    - `src/ReadyStackGo.Api/Endpoints/Environments/`
    - `src/ReadyStackGo.WebUi/packages/core/src/api/environments.ts`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useEnvironmentStore.ts`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/AddEnvironment.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/Environments.tsx`

- [ ] **Feature 6: Unit Tests Phase 1**
  - ConnectionConfig Polymorphismus (Serialisierung, Equality, Validation, Migration)
  - SshTunnelConfig Validation (Host required, Port 1–65535, Auth method required)
  - CredentialEncryptionService (AES Encrypt/Decrypt roundtrip, key rotation)
  - SshTunnelManager (Mock SSH.NET, Tunnel Lifecycle, Reconnect, Pool)
  - DockerService SSH-Routing (Environment Type → Tunnel → Docker Client)
  - Environment Creation mit SshTunnel Type

---

## Phase 2 — Docker TCP/TLS

Für managed Docker Hosts mit exponiertem Docker API Port. Kein RSGO und kein SSH nötig.

### Features

- [ ] **Feature 7: DockerTcpConfig + TLS Credentials**
  - ConnectionConfig Subtyp: `DockerTcpConfig` (ApiUrl, UseTls, TlsCertPath, TlsKeyPath, TlsCaPath)
  - `EnvironmentType.DockerTcp = 1` (überschreibt bestehenden `DockerApi` Platzhalter)
  - TLS Zertifikat-Upload + AES-verschlüsselte Speicherung (wie SSH Keys)
  - `Environment.CreateDockerTcp(id, orgId, name, description, tcpConfig)`
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Environments/ConnectionConfig.cs`
    - `src/ReadyStackGo.Domain/Deployment/Environments/Environment.cs`

- [ ] **Feature 8: Docker TCP/TLS Verbindung**
  - `DockerService.GetDockerClientAsync()` erweitern: TCP URI + TLS Credentials an `DockerClientConfiguration`
  - Test Connection für TCP/TLS (direkte Verbindung, kein Tunnel)
  - UI: Environment-Typ "Docker TCP" mit URL + optionalen TLS-Feldern (Cert Upload)
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Docker/DockerService.cs`
    - `src/ReadyStackGo.Api/Endpoints/Environments/`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/AddEnvironment.tsx`

- [ ] **Feature 9: Unit Tests Phase 2**
  - DockerTcpConfig Validation (URL format, TLS fields)
  - Docker TCP Client Creation + TLS Certificate Handling
  - Test Connection via TCP

---

## Phase 3 — Remote Agent: Domain & Infrastruktur

RSGO-zu-RSGO Konnektivität. Agent-initiated Pull-Modell: Der Agent verbindet sich zum Controller.

### Architektur

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

### Kommunikationsprotokoll

#### Check-in (Agent → Controller, periodisch)

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

#### Report (Agent → Controller, nach Command-Ausführung + periodisch Health)

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

#### Agent-Registrierung (einmalig)

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

### Features

- [ ] **Feature 10: Agent Domain Model**
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

- [ ] **Feature 11: Command Queue**
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

- [ ] **Feature 12: Controller-Endpoints**
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

- [ ] **Feature 13: Agent Background Service**
  - `AgentBackgroundService` im Agent-RSGO — periodischer Check-in Loop
  - Konfiguration: `RSGO_CONTROLLER_URL`, `RSGO_AGENT_SECRET` (Environment Variables oder Settings UI)
  - Ablauf: Check-in → Pending Commands → lokal ausführen → Report senden
  - Command Execution: Nutzt bestehende `IDockerService` + `IDeploymentService` lokal
  - Auto-Register beim ersten Check-in
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/BackgroundServices/AgentBackgroundService.cs` (neu)
    - `src/ReadyStackGo.Application/Services/IAgentService.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/AgentService.cs` (neu)

- [ ] **Feature 14: Deployment-Routing (Controller-Seite)**
  - Wenn User auf Remote Agent Environment deployed → Command in Queue statt direkter Docker-Call
  - Strategy Pattern: `IDeploymentStrategy` mit `LocalDeploymentStrategy` und `RemoteAgentStrategy`
  - Health Aggregation: Agent-reported Health in Controller-DB, für UI anzeigen
  - Offline-Detection: Background Service prüft `LastCheckinUtc` gegen Portainer-Formel
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Services/IDeploymentStrategy.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/LocalDeploymentStrategy.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/RemoteAgentStrategy.cs` (neu)
    - `src/ReadyStackGo.Api/BackgroundServices/AgentMonitorBackgroundService.cs` (neu)

- [ ] **Feature 15: Unit Tests Phase 3**
  - Agent Domain Model (Creation, Registration, Checkin, Status Transitions, Offline Detection)
  - Command Queue (Lifecycle: Pending → InProgress → Completed/Failed)
  - AgentSecret (Generation, Hashing, Verification)
  - Deployment Routing (Local vs Remote Agent Strategy)
  - Controller Endpoint Handlers (Register, Checkin, Report)

---

## Phase 4 — Remote Agent: UI

- [ ] **Feature 16: Agent Management UI (Controller)**
  - Neue Seite: Settings → Agents
  - Agent-Secret generieren (einmalig anzeigen, dann nur Hash gespeichert)
  - Agent-Liste: Name, Status (Online/Offline), Last Checkin, Version, zugeordnetes Environment
  - Agent-Detail: Command History, Health-Daten, Capabilities
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/core/src/api/agents.ts` (neu)
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useAgentStore.ts` (neu)
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Agents/` (neu)

- [ ] **Feature 17: Environment UI erweitern**
  - Environments-Liste: Type-Badge (Local / SSH / TCP / Remote Agent)
  - Remote Agent Environments zeigen Agent-Status (Online/Offline Indicator)
  - Deployment auf Remote Agent zeigt "Queued → In Progress → Completed" States
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/Environments.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/AddEnvironment.tsx`
    - `src/ReadyStackGo.WebUi/packages/core/src/hooks/useEnvironmentStore.ts`

- [ ] **Feature 18: Agent-Konfiguration UI (Agent-Seite)**
  - Settings-Seite auf dem Agent-RSGO: Controller URL + Secret eingeben
  - Oder: Environment Variables `RSGO_CONTROLLER_URL` + `RSGO_AGENT_SECRET`
  - Status-Anzeige: Verbunden / Nicht verbunden / Auth-Fehler
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Settings/AgentSettings.tsx` (neu)

- [ ] **Feature 19: Unit Tests Phase 4**
  - Agent Management Handlers (Create Secret, List, Delete)
  - Environment-Agent Zuordnung

---

## Phase 5 — Abschluss

- [ ] **Dokumentation & Website** — Bilingual Docs (DE/EN) mit Screenshots für alle 4 Environment-Typen
- [ ] **E2E Tests** — SSH Tunnel Connection, Agent-Registrierung, Command-Queue
- [ ] **Phase abschließen** – Integration PR gegen main

## Offene Punkte

Alle geklärt (siehe Entscheidungen).

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Implementierungsreihenfolge | Agent zuerst, SSH zuerst, TCP zuerst | SSH Tunnel zuerst (Phase 1) | Häufigster Use Case — jeder Server hat SSH. Kein Agent nötig, kein exponierter Port. Geringste Infrastruktur-Anforderungen. |
| Agent-Architektur | Separater Agent-Binary, RSGO als Agent | RSGO als Agent | Kein separater Binary nötig, RSGO hat bereits API + Auth + Docker-Zugriff |
| Kommunikationsmodell (Agent) | Controller → Agent (Push), Agent → Controller (Pull) | Agent → Controller (Pull) | Firewall-freundlich, NAT-kompatibel, kein Port auf Agent nötig. Portainer Edge Agent Pattern. |
| SSH Tunnel | Eigener Typ (Phase 1), Nicht nötig | Eigener Typ (Phase 1) | Für Remote-Hosts ohne RSGO und ohne exponierten Docker-Port. Controller baut SSH-Tunnel auf, Docker.DotNet verbindet durch den Tunnel. |
| Docker TCP/TLS | Separater Typ (Phase 2), Nicht nötig | Separater Typ (Phase 2) | Für managed Docker Hosts ohne RSGO. Einfachster Remote-Typ (nur URL + TLS). |
| ConnectionConfig Speicherung | Separate Columns, JSON Column | JSON Column | Polymorph, konsistent mit MaintenanceObserverConfig Pattern |
| SSH Credential-Speicherung | Klartext, AES-verschlüsselt | AES-verschlüsselt | Controller braucht Klartext für jeden Tunnel-Aufbau, aber Key soll nicht im Klartext in DB liegen. Master Key aus `RSGO_ENCRYPTION_KEY` Environment Variable. |
| Agent-Secret-Speicherung | Klartext, Hash | Gehasht (wie API Key) | Agent sendet Secret als Bearer Token. Controller verifiziert gegen Hash. Klartext nur einmalig bei Erstellung angezeigt. |
| Deployment-Orchestrierung (Agent) | Controller sendet Befehl, Agent hat eigenen Katalog | Controller sendet fertigen Befehl via Command Queue | Stack-Definition vollständig im DeployStack-Command. Agent braucht keinen eigenen Katalog. |
| Health Polling (Agent) | Gleich wie lokal, Konfigurierbar | Konfigurierbar pro Agent (Default 30s) | Portainer-Pattern: Pro-Agent Intervall. Offline-Erkennung: `(Intervall × 2) + 20s`. |
| Offline-Erkennung | Eigene Formel, Portainer-Formel | Portainer-Formel: `(interval × 2) + 20s` | Bewährt. Bei 30s Default → 80s bis offline. |
| Agent-Registrierung | Manuell, Self-Register | Self-Register beim ersten Checkin | Agent bekommt Secret + Controller-URL, registriert sich selbst. Environment wird automatisch erstellt. |
