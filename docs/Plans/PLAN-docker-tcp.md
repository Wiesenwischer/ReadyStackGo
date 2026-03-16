<!-- GitHub Epic: #283 -->
# Phase: Docker TCP/TLS Environment (Phase 2 — Remote Environments)

## Ziel

Docker-Hosts mit exponiertem Docker API Port als Deployment-Ziele nutzen. Für managed Docker Hosts **ohne RSGO-Installation** und **ohne SSH-Zugang** — direkter TCP/TLS-Zugriff auf die Docker API.

### Übersicht Environment-Typen

| # | Typ | Remote braucht | Wer initiiert | Phase |
|---|-----|---------------|--------------|-------|
| 0 | **DockerSocket** | Nichts (lokal) | — | Implementiert |
| 1 | **SshTunnel** | Nur SSH-Zugang | Controller → SSH → Docker | Separater Plan |
| 2 | **DockerTcp** | Exponierter Docker Port + TLS | Controller → Docker API | **Dieser Plan** |
| 3 | **RemoteAgent** | RSGO-Installation | Agent → Controller (Pull) | Separater Plan |

## Analyse / Voraussetzungen

- **Abhängig von Phase 1 (SSH Tunnel)**: ConnectionConfig Polymorphismus (JSON Column, Subtypen) muss bereits implementiert sein
- `DockerService.ParseDockerUri()` unterstützt bereits `tcp://` — wird aber nicht produktiv genutzt
- `CredentialEncryptionService` aus Phase 1 kann für TLS-Zertifikat-Speicherung wiederverwendet werden
- `EnvironmentType.DockerApi = 1` existiert als Platzhalter — wird zu `DockerTcp` umbenannt

---

## Features

- [ ] **Feature 1: DockerTcpConfig + TLS Credentials**
  - ConnectionConfig Subtyp: `DockerTcpConfig` (ApiUrl, UseTls, TlsCertPath, TlsKeyPath, TlsCaPath)
  - `EnvironmentType.DockerTcp = 1` (überschreibt bestehenden `DockerApi` Platzhalter)
  - TLS Zertifikat-Upload + AES-verschlüsselte Speicherung (wie SSH Keys)
  - `Environment.CreateDockerTcp(id, orgId, name, description, tcpConfig)`
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/Deployment/Environments/ConnectionConfig.cs`
    - `src/ReadyStackGo.Domain/Deployment/Environments/Environment.cs`

- [ ] **Feature 2: Docker TCP/TLS Verbindung**
  - `DockerService.GetDockerClientAsync()` erweitern: TCP URI + TLS Credentials an `DockerClientConfiguration`
  - Test Connection für TCP/TLS (direkte Verbindung, kein Tunnel)
  - UI: Environment-Typ "Docker TCP" mit URL + optionalen TLS-Feldern (Cert Upload)
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Docker/DockerService.cs`
    - `src/ReadyStackGo.Api/Endpoints/Environments/`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Environments/AddEnvironment.tsx`

- [ ] **Feature 3: Unit Tests**
  - DockerTcpConfig Validation (URL format, TLS fields)
  - Docker TCP Client Creation + TLS Certificate Handling
  - Test Connection via TCP

- [ ] **Dokumentation & Website** — Bilingual Docs (DE/EN) mit Screenshots
- [ ] **Phase abschließen** – Integration PR gegen main

---

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Docker TCP/TLS | Separater Typ, Nicht nötig | Separater Typ | Für managed Docker Hosts ohne RSGO. Einfachster Remote-Typ (nur URL + TLS). |
| TLS Credential-Speicherung | Klartext, AES-verschlüsselt | AES-verschlüsselt | Wiederverwendung des `CredentialEncryptionService` aus Phase 1 (SSH). Konsistente Sicherheitsstrategie. |
