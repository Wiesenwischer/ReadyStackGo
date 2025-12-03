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

ReadyStackGo is a self-hosted platform that greatly simplifies the deployment and management of complex microservice stacks based on Docker.
Instead of working directly with `docker run`, Compose, Swarm, or Kubernetes, operators get:

- a **single admin container**,
- a guided **wizard for initial setup**,
- an **admin web UI**,
- **Docker Compose-based deployments** for complete stacks,
- clearly structured **configuration** (SQLite + JSON).

Goal: Simple, repeatable, secure deployments in on-prem and isolated environments.

---

## 2. System Overview

At a high level, ReadyStackGo consists of:

- the **admin container** (ReadyStackGo itself),
- the **Docker host** (or multiple via Docker API),
- the **stack containers** (domains, BFFs, gateways),
- a **SQLite database** for dynamic data,
- **JSON configuration files** for static settings,
- optionally a **stack sources directory** (local stacks, later Git).

---

## 3. High-Level Architecture Diagram

```mermaid
flowchart LR
    subgraph Host["Customer Host / Server"]
        subgraph Admin["ReadyStackGo Admin Container"]
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

ReadyStackGo follows a **Clean Architecture** with Domain-Driven Design (DDD):

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

Contains business logic, independent of infrastructure:

- **SharedKernel**: AggregateRoot, Entity, ValueObject, DomainEvent
- **IdentityAccess**: Organization, User, Role (Bounded Context)
- **Deployment**: Environment, Deployment (Bounded Context)
- **StackManagement**: StackSource, StackDefinition (Bounded Context)

### Application Layer (`ReadyStackGo.Application`)

Orchestrates business logic via UseCases:

- **CQRS Pattern** with MediatR
- **UseCases**: Commands (state-changing) and Queries (read-only)
- **Service Interfaces**: Infrastructure abstraction

### Infrastructure Layer (`ReadyStackGo.Infrastructure`)

Implements technical concerns:

- **DataAccess**: EF Core DbContext, Repositories, SQLite
- **Authentication**: JWT Token Service, BCrypt Password Hasher
- **Configuration**: JSON-based ConfigStore
- **Docker**: Docker Engine Integration via Docker.DotNet
- **Services**: DeploymentEngine, DeploymentService
- **Stacks**: Stack Source Provider, Cache

### API Layer (`ReadyStackGo.Api`)

HTTP interface:

- **FastEndpoints** for REST APIs
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

    User->>UI: Clicks "List Containers"
    UI->>API: GET /api/v1/containers
    API->>MED: Send(ListContainersQuery)
    MED->>APP: Handle(ListContainersQuery)
    APP->>INF: dockerService.ListAsync()
    INF-->>APP: ContainerInfo[]
    APP-->>MED: Result
    MED-->>API: ContainerInfo[]
    API-->>UI: JSON Response
    UI-->>User: Table with containers
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
        DK-->>ENG: OK/Error
    end
    ENG-->>SVC: DeploymentResult
    SVC-->>MED: Result
    MED-->>API: DeploymentResult
    API-->>Admin: success / errorCode
```

### 6.2 DeploymentPlan Structure

The DeploymentEngine generates a plan based on:

- Docker Compose YAML (parsed)
- Environment variables from feature flags
- Organization-specific settings
- Service dependencies (depends_on)

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

The wizard:

1. Creates the **SystemAdmin** user (first user in the system)
2. Creates the **Organization**
3. Creates the **Default Environment** (Docker Socket)
4. Marks the wizard as completed

All data is persisted in **SQLite**, wizard status in `rsgo.system.json`.

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

- **Local Auth**: Username/Password → BCrypt → JWT Token
- **Roles**: SystemAdmin, OrgOwner, Operator, Viewer
- **Scopes**: Global, Organization, Environment

### 8.2 TLS Flow

```mermaid
sequenceDiagram
    participant C as Customer Admin (Browser)
    participant A as RSGO Admin Container
    participant T as TLS Engine

    C->>A: https://host:8443 (Wizard)
    A->>T: Check rsgo.tls.json
    T-->>A: Self-Signed OK
    Note over A: After wizard installation:
    Note over A: Optional: Custom Certificate Upload
```

---

## 9. Multi-Node & Future Scaling

While v0.6 is primarily single-node, the architecture is prepared for:

- multiple **Docker hosts** (via Docker API instead of socket)
- **environment-based** separation (Production, Staging, Dev)
- **EnvironmentType**: DockerSocket, DockerApi, (future: DockerSwarm, Kubernetes)

This is controlled via the `Environment` aggregate:

```csharp
public enum EnvironmentType
{
    DockerSocket = 0,  // Local Docker Socket
    DockerApi = 1      // Remote Docker API
}
```

---

## Conclusion

ReadyStackGo is designed to elegantly solve simple single-host installations today, and grow into a full-featured, extensible on-prem orchestration platform tomorrow.

The **DDD architecture** enables:
- Clear separation of business logic and infrastructure
- Testable domain logic
- Extensible bounded contexts
- Easy migration to other databases if needed
