# Components

ReadyStackGo consists of several clearly separated components that work together to manage the entire stack.

## Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                ReadyStackGo Admin Container                      │
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

## Domain Layer Components

### SharedKernel

Basic DDD Building Blocks:

| Class | Description |
|-------|-------------|
| `AggregateRoot<TId>` | Base class for Aggregate Roots with Domain Events |
| `Entity<TId>` | Base class for Entities with Identity |
| `ValueObject` | Base class for immutable Value Objects |
| `DomainEvent` | Base class for Domain Events |
| `AssertionConcern` | Validation helper methods |

### IdentityAccess Context

Bounded Context for identity and access management:

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
    ├── Role.cs              # Aggregate with Built-in Roles
    ├── RoleId.cs            # Typed ID
    ├── Permission.cs        # Value Object
    ├── ScopeType.cs         # Enum (Global, Organization, Environment)
    └── IRoleRepository.cs
```

### Deployment Context

Bounded Context for deployment management:

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

Bounded Context for stack source management:

```
StackManagement/
└── StackSources/
    ├── StackSource.cs              # Aggregate Root
    ├── StackSourceId.cs            # Typed ID
    ├── StackSourceType.cs          # Enum (LocalDirectory, GitRepository)
    ├── StackDefinition.cs          # Value Object (Stack Metadata)
    ├── StackVariable.cs            # Value Object
    ├── IStackSourceRepository.cs
    └── IStackDefinitionRepository.cs
```

---

## Application Layer Components

### UseCases by Domain

| Domain | Commands | Queries |
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

Infrastructure abstraction for Dependency Inversion:

| Interface | Description |
|-----------|-------------|
| `IDockerService` | Docker Engine abstraction (Containers, Images, Networks) |
| `IDockerComposeParser` | Parses Docker Compose YAML |
| `IDeploymentService` | High-level deployment orchestration |
| `IEnvironmentService` | Environment management |
| `IStackSourceService` | Stack source synchronization |
| `IStackSourceProvider` | Provider pattern for different source types |
| `IStackCache` | In-memory cache for stack definitions |
| `ITokenService` | JWT token generation |

---

## Infrastructure Layer Components

### DataAccess (SQLite with EF Core)

```
DataAccess/
├── ReadyStackGoDbContext.cs     # EF Core DbContext
├── Configurations/              # Fluent API Entity Mappings
│   ├── OrganizationConfiguration.cs
│   ├── UserConfiguration.cs
│   ├── EnvironmentConfiguration.cs
│   └── DeploymentConfiguration.cs
└── Repositories/                # Repository Implementations
    ├── OrganizationRepository.cs
    ├── UserRepository.cs
    ├── RoleRepository.cs        # In-Memory (Built-in Roles)
    ├── EnvironmentRepository.cs
    └── DeploymentRepository.cs
```

### Authentication

```
Authentication/
├── BCryptPasswordHasher.cs      # IPasswordHasher Implementation
├── TokenService.cs              # ITokenService Implementation (JWT)
└── JwtSettings.cs               # JWT Configuration (from appsettings.json)
```

### Configuration (JSON-based)

Only for static configuration:

```
Configuration/
├── IConfigStore.cs              # Interface
├── ConfigStore.cs               # JSON files in /app/config volume
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
├── DeploymentService.cs         # High-Level Orchestration
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
    └── LocalDirectoryStackSourceProvider.cs  # Local Stacks
```

### Manifests

```
Manifests/
├── IManifestProvider.cs
├── ManifestProvider.cs          # Release Manifest Loading
├── ReleaseManifest.cs           # Manifest Model
└── DockerComposeParser.cs       # YAML to DeploymentPlan
```

### TLS

```
Tls/
├── ITlsService.cs
└── TlsService.cs                # Self-Signed & Custom Certificates
```

---

## API Layer Components

### Web UI (Frontend)
- **Technology**: React + TypeScript + Tailwind CSS
- **Features**:
  - Setup Wizard
  - Dashboard with container overview
  - Stack deployments
  - Environment management
  - Feature flags management
  - TLS configuration

### API Layer
- **Technology**: ASP.NET Core + FastEndpoints
- **Responsibilities**:
  - REST endpoints under `/api/v1/*`
  - JWT authentication
  - Input validation
  - MediatR dispatch to UseCases

---

## Managed Containers

### User-Deployed Stacks

Containers managed by ReadyStackGo are labeled:

```
Labels:
  rsgo.stack: "my-stack"
  rsgo.context: "api"
  rsgo.environment: "env-id-guid"
```

These labels enable:
- Grouping by stack
- Identification of services
- Assignment to environments

---

## Communication Between Components

### Within the Admin Container

```
Web UI → API → MediatR → UseCase Handler → Domain/Infrastructure Services
```

### With Docker Engine

```
Infrastructure (DockerService) → Docker Socket/API → Docker Engine
```

### Data Flow

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

All components are registered via ASP.NET Core Dependency Injection:

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

## Next Steps

- [Container Lifecycle](Container-Lifecycle.md)
- [Deployment Engine](Deployment-Engine.md)
- [Architecture Overview](Overview.md)
