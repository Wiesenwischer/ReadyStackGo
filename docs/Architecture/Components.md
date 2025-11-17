# Komponenten

ReadyStackGo besteht aus mehreren klar getrennten Komponenten, die im Zusammenspiel den gesamten Stack verwalten.

## Übersicht

```
┌─────────────────────────────────────────────────────────┐
│              ReadyStackGo Admin-Container                │
│  ┌─────────┐  ┌─────────┐  ┌──────────────┐            │
│  │ Web UI  │  │   API   │  │    Wizard    │            │
│  └────┬────┘  └────┬────┘  └──────┬───────┘            │
│       └────────────┼───────────────┘                    │
│  ┌─────────────────┴────────────────────────┐           │
│  │         Application Layer                 │           │
│  │  (Dispatcher, Commands, Queries)          │           │
│  └─────────────────┬────────────────────────┘           │
│  ┌─────────────────┴────────────────────────┐           │
│  │       Infrastructure Layer                │           │
│  │  ┌──────────┐  ┌────────┐  ┌──────────┐ │           │
│  │  │  Docker  │  │  TLS   │  │  Config  │ │           │
│  │  │ Service  │  │ Engine │  │  Store   │ │           │
│  │  └──────────┘  └────────┘  └──────────┘ │           │
│  └──────────────────────────────────────────┘           │
└─────────────────────────────────────────────────────────┘
                         │
                    Docker Socket
                         │
        ┌────────────────┴────────────────┐
        │      Managed Containers          │
        │  (Gateway, BFFs, Services)       │
        └──────────────────────────────────┘
```

## Admin-Container Komponenten

### Web UI (Frontend)
- **Technologie**: React + TypeScript + Tailwind CSS
- **Features**:
  - Setup-Wizard
  - Dashboard mit Container-Übersicht
  - Release-Management
  - Feature-Flags-Verwaltung
  - TLS-Konfiguration

### API Layer
- **Technologie**: ASP.NET Core + FastEndpoints
- **Verantwortlichkeiten**:
  - REST-Endpunkte
  - Authentifizierung/Autorisierung
  - Input-Validierung
  - Routing zu Application Layer

### Application Layer
- **Pattern**: Dispatcher + Commands/Queries
- **Komponenten**:
  - **Dispatcher**: Routing von Requests zu Handlers
  - **Command Handlers**: Zustandsändernde Operationen
  - **Query Handlers**: Lesende Operationen
  - **Policies**: Geschäftslogik

### Domain Layer
- **Reine Geschäftsobjekte**:
  - Entities (z.B. `Container`, `Release`, `Manifest`)
  - Value Objects (z.B. `Version`, `ConnectionString`)
  - Domain Services
  - Interfaces für Infrastructure

### Infrastructure Layer

#### Docker Service
- **Verantwortlichkeiten**:
  - Kommunikation mit Docker Engine via Docker Socket
  - Container-Lifecycle (Create, Start, Stop, Remove)
  - Image-Management
  - Network-Management
  - Volume-Management

#### TLS Engine
- **Verantwortlichkeiten**:
  - Self-Signed-Zertifikat-Generierung beim Bootstrap
  - Custom-Zertifikat-Upload und -Validierung
  - Zertifikatsspeicherung
  - TLS-Konfiguration

#### Config Store
- **Verantwortlichkeiten**:
  - Lesen/Schreiben von `rsgo.*.json` Dateien
  - Config-Versionierung
  - Validierung von Konfigurationen
  - Persistente Speicherung im Volume

#### Manifest Provider
- **Verantwortlichkeiten**:
  - Laden von Release-Manifesten
  - Schema-Validierung
  - Version-Resolution
  - Manifest-Repository-Zugriff

## Managed Containers

### Gateway-Schicht
- **Edge Gateway**: TLS-Termination, Reverse Proxy
- **Public API Gateway**: Öffentliche API-Endpunkte
- **BFF Desktop**: Backend-for-Frontend für Desktop-App
- **BFF Web**: Backend-for-Frontend für Web-App

### Fachliche Kontexte (Microservices)
Beispiele aus dem AMS-System:
- **Project**: Projektverwaltung
- **Memo**: Memo-Verwaltung
- **Discussion**: Diskussionen
- **Identity**: Benutzerverwaltung
- **Notification**: Benachrichtigungen
- **Search**: Suchfunktionalität
- **Files**: Dateiverwaltung

## Kommunikation zwischen Komponenten

### Innerhalb des Admin-Containers
```
Web UI → API → Dispatcher → Handler → Infrastructure Services
```

### Mit Docker Engine
```
Infrastructure (DockerService) → Docker Socket → Docker Engine
```

### Zwischen Managed Containers
```
Gateway → BFF → Services (über Docker Network: rsgo-net)
```

## Dependency Injection

Alle Komponenten werden über ASP.NET Core Dependency Injection registriert:

```csharp
// Simplified example
services.AddScoped<IDispatcher, Dispatcher>();
services.AddScoped<IDockerService, DockerService>();
services.AddScoped<IConfigStore, ConfigStore>();
services.AddScoped<ITlsEngine, TlsEngine>();
services.AddScoped<IManifestProvider, ManifestProvider>();
```

## Nächste Schritte

- [Container Lifecycle](Container-Lifecycle.md)
- [Deployment Engine](Deployment-Engine.md)
- [Architecture Overview](Overview.md)
