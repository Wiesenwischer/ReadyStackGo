# Distribution Architecture

ReadyStackGo unterstützt **downstream Distributionen**: separate Docker-Images mit eigenem Branding, Wizard-Flow und vorkonfigurierten Defaults — aufgebaut auf dem gleichen Core.

## Übersicht

```
ReadyStackGo (GitHub, OSS)
├── ReadyStackGo.Core           Meta-Package (Domain + Application + Infrastructure)
├── ReadyStackGo.Api            Generischer ASP.NET Host
└── @rsgo/core + @rsgo/ui-generic   Frontend (pnpm Monorepo)

ReadyStackGo.Ams (Azure DevOps, privat)
├── ReadyStackGo.WebHost.Ams    ASP.NET Host → referenziert ReadyStackGo.Core
└── @rsgo/ui-ams                Eigenes Frontend-Package
```

## Extension Points

### 1. ISetupWizardDefinitionProvider

Definiert die Onboarding-Schritte nach dem Admin-Login (Phase 2). Der Wizard (Phase 1 — Admin-Erstellung mit 5-Minuten-Timeout) bleibt unverändert.

```csharp
// Interface in ReadyStackGo.Application.Services
public interface ISetupWizardDefinitionProvider
{
    SetupWizardDefinition GetDefinition();
}
```

**Generic-Implementierung** (`GenericSetupWizardDefinitionProvider`): 4 Schritte — Organization, Environment, Stack Sources, Registries.

**Eigene Implementierung registrieren:**

```csharp
// In der Distribution's Program.cs — VOR AddApplication()
builder.Services.AddSingleton<ISetupWizardDefinitionProvider, AmsSetupWizardDefinitionProvider>();
builder.Services.AddApplication();   // TryAddSingleton wird übersprungen
builder.Services.AddInfrastructure(config);
```

**API Endpunkt:** `GET /api/wizard/definition` — liefert die Step-Definitionen als JSON.

### 2. IBootstrapper

Distributionsspezifische Initialisierung beim Start. Muss idempotent sein (läuft bei jedem Start).

```csharp
public interface IBootstrapper
{
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
```

**Generic-Implementierung** (`GenericBootstrapper`): No-Op.

**Beispiel für eine ams-Distribution:**

```csharp
public sealed class AmsBootstrapper : IBootstrapper
{
    private readonly IStackSourceRepository _sources;
    private readonly IRegistryRepository _registries;

    public AmsBootstrapper(IStackSourceRepository sources, IRegistryRepository registries)
    {
        _sources = sources;
        _registries = registries;
    }

    public async Task BootstrapAsync(CancellationToken ct)
    {
        // Seed ams-spezifische Stack Source (idempotent)
        if (!await _sources.ExistsByNameAsync("ams-stacks", ct))
        {
            var source = StackSource.CreateGitRepository(
                StackSourceId.NewId(), "ams-stacks", "https://github.com/amssolution/rsgo-stacks");
            await _sources.AddAsync(source, ct);
        }
    }
}
```

**Registrierung:** Wie bei `ISetupWizardDefinitionProvider` — vor `AddApplication()` registrieren.

### 3. Multi-Assembly FastEndpoints

FastEndpoints ist auf explizite Assembly-Discovery konfiguriert. Downstream-Distributionen fügen ihre Endpoint-Assemblies hinzu:

```csharp
builder.Services.AddFastEndpoints(o =>
{
    o.Assemblies = [
        typeof(Program).Assembly,                    // Generische Endpoints
        typeof(AmsSpecificEndpoint).Assembly          // Distribution-spezifische Endpoints
    ];
});
```

### 4. ReadyStackGo.Core Meta-Package

Konvenienzbaustein für downstream Projekte — referenziert Domain, Application und Infrastructure in einem Projekt:

```xml
<!-- In der Distribution's .csproj -->
<ItemGroup>
  <ProjectReference Include="path/to/ReadyStackGo.Core.csproj" />
</ItemGroup>
```

Später als NuGet-Package veröffentlichbar (`ReadyStackGo.Core`).

## Eigene Distribution erstellen

### 1. Projekt-Struktur

```
MyDistribution/
  src/
    MyDistribution.WebHost/
      MyDistribution.WebHost.csproj   → referenziert ReadyStackGo.Core
      Program.cs
      MyBootstrapper.cs
      MySetupWizardDefinitionProvider.cs
      Endpoints/                      → Optionale eigene Endpoints
    MyDistribution.Ui/                → Eigenes Frontend (optional)
  Dockerfile
```

### 2. Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Eigene Extension Points registrieren (VOR AddApplication!)
builder.Services.AddSingleton<ISetupWizardDefinitionProvider, MyWizardProvider>();
builder.Services.AddScoped<IBootstrapper, MyBootstrapper>();

// 2. Core Services registrieren
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 3. FastEndpoints mit eigenen Assemblies
builder.Services.AddFastEndpoints(o =>
{
    o.Assemblies = [typeof(Program).Assembly];
});

// ... Rest wie in ReadyStackGo.Api/Program.cs
```

### 3. Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "MyDistribution.WebHost.dll"]
```

## DI-Reihenfolge

Die Reihenfolge der Service-Registrierung ist wichtig:

1. **Distribution-spezifische Services** (eigene `ISetupWizardDefinitionProvider`, `IBootstrapper`)
2. `AddApplication()` — registriert MediatR, Domain Events und **generische Defaults** via `TryAdd`
3. `AddInfrastructure(config)` — registriert Infrastruktur-Services
4. `AddFastEndpoints(...)` — mit expliziter Assembly-Liste

`TryAdd` in `AddApplication()` garantiert: Wenn die Distribution ihre Implementierung zuerst registriert, wird die generische übersprungen.

## Onboarding-Status API

`GET /api/onboarding/status` enthält jetzt zusätzlich:

```json
{
  "isComplete": true,
  "isDismissed": false,
  "organization": { "done": true, "count": 1, "name": "My Corp" },
  "environment": { "done": true, "count": 2 },
  "stackSources": { "done": true, "count": 3 },
  "registries": { "done": false, "count": 0 },
  "distributionId": "generic",
  "steps": [
    {
      "id": "organization",
      "title": "Set Up Organization",
      "description": "Create your organization...",
      "componentType": "OrganizationStep",
      "required": true,
      "order": 1,
      "done": true,
      "count": 1
    }
  ]
}
```

Die fixen Properties (`organization`, `environment`, etc.) bleiben für Abwärtskompatibilität. Das neue `steps`-Array ist datengetrieben und stammt aus der `ISetupWizardDefinitionProvider`-Implementierung.
