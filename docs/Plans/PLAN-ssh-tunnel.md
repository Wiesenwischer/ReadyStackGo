# Phase: SSH Tunnel Environment (Phase 1 — Remote Environments)

## Ziel

Docker-Hosts auf entfernten Servern als Deployment-Ziele nutzen — über SSH-Tunnel. Für Remote-Hosts **ohne RSGO-Installation** und **ohne exponierten Docker-Port**. Nur SSH-Zugang nötig — der häufigste Fall.

### Übersicht Environment-Typen

| # | Typ | Remote braucht | Wer initiiert | Phase |
|---|-----|---------------|--------------|-------|
| 0 | **DockerSocket** | Nichts (lokal) | — | Implementiert |
| 1 | **SshTunnel** | Nur SSH-Zugang | Controller → SSH → Docker | **Dieser Plan** |
| 2 | **DockerTcp** | Exponierter Docker Port + TLS | Controller → Docker API | Separater Plan |
| 3 | **RemoteAgent** | RSGO-Installation | Agent → Controller (Pull) | Separater Plan |

## Analyse

### Ist-Zustand

- `Environment` Aggregate Root mit `EnvironmentType` Enum: `DockerSocket = 0`, `DockerApi = 1`, `DockerAgent = 2`
- `ConnectionConfig` Value Object: enthält nur `SocketPath` (String)
- `DockerService.GetDockerClientAsync()`: Lädt Environment, parst Socket Path, cached `DockerClient` pro EnvironmentId
- `DockerService.ParseDockerUri()`: Unterstützt `unix://`, `npipe://`, `tcp://` — TCP ist geparst aber nicht produktiv genutzt
- Alle Deployments, Health Checks, Container-Operationen sind bereits environment-scoped
- Frontend: `AddEnvironment.tsx` zeigt nur Socket Path Feld, kein Typ-Selektor

### Pattern-Vorbilder

- Value Object: `ConnectionConfig.cs` — sealed class mit Factory Methods, `ValueObject` Basisklasse
- Aggregate Root: `Environment.cs` — Factory Methods `CreateDockerSocket()`, `CreateDefault()`
- EF Config: `EnvironmentConfiguration.cs` — Owned Type für `ConnectionConfig`, Unique Index auf `(OrgId, Name)`
- Endpoint: `TestConnectionEndpoint.cs` — POST `/api/environments/test-connection` mit raw Docker Host URL
- Service: `DockerService.cs` — `ConcurrentDictionary<string, DockerClient>` Client-Cache

---

## Architektur

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

---

## Features

- [x] **Feature 1: ConnectionConfig polymorph machen**
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

- [x] **Feature 2: SSH Credential Storage**
  - Value Object: `SshCredential` mit AuthMethod (PrivateKey / Password), verschlüsseltem Secret
  - `CredentialEncryptionService`: AES-Verschlüsselung für SSH Keys/Passwords (reversibel, da Controller den Key für jeden Tunnel-Aufbau braucht)
  - Master Key aus Environment Variable `RSGO_ENCRYPTION_KEY` oder auto-generiert in Config
  - SSH Known Hosts: Auto-Accept mit Fingerprint-Speicherung beim ersten Connect
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Environments/SshCredential.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/CredentialEncryptionService.cs` (neu)

- [x] **Feature 3: SshTunnelManager**
  - NuGet Package: `SSH.NET`
  - Service: `SshTunnelManager` — baut SSH-Tunnel auf, verwaltet Lifecycle
  - Local Port Forwarding: `localhost:random-port` → `remote:RemoteSocketPath`
  - Tunnel-Pool: `ConcurrentDictionary<EnvironmentId, SshTunnel>` (analog DockerClient Cache)
  - Auto-Reconnect bei Tunnel-Abbruch (Retry mit Backoff)
  - Tunnel wird bei erstem Zugriff aufgebaut (Lazy), nach Inaktivität abgebaut
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/SshTunnelManager.cs` (neu)
    - `src/ReadyStackGo.Infrastructure/Services/ISshTunnelManager.cs` (neu)

- [x] **Feature 4: DockerService SSH-Routing**
  - `DockerService.GetDockerClientAsync()` erweitern: Wenn EnvironmentType == SshTunnel → Tunnel via SshTunnelManager aufbauen → DockerClient auf `tcp://localhost:tunnel-port`
  - Test Connection: Tunnel aufbauen → Docker System Info → Tunnel abbauen
  - Health Collector: Funktioniert transparent — Docker-Operationen gehen durch den Tunnel
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Docker/DockerService.cs`
    - `src/ReadyStackGo.Application/UseCases/Environments/TestConnection/TestConnectionHandler.cs`

- [x] **Feature 5: SSH Environment API + UI**
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

- [x] **Feature 6: Unit Tests**
  - ConnectionConfig Polymorphismus (Serialisierung, Equality, Validation, Migration)
  - SshTunnelConfig Validation (Host required, Port 1–65535, Auth method required)
  - CredentialEncryptionService (AES Encrypt/Decrypt roundtrip, key rotation)
  - SshTunnelManager (Mock SSH.NET, Tunnel Lifecycle, Reconnect, Pool)
  - DockerService SSH-Routing (Environment Type → Tunnel → Docker Client)
  - Environment Creation mit SshTunnel Type

- [x] **Dokumentation & Website** — Bilingual Docs (DE/EN) mit Screenshots
- [x] **Phase abschließen** – Integration PR gegen main

---

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Implementierungsreihenfolge | Agent zuerst, SSH zuerst, TCP zuerst | SSH Tunnel zuerst | Häufigster Use Case — jeder Server hat SSH. Kein Agent nötig, kein exponierter Port. Geringste Infrastruktur-Anforderungen. |
| ConnectionConfig Speicherung | Separate Columns, JSON Column | JSON Column | Polymorph, konsistent mit MaintenanceObserverConfig Pattern |
| SSH Credential-Speicherung | Klartext, AES-verschlüsselt | AES-verschlüsselt | Controller braucht Klartext für jeden Tunnel-Aufbau, aber Key soll nicht im Klartext in DB liegen. Master Key aus `RSGO_ENCRYPTION_KEY` Environment Variable. |
