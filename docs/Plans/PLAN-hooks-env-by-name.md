<!-- GitHub Epic: #324 -->
# Phase: Hooks — Environment Resolution by Name

## Ziel

Alle Hook-Endpoints (`/api/hooks/deploy`, `/api/hooks/upgrade`, `/api/hooks/redeploy`, `/api/hooks/stop`, `/api/hooks/restart`) sollen neben der Environment-GUID auch den **Environment-Namen** akzeptieren. Das vereinfacht die CI/CD-Integration, da der Name lesbarer ist als eine GUID.

## Analyse

### Bestehende Architektur

Alle 5 Hook-Handler (+ ihre Endpoints) folgen dem gleichen Pattern:

1. **Endpoint** (`src/ReadyStackGo.Api/Endpoints/Hooks/`) liest `EnvironmentId` aus dem Request Body, Fallback auf `env_id` Claim aus API Key
2. **Handler** (`src/ReadyStackGo.Application/UseCases/Hooks/`) validiert mit `Guid.TryParse(request.EnvironmentId, ...)` — schlägt fehl wenn kein GUID

**Bereits vorhanden:**
- `IEnvironmentRepository.GetByName(OrganizationId, string name)` — Name-basierte Suche existiert bereits
- Alle Hooks laufen im Kontext einer Organisation (über API Key oder JWT)

### Betroffene Handler

| Handler | Datei |
|---|---|
| DeployViaHookHandler | `Application/UseCases/Hooks/DeployStack/DeployViaHookCommand.cs` |
| RedeployViaHookHandler | `Application/UseCases/Hooks/RedeployStack/RedeployViaHookCommand.cs` |
| UpgradeViaHookHandler | `Application/UseCases/Hooks/UpgradeViaHook/UpgradeViaHookCommand.cs` |
| StopContainersViaHookHandler | `Application/UseCases/Hooks/StopContainers/StopContainersViaHookCommand.cs` |
| RestartContainersViaHookHandler | `Application/UseCases/Hooks/RestartContainers/RestartContainersViaHookCommand.cs` |

### Lösung

Statt `Guid.TryParse` direkt zu prüfen, einen **gemeinsamen Resolver** einführen:

```csharp
// If value is a valid GUID, use it directly.
// Otherwise, look up by name via IEnvironmentRepository.GetByName().
```

Da alle Handler denselben Pattern haben, bietet sich eine **shared Helper-Methode** oder ein **Application Service** an.

## AMS UI Counterpart

- [x] **Nein** — reine Backend-Änderung, kein UI betroffen

## Features / Schritte

- [ ] **Feature 1: Environment Resolver Service** – Shared Methode die GUID oder Name akzeptiert und EnvironmentId zurückgibt
  - Neue statische Methode oder Service: `ResolveEnvironmentId(string idOrName, IEnvironmentRepository, OrganizationId)`
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/Services/EnvironmentResolver.cs` (neu)
  - Abhängig von: -

- [ ] **Feature 2: Alle Hook-Handler umstellen** – `Guid.TryParse` durch Resolver ersetzen
  - Betroffene Dateien (5 Handler):
    - `src/ReadyStackGo.Application/UseCases/Hooks/DeployStack/DeployViaHookCommand.cs`
    - `src/ReadyStackGo.Application/UseCases/Hooks/RedeployStack/RedeployViaHookCommand.cs`
    - `src/ReadyStackGo.Application/UseCases/Hooks/UpgradeViaHook/UpgradeViaHookCommand.cs`
    - `src/ReadyStackGo.Application/UseCases/Hooks/StopContainers/StopContainersViaHookCommand.cs`
    - `src/ReadyStackGo.Application/UseCases/Hooks/RestartContainers/RestartContainersViaHookCommand.cs`
  - Abhängig von: Feature 1

- [ ] **Feature 3: Unit Tests** – Resolver + Handler Tests
  - GUID-Input → direkt verwenden
  - Name-Input → Lookup, Environment gefunden → ID zurückgeben
  - Name-Input → Lookup, nicht gefunden → Fehlermeldung
  - Leerer Input → Fehlermeldung
  - Abhängig von: Feature 1

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**: EnvironmentResolver mit Mock-Repository (GUID vs Name vs nicht gefunden)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Resolver-Implementierung | A: Statische Methode, B: Injected Service | A | Einfach, kein DI nötig — Repository wird als Parameter übergeben |
| Request-Feld | A: `EnvironmentId` akzeptiert beides, B: Neues Feld `EnvironmentName` | A | Weniger Breaking Change, ein Feld für beides ist intuitiver |
