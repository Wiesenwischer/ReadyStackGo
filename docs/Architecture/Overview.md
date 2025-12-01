# ReadyStackGo Architecture

## Table of Contents
1. Vision & Goals
2. System Overview
3. High-Level Architecture Diagram
4. Layered Architecture
5. Request Flow
6. Deployment Engine
7. Wizard Engine
8. Security & TLS Architecture
9. Multi-Node & Future Scaling

---

## 1. Vision & Goals

ReadyStackGo ist eine selbst gehostete Plattform, die die Bereitstellung und Verwaltung komplexer Microservice-Stacks auf Basis von Docker extrem vereinfacht.
Statt direkter Arbeit mit `docker run`, Compose, Swarm oder Kubernetes erhalten Betreiber:

- einen **einzigen Admin-Container**,
- eine geführte **Wizard-Ersteinrichtung**,
- eine **Admin-Web-UI**,
- **Docker Compose-basierte Deployments** für komplette Stacks,
- klar strukturierte **Konfiguration** (SQLite + JSON).

Ziel: Einfache, wiederholbare, sichere Deployments in On-Prem- und isolierten Umgebungen.

---

## 2. System Overview

Auf hoher Ebene besteht ReadyStackGo aus:

- dem **Admin-Container** (ReadyStackGo selbst),
- dem **Docker-Host** (oder mehrere via Docker API),
- den **Stack-Containern** (Fachdomänen, BFFs, Gateways),
- einer **SQLite-Datenbank** für dynamische Daten,
- **JSON-Konfigurationsdateien** für statische Einstellungen,
- optional einem **Stack-Sources-Verzeichnis** (lokale Stacks, später Git).

---

## 3. High-Level Architecture Diagram

```mermaid
flowchart LR
    subgraph Host["Customer Host / Server"]
        subgraph Admin["ReadyStackGo Admin-Container"]
            API["API (FastEndpoints)"]
            UI["Web UI (React + Tailwind)"]
            WZ["Setup Wizard"]
            DE["Deployment Engine"]
            DB[("SQLite DB")]
            CFG["Config Store (JSON)"]
            TLS["TLS Engine"]
            SS["Stack Sources"]
        end

        DK[["Docker Engine
/var/run/docker.sock"]]

        subgraph Stack["Deployed Stack"]
            S1["service-1"]
            S2["service-2"]
            S3["service-3"]
        end
    end

    UI --> API
    WZ --> API
    API --> DE
    API --> DB
    API --> CFG
    API --> TLS
    API --> SS

    DE --> DK
    DK --> Stack
```

---

## 4. Layered Architecture

ReadyStackGo folgt einer **Clean Architecture** mit Domain-Driven Design (DDD):

```
┌────────────────────────────────────────────────────────────────┐
│                        API Layer                                │
│              (FastEndpoints, Controllers, DTOs)                 │
├────────────────────────────────────────────────────────────────┤
│                    Application Layer                            │
│         (UseCases, Commands, Queries, MediatR)                  │
├────────────────────────────────────────────────────────────────┤
│                      Domain Layer                               │
│     (Aggregates, Entities, Value Objects, Domain Services)      │
├────────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                          │
│  (EF Core, Repositories, Docker Service, External Services)     │
└────────────────────────────────────────────────────────────────┘
```

### Domain Layer (`ReadyStackGo.Domain`)

Enthält die Geschäftslogik, unabhängig von Infrastruktur:

- **SharedKernel**: AggregateRoot, Entity, ValueObject, DomainEvent
- **IdentityAccess**: Organization, User, Role (Bounded Context)
- **Deployment**: Environment, Deployment (Bounded Context)
- **StackManagement**: StackSource, StackDefinition (Bounded Context)

### Application Layer (`ReadyStackGo.Application`)

Orchestriert die Geschäftslogik über UseCases:

- **CQRS-Pattern** mit MediatR
- **UseCases**: Commands (zustandsändernd) und Queries (lesend)
- **Service Interfaces**: Abstraktion der Infrastruktur

### Infrastructure Layer (`ReadyStackGo.Infrastructure`)

Implementiert technische Belange:

- **DataAccess**: EF Core DbContext, Repositories, SQLite
- **Authentication**: JWT Token Service, BCrypt Password Hasher
- **Configuration**: JSON-basierter ConfigStore
- **Docker**: Docker Engine Integration via Docker.DotNet
- **Services**: DeploymentEngine, DeploymentService
- **Stacks**: Stack Source Provider, Cache

### API Layer (`ReadyStackGo.Api`)

HTTP-Schnittstelle:

- **FastEndpoints** für REST-APIs
- **Authentication/Authorization** Middleware
- **Request/Response** DTOs

---

## 5. Request Flow

### 5.1 High-Level Request Flow (Admin UI)

```mermaid
sequenceDiagram
    participant User as Admin User
    participant UI as RSGO Web UI
    participant API as RSGO API
    participant MED as MediatR
    participant APP as UseCase Handler
    participant INF as Infrastructure Services

    User->>UI: Klickt auf "Liste Container"
    UI->>API: GET /api/v1/containers
    API->>MED: Send(ListContainersQuery)
    MED->>APP: Handle(ListContainersQuery)
    APP->>INF: dockerService.ListAsync()
    INF-->>APP: ContainerInfo[]
    APP-->>MED: Ergebnis
    MED-->>API: ContainerInfo[]
    API-->>UI: JSON-Response
    UI-->>User: Tabelle mit Containern
```

---

## 6. Deployment Engine

### 6.1 Deployment Flow

```mermaid
sequenceDiagram
    participant Admin as Admin UI
    participant API as RSGO API
    participant MED as MediatR
    participant SVC as DeploymentService
    participant ENG as DeploymentEngine
    participant DK as Docker Engine

    Admin->>API: POST /api/v1/deployments
    API->>MED: Send(DeployComposeCommand)
    MED->>SVC: Handle(DeployComposeCommand)
    SVC->>ENG: GenerateDeploymentPlanAsync()
    ENG-->>SVC: DeploymentPlan
    SVC->>ENG: ExecuteDeploymentAsync(plan)
    loop for each service
        ENG->>DK: Pull Image
        ENG->>DK: Create Container
        ENG->>DK: Start Container
        DK-->>ENG: OK/Fehler
    end
    ENG-->>SVC: DeploymentResult
    SVC-->>MED: Result
    MED-->>API: DeploymentResult
    API-->>Admin: success / errorCode
```

### 6.2 DeploymentPlan Struktur

Der DeploymentEngine generiert einen Plan basierend auf:

- Docker Compose YAML (geparst)
- Environment-Variablen aus Feature-Flags
- Organization-spezifische Einstellungen
- Service-Abhängigkeiten (depends_on)

```csharp
public class DeploymentPlan
{
    public string StackVersion { get; set; }
    public string? EnvironmentId { get; set; }
    public string? StackName { get; set; }
    public Dictionary<string, string> GlobalEnvVars { get; set; }
    public List<DeploymentStep> Steps { get; set; }
    public Dictionary<string, NetworkDefinition> Networks { get; set; }
}
```

---

## 7. Wizard Engine

### 7.1 Wizard State Machine

```mermaid
stateDiagram-v2
    [*] --> NotStarted
    NotStarted --> AdminCreated: POST /wizard/admin
    AdminCreated --> OrganizationSet: POST /wizard/organization
    OrganizationSet --> EnvironmentCreated: POST /wizard/environment
    EnvironmentCreated --> Completed: POST /wizard/complete
    Completed --> [*]
```

Der Wizard:

1. Erstellt den **SystemAdmin** User (erster User im System)
2. Erstellt die **Organization**
3. Erstellt das **Default Environment** (Docker Socket)
4. Markiert den Wizard als abgeschlossen

Alle Daten werden in **SQLite** persistiert, der Wizard-Status in `rsgo.system.json`.

---

## 8. Security & TLS Architecture

### 8.1 Security Overview

```mermaid
flowchart LR
    UI["Admin UI"] --> API["RSGO API"]
    API --> AUTH["JWT Auth"]
    AUTH --> DB[("SQLite Users")]
    AUTH --> JWT["JWT Tokens"]
```

- **Local Auth**: Username/Passwort → BCrypt → JWT Token
- **Roles**: SystemAdmin, OrgOwner, Operator, Viewer
- **Scopes**: Global, Organization, Environment

### 8.2 TLS Flow

```mermaid
sequenceDiagram
    participant C as Customer Admin (Browser)
    participant A as RSGO Admin-Container
    participant T as TLS Engine

    C->>A: https://host:8443 (Wizard)
    A->>T: Check rsgo.tls.json
    T-->>A: Self-Signed OK
    Note over A: Nach Wizard-Installation:
    Note over A: Optional: Custom Certificate Upload
```

---

## 9. Multi-Node & Future Scaling

Auch wenn v0.6 primär Single-Node ist, ist die Architektur vorbereitet auf:

- mehrere **Docker-Hosts** (via Docker API statt Socket)
- **Environment-basierte** Trennung (Production, Staging, Dev)
- **EnvironmentType**: DockerSocket, DockerApi, (future: DockerSwarm, Kubernetes)

Das wird über das `Environment`-Aggregate gesteuert:

```csharp
public enum EnvironmentType
{
    DockerSocket = 0,  // Lokaler Docker Socket
    DockerApi = 1      // Remote Docker API
}
```

---

## Fazit

ReadyStackGo ist so aufgebaut, dass es heute einfache Single-Host-Installationen elegant löst, und morgen zu einer vollwertigen, erweiterbaren On-Prem-Orchestrierungsplattform wachsen kann.

Die **DDD-Architektur** ermöglicht:
- Klare Trennung von Geschäftslogik und Infrastruktur
- Testbare Domain-Logik
- Erweiterbare Bounded Contexts
- Einfache Migration zu anderen Datenbanken bei Bedarf
