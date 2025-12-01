# Komponenten

ReadyStackGo besteht aus mehreren klar getrennten Komponenten, die im Zusammenspiel den gesamten Stack verwalten.

## Übersicht

```
┌─────────────────────────────────────────────────────────────────┐
│                ReadyStackGo Admin-Container                      │
│  ┌─────────┐  ┌─────────┐  ┌──────────────┐                     │
│  │ Web UI  │  │   API   │  │    Wizard    │                     │
│  └────┬────┘  └────┬────┘  └──────┬───────┘                     │
│       └────────────┼───────────────┘                             │
│  ┌─────────────────┴────────────────────────┐                    │
│  │         Application Layer                 │                    │
│  │  (MediatR, UseCases, Commands, Queries)   │                    │
│  └─────────────────┬────────────────────────┘                    │
│  ┌─────────────────┴────────────────────────┐                    │
│  │           Domain Layer                    │                    │
│  │  (Aggregates, Entities, Value Objects,   │                    │
│  │   Domain Services, Domain Events)         │                    │
│  └─────────────────┬────────────────────────┘                    │
│  ┌─────────────────┴────────────────────────┐                    │
│  │       Infrastructure Layer                │                    │
│  │  ┌────────────┐  ┌────────┐  ┌────────┐  │                    │
│  │  │ DataAccess │  │ Docker │  │ Config │  │                    │
│  │  │  (SQLite)  │  │Service │  │ Store  │  │                    │
│  │  └────────────┘  └────────┘  └────────┘  │                    │
│  │  ┌────────────┐  ┌────────┐  ┌────────┐  │                    │
│  │  │   Auth     │  │  TLS   │  │ Stacks │  │                    │
│  │  │  Service   │  │Service │  │ Service│  │                    │
│  │  └────────────┘  └────────┘  └────────┘  │                    │
│  └──────────────────────────────────────────┘                    │
└─────────────────────────────────────────────────────────────────┘
                         │
                    Docker Socket / API
                         │
        ┌────────────────┴────────────────┐
        │      Managed Containers          │
        │  (Gateway, BFFs, Services)       │
        └──────────────────────────────────┘
```

---

## Domain Layer Komponenten

### SharedKernel

Grundlegende DDD Building Blocks:

| Klasse | Beschreibung |
|--------|--------------|
| `AggregateRoot<TId>` | Basisklasse für Aggregate Roots mit Domain Events |
| `Entity<TId>` | Basisklasse für Entities mit Identity |
| `ValueObject` | Basisklasse für unveränderliche Value Objects |
| `DomainEvent` | Basisklasse für Domain Events |
| `AssertionConcern` | Validierungs-Hilfsmethoden |

### IdentityAccess Context

Bounded Context für Identitäts- und Zugriffsverwaltung:

```
IdentityAccess/
├── Organizations/           # Organization Aggregate
│   ├── Organization.cs      # Aggregate Root
│   ├── OrganizationId.cs    # Typed ID (Value Object)
│   ├── IOrganizationRepository.cs
│   └── OrganizationProvisioningService.cs  # Domain Service
│
├── Users/                   # User Aggregate
│   ├── User.cs              # Aggregate Root
│   ├── UserId.cs            # Typed ID
│   ├── EmailAddress.cs      # Value Object
│   ├── HashedPassword.cs    # Value Object
│   ├── Enablement.cs        # Value Object
│   ├── RoleAssignment.cs    # Value Object
│   ├── IUserRepository.cs
│   ├── AuthenticationService.cs           # Domain Service
│   └── SystemAdminRegistrationService.cs  # Domain Service
│
└── Roles/                   # Role Aggregate
    ├── Role.cs              # Aggregate mit Built-in Rollen
    ├── RoleId.cs            # Typed ID
    ├── Permission.cs        # Value Object
    ├── ScopeType.cs         # Enum (Global, Organization, Environment)
    └── IRoleRepository.cs
```

### Deployment Context

Bounded Context für Deployment-Management:

```
Deployment/
├── Environments/            # Environment Aggregate
│   ├── Environment.cs       # Aggregate Root
│   ├── EnvironmentId.cs     # Typed ID
│   ├── EnvironmentType.cs   # Enum (DockerSocket, DockerApi)
│   ├── ConnectionConfig.cs  # Value Object
│   └── IEnvironmentRepository.cs
│
└── Deployments/             # Deployment Aggregate
    ├── Deployment.cs        # Aggregate Root
    ├── DeploymentId.cs      # Typed ID
    ├── DeploymentStatus.cs  # Enum (Pending, Running, Stopped, Failed, Removed)
    ├── DeployedService.cs   # Entity
    └── IDeploymentRepository.cs
```

### StackManagement Context

Bounded Context für Stack-Quellenverwaltung:

```
StackManagement/
└── StackSources/
    ├── StackSource.cs              # Aggregate Root
    ├── StackSourceId.cs            # Typed ID
    ├── StackSourceType.cs          # Enum (LocalDirectory, GitRepository)
    ├── StackDefinition.cs          # Value Object (Stack-Metadaten)
    ├── StackVariable.cs            # Value Object
    ├── IStackSourceRepository.cs
    └── IStackDefinitionRepository.cs
```

---

## Application Layer Komponenten

### UseCases nach Domäne

| Domäne | Commands | Queries |
|--------|----------|---------|
| **Administration** | RegisterSystemAdmin | - |
| **Authentication** | Login | - |
| **Organizations** | ProvisionOrganization | - |
| **Environments** | CreateEnvironment, UpdateEnvironment, DeleteEnvironment, SetDefaultEnvironment, TestConnection | GetEnvironment, ListEnvironments |
| **Deployments** | DeployCompose, RemoveDeployment, ParseCompose | GetDeployment, ListDeployments |
| **Containers** | StartContainer, StopContainer | ListContainers |
| **Stacks** | - | GetStack, ListStacks |
| **StackSources** | SyncStackSources | ListStackSources |
| **Dashboard** | - | GetDashboardStats |
| **Wizard** | CompleteWizard | GetWizardStatus |

### Service Interfaces

Abstraktion der Infrastruktur für Dependency Inversion:

| Interface | Beschreibung |
|-----------|--------------|
| `IDockerService` | Docker Engine Abstraktion (Container, Images, Networks) |
| `IDockerComposeParser` | Parst Docker Compose YAML |
| `IDeploymentService` | High-Level Deployment-Orchestrierung |
| `IEnvironmentService` | Environment-Management |
| `IStackSourceService` | Stack Source Synchronisation |
| `IStackSourceProvider` | Provider Pattern für verschiedene Source-Typen |
| `IStackCache` | In-Memory Cache für Stack-Definitionen |
| `ITokenService` | JWT Token Generation |

---

## Infrastructure Layer Komponenten

### DataAccess (SQLite mit EF Core)

```
DataAccess/
├── ReadyStackGoDbContext.cs     # EF Core DbContext
├── Configurations/              # Fluent API Entity Mappings
│   ├── OrganizationConfiguration.cs
│   ├── UserConfiguration.cs
│   ├── EnvironmentConfiguration.cs
│   └── DeploymentConfiguration.cs
└── Repositories/                # Repository Implementierungen
    ├── OrganizationRepository.cs
    ├── UserRepository.cs
    ├── RoleRepository.cs        # In-Memory (Built-in Rollen)
    ├── EnvironmentRepository.cs
    └── DeploymentRepository.cs
```

### Authentication

```
Authentication/
├── BCryptPasswordHasher.cs      # IPasswordHasher Implementation
├── TokenService.cs              # ITokenService Implementation (JWT)
└── JwtSettings.cs               # JWT Konfiguration (aus appsettings.json)
```

### Configuration (JSON-basiert)

Nur noch für statische Konfiguration:

```
Configuration/
├── IConfigStore.cs              # Interface
├── ConfigStore.cs               # JSON-Dateien im /app/config Volume
├── SystemConfig.cs              # rsgo.system.json
├── TlsConfig.cs                 # rsgo.tls.json
├── FeaturesConfig.cs            # rsgo.features.json
├── ReleaseConfig.cs             # rsgo.release.json
├── WizardState.cs               # Enum
└── DeploymentMode.cs            # Enum
```

### Docker

```
Docker/
└── DockerService.cs             # Docker.DotNet Wrapper
    - ListContainersAsync()
    - CreateAndStartContainerAsync()
    - StopContainerAsync()
    - RemoveContainerAsync()
    - PullImageAsync()
    - EnsureNetworkAsync()
    - etc.
```

### Services (Business Services)

```
Services/
├── IDeploymentEngine.cs         # Interface
├── DeploymentEngine.cs          # Deployment Plan Generation & Execution
├── DeploymentService.cs         # High-Level Orchestrierung
└── EnvironmentService.cs        # Environment Operations
```

### Stacks (Stack Source Management)

```
Stacks/
├── StackSourceService.cs        # IStackSourceService Implementation
├── InMemoryStackCache.cs        # IStackCache Implementation
├── Configuration/
│   └── StackSourceConfig.cs     # appsettings.json Binding
└── Sources/
    └── LocalDirectoryStackSourceProvider.cs  # Lokale Stacks
```

### Manifests

```
Manifests/
├── IManifestProvider.cs
├── ManifestProvider.cs          # Release Manifest Loading
├── ReleaseManifest.cs           # Manifest Model
└── DockerComposeParser.cs       # YAML zu DeploymentPlan
```

### TLS

```
Tls/
├── ITlsService.cs
└── TlsService.cs                # Self-Signed & Custom Certificates
```

---

## API Layer Komponenten

### Web UI (Frontend)
- **Technologie**: React + TypeScript + Tailwind CSS
- **Features**:
  - Setup-Wizard
  - Dashboard mit Container-Übersicht
  - Stack-Deployments
  - Environment-Management
  - Feature-Flags-Verwaltung
  - TLS-Konfiguration

### API Layer
- **Technologie**: ASP.NET Core + FastEndpoints
- **Verantwortlichkeiten**:
  - REST-Endpunkte unter `/api/v1/*`
  - JWT-Authentifizierung
  - Input-Validierung
  - MediatR Dispatch zu UseCases

---

## Managed Containers

### Vom Benutzer deployte Stacks

Die von ReadyStackGo verwalteten Container werden mit Labels versehen:

```
Labels:
  rsgo.stack: "my-stack"
  rsgo.context: "api"
  rsgo.environment: "env-id-guid"
```

Diese Labels ermöglichen:
- Gruppierung nach Stack
- Identifikation von Services
- Zuordnung zu Environments

---

## Kommunikation zwischen Komponenten

### Innerhalb des Admin-Containers

```
Web UI → API → MediatR → UseCase Handler → Domain/Infrastructure Services
```

### Mit Docker Engine

```
Infrastructure (DockerService) → Docker Socket/API → Docker Engine
```

### Datenfluss

```
                    ┌─────────────┐
                    │   API       │
                    └──────┬──────┘
                           │ MediatR
                    ┌──────┴──────┐
                    │  UseCase    │
                    │  Handler    │
                    └──────┬──────┘
           ┌───────────────┼───────────────┐
           │               │               │
    ┌──────┴──────┐ ┌──────┴──────┐ ┌──────┴──────┐
    │   Domain    │ │   Domain    │ │ Infrastructure│
    │   Service   │ │ Repository  │ │   Service    │
    └─────────────┘ └──────┬──────┘ └──────┬──────┘
                           │               │
                    ┌──────┴──────┐ ┌──────┴──────┐
                    │   SQLite    │ │   Docker    │
                    │   (EF Core) │ │   Engine    │
                    └─────────────┘ └─────────────┘
```

---

## Dependency Injection

Alle Komponenten werden über ASP.NET Core Dependency Injection registriert:

```csharp
// Domain Services
services.AddScoped<AuthenticationService>();
services.AddScoped<SystemAdminRegistrationService>();
services.AddScoped<OrganizationProvisioningService>();

// Repositories
services.AddScoped<IOrganizationRepository, OrganizationRepository>();
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<IRoleRepository, RoleRepository>();
services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
services.AddScoped<IDeploymentRepository, DeploymentRepository>();

// Infrastructure Services
services.AddScoped<IDockerService, DockerService>();
services.AddScoped<IConfigStore, ConfigStore>();
services.AddScoped<ITokenService, TokenService>();
services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
services.AddScoped<IDeploymentEngine, DeploymentEngine>();
services.AddScoped<IDeploymentService, DeploymentService>();
services.AddScoped<IEnvironmentService, EnvironmentService>();
services.AddScoped<IStackSourceService, StackSourceService>();

// MediatR
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
```

---

## Nächste Schritte

- [Container Lifecycle](Container-Lifecycle.md)
- [Deployment Engine](Deployment-Engine.md)
- [Architecture Overview](Overview.md)
