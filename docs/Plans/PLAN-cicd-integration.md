# Phase: CI/CD Integration (v0.19)

## Ziel

CI/CD Pipelines (Azure DevOps, GitHub Actions) sollen automatisch Deployments auf ReadyStackGo-Instanzen auslösen können. Authentifizierung über API Keys (`X-Api-Key` Header), drei Webhook-Endpoints für Redeploy, Upgrade und Katalog-Sync.

Primärer Use Case: Nach einem erfolgreichen Build in der ams.project-Pipeline soll der aktualisierte Stack/Container automatisch auf den Testserver deployt werden.

## Analyse

### Bestehende Architektur

**Authentifizierung (JWT + RBAC)**:
- JWT-basiert mit HS256, Token via `TokenService.cs`
- RBAC via `RbacPreProcessor` (FastEndpoints IPreProcessor)
- Rollen: SystemAdmin, OrganizationOwner, Operator, Viewer
- Permissions: `Resource.Action` Format (z.B. `Deployments.Create`)
- Claim-basiert: `RoleAssignments` als JSON-Array im JWT

**Deployment-Flow**:
- `POST /api/environments/{envId}/stacks/{stackId}/deploy` → `DeployStackEndpoint` → MediatR `DeployStackCommand` → `DeployStackHandler` → `DeploymentService` → `DeploymentEngine`
- StackId Format: `sourceId:productName:stackName` (3-teilig)
- Deploy bei existierendem Stack: Alte Container stoppen → Images pullen → Neue Container starten
- Progress via SignalR (SessionId-basiert)

**Upgrade-Flow**:
- `POST /api/environments/{envId}/deployments/{deploymentId}/upgrade` → `UpgradeStackHandler`
- Lädt neue Stack-Version aus Katalog, merged Variables, delegiert an `DeployStackCommand`
- Snapshot für Rollback wird erstellt

**Stack-Auflösung**:
- `IProductSourceService.GetStackAsync(stackId)` → `StackDefinition`
- `IProductCache`: `GetProductVersions(groupId)`, `GetAvailableUpgrades(groupId, version)`
- Quellen: Lokale Verzeichnisse, Git Repositories

**Domain-Patterns**:
- Aggregate Roots: `AggregateRoot<TId>`, Factory Methods, private Konstruktoren
- Value Objects: `ValueObject` mit `GetEqualityComponents()`
- ID Types: `RegistryId`, `UserId` etc. (Guid-basiert)
- Repositories: Synchron (Registry) oder Async (StackSource)
- EF Core: SQLite, `IEntityTypeConfiguration<T>`, JSON-Spalten für Collections

**Betroffene Dateien (Pattern-Vorbilder)**:
- `src/ReadyStackGo.Domain/Deployment/Registries/Registry.cs` – Aggregate Root Pattern
- `src/ReadyStackGo.Infrastructure.DataAccess/Configurations/RegistryConfiguration.cs` – EF Core Config Pattern
- `src/ReadyStackGo.Infrastructure.Security/Authentication/TokenService.cs` – Auth Pattern
- `src/ReadyStackGo.Api/Authorization/RbacPreProcessor.cs` – Authorization Pattern
- `src/ReadyStackGo.Api/Program.cs` – Auth-Schema Registration
- `src/ReadyStackGo.Application/UseCases/Deployments/DeployStack/DeployStackHandler.cs` – Deploy-Logik
- `src/ReadyStackGo.Application/UseCases/Deployments/UpgradeStack/UpgradeStackHandler.cs` – Upgrade-Logik

## Architektur-Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Key-Hashing | A) bcrypt, B) SHA-256 | B) SHA-256 | Keys müssen bei jedem Request per Index gesucht werden. bcrypt ist zu langsam (~100ms). 37-Zeichen-Keys (rsgo_ + 32 random) haben ~190 Bit Entropie – Rainbow Tables sind unpraktikabel. |
| Auth-Schema | A) Separates Schema, B) Policy-Scheme | B) Policy-Scheme | `X-Api-Key` Header → ApiKey-Schema, sonst JWT. Bestehender RbacPreProcessor funktioniert mit beiden (liest Claims). Kein Umbau der bestehenden Endpoints nötig. |
| Webhook vs. REST | A) Bestehende Endpoints, B) Separate Hooks | B) Separate Hooks | Einfachere DTOs (stackName statt stackId), eigene Permissions (`Hooks.Redeploy`), kein SignalR nötig. Pipeline-freundlich. |
| Redeploy-Implementierung | A) Neuer Deploy-Pfad, B) DeployStackCommand wiederverwenden | B) Wiederverwenden | Redeploy = gleiche Parameter an DeployStackCommand. Kein neuer Deploy-Pfad nötig. Engine stoppt alte Container, pullt frische Images, startet neu. |
| Key-Format | A) UUID, B) Prefix + Random | B) `rsgo_` + 32 alphanumerisch | Erkennbar als RSGo-Key, einfach in Logs/Configs zu identifizieren. Prefix für Format-Validierung. |
| DB-Migration | A) EF Migrations, B) Manual CREATE TABLE, C) EnsureCreated beibehalten | C) EnsureCreated | Pre-v1.0 akzeptabel. Bestehende User löschen DB bei Upgrade. Kein Migration-Overhead. |
| API Key Scope | A) Alle Endpoints, B) Nur Hooks | B) Nur Hooks | Klarere Security-Grenze. `/api/hooks/*` nur per API Key, reguläre Endpoints nur per JWT. |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [x] **Feature 1: API Key Domain + Data Access** (PR #80 gegen integration)
  - `ApiKeyId` Value Object (Pattern wie `RegistryId`)
  - `ApiKey` Aggregate Root: Name, KeyHash (SHA-256), KeyPrefix (erste 12 Zeichen), OrganizationId, EnvironmentId? (Scope), Permissions (List\<string\>), CreatedAt, LastUsedAt?, ExpiresAt?, IsRevoked, RevokedAt?, RevokedReason?
  - Methoden: `Create(...)`, `Revoke(reason?)`, `RecordUsage()`, `IsExpired()`, `IsValid()`, `HasPermission(string)` mit Wildcard-Support (`*.*`, `Resource.*`)
  - Domain Events: `ApiKeyCreated`, `ApiKeyRevoked`
  - `IApiKeyRepository`: GetById, GetByKeyHash, GetByOrganization, Add, Update, Remove, SaveChanges
  - EF Core `ApiKeyConfiguration`: Tabelle `ApiKeys`, Permissions als JSON-Spalte, Indexes: KeyHash (unique), OrganizationId, (OrganizationId + Name) unique
  - `ApiKeyRepository` (synchron, Pattern wie `RegistryRepository`)
  - `ReadyStackGoDbContext`: `DbSet<ApiKey> ApiKeys`
  - DI Registration in `Infrastructure.DataAccess/DependencyInjection.cs`
  - Neue Permissions in `Permission.cs`: `ApiKeys.Create/Read/Delete`, `Hooks.Redeploy/Upgrade/SyncSources`
  - Rollen-Update in `Role.cs`: SystemAdmin + OrganizationOwner erhalten ApiKeys-Permissions
  - **Tests**: Unit (Create valid/invalid, Revoke normal/already-revoked, IsValid/IsExpired Varianten, HasPermission mit Wildcards, RecordUsage) + Integration (CRUD, KeyHash-Lookup, Org-Isolation, Unique Constraints)
  - Branch: `feature/cicd-api-key-domain`
  - Abhängig von: -

- [x] **Feature 2: API Key Authentication Middleware** (PR #81 gegen integration)
  - ASP.NET Core `AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>` in `Infrastructure.Security`
  - Scheme: `"ApiKey"`, Header: `"X-Api-Key"`
  - Flow: Header lesen → Format prüfen (`rsgo_` Prefix) → SHA-256 Hash → DB-Lookup via `IApiKeyRepository.GetByKeyHash()` → Validierung (revoked/expired) → ClaimsPrincipal bauen
  - Claims: `apikey_id`, `apikey_name`, `org_id`, `env_id` (optional), `RoleAssignments` (Operator-Level kompatibel mit bestehendem RbacService), `api_permission` pro Permission
  - Static Helper: `ComputeSha256Hash(string rawKey)` (SHA256 → hex lowercase)
  - Usage-Tracking: `RecordUsage()` + SaveChanges bei erfolgreichem Auth
  - `Program.cs` Multi-Scheme Setup: `AddPolicyScheme("MultiScheme", ...)` wählt basierend auf `X-Api-Key` Header zwischen ApiKey und JWT Bearer
  - **Tests**: Unit (Valid key → Success + Claims, kein Header → NoResult, leerer Header → NoResult, falsches Prefix → Fail, unbekannter Key → Fail, Revoked → Fail, Expired → Fail, Environment-Scope → env_id Claim, Usage-Tracking aufgerufen, ComputeSha256Hash deterministisch)
  - Branch: `feature/cicd-api-key-auth`
  - Abhängig von: Feature 1

- [x] **Feature 3: API Key Management (CRUD + UI)** (PR #82 gegen integration)
  - **Application Layer** (`src/ReadyStackGo.Application/UseCases/ApiKeys/`):
    - `ApiKeyDtos.cs`: `ApiKeyDto` (id, name, keyPrefix, orgId, envId?, permissions, createdAt, lastUsedAt?, expiresAt?, isRevoked), `ApiKeyCreatedDto` (id, name, keyPrefix, **fullKey** – nur einmalig!)
    - `CreateApiKey/`: Command (Name, EnvironmentId?, Permissions[], ExpiresAt?) + Handler (Key generieren: `rsgo_` + 32 random alphanumerisch, SHA-256 hashen, Prefix extrahieren, ApiKey.Create(), speichern, fullKey einmalig zurückgeben)
    - `ListApiKeys/`: Query + Handler (alle Keys der Organisation)
    - `RevokeApiKey/`: Command (ApiKeyId, Reason?) + Handler
  - **API Endpoints** (`src/ReadyStackGo.Api/Endpoints/ApiKeys/`):
    - `CreateApiKeyEndpoint.cs`: `POST /api/api-keys` mit `[RequirePermission("ApiKeys", "Create")]`
    - `ListApiKeysEndpoint.cs`: `GET /api/api-keys` mit `[RequirePermission("ApiKeys", "Read")]`
    - `RevokeApiKeyEndpoint.cs`: `DELETE /api/api-keys/{id}` mit `[RequirePermission("ApiKeys", "Delete")]`
  - **Frontend**:
    - `src/ReadyStackGo.WebUi/src/api/apiKeys.ts`: TypeScript DTOs + API-Funktionen
    - `src/ReadyStackGo.WebUi/src/pages/Settings/CiCd/CiCdList.tsx`: Tabelle (Name, Key-Prefix, Environment-Scope, Created, Last Used, Status), Create-Modal, Copy-to-Clipboard bei Erstellung, Revoke mit Bestätigung
    - `SettingsIndex.tsx`: Neuer Eintrag "CI/CD Integration"
    - `App.tsx`: Route `/settings/cicd`
  - **Tests**: Unit (CreateApiKeyHandler: valid, duplicate name, key format, hash correctness) + Integration (Endpoint-Tests: auth, CRUD, 401 ohne Auth)
  - Branch: `feature/cicd-api-key-management`
  - Abhängig von: Feature 2

- [x] **Feature 4: Redeploy Webhook** (PR #83 gegen integration)
  - **Application Layer** (`src/ReadyStackGo.Application/UseCases/Hooks/RedeployStack/`):
    - Command: StackName, EnvironmentId?, ApiKeyId
    - Handler-Logik:
      1. EnvironmentId ermitteln (Request oder API Key Claim `env_id`)
      2. Deployment per StackName + EnvironmentId finden (`IDeploymentRepository`)
      3. Validieren: Status == Running (nur laufende Stacks redeployen)
      4. Gespeicherte Parameter extrahieren (StackId, Variables aus Deployment)
      5. `DeployStackCommand` via MediatR senden (gleiche Parameter, kein SessionId)
      6. Ergebnis zurückgeben (deploymentId, success, message)
  - **API Endpoint** (`src/ReadyStackGo.Api/Endpoints/Hooks/RedeployEndpoint.cs`):
    - `POST /api/hooks/redeploy` mit `[RequirePermission("Hooks", "Redeploy")]`
    - Request: `{ stackName: string, environmentId?: string }`
    - Response: `{ success: bool, deploymentId?: string, message?: string }`
    - EnvironmentId Fallback: aus API Key Claims (`env_id`)
  - **Tests**: Unit (Running → Success, Unknown stack → NotFound, Failed/Removed → Error, korrekte Variable-Übernahme) + Integration (API Key Auth OK, JWT → 403, ohne Auth → 401)
  - Branch: `feature/cicd-redeploy-webhook`
  - Abhängig von: Feature 3

- [x] **Feature 5: Upgrade Webhook** (PR #84 gegen integration)
  - **Application Layer** (`src/ReadyStackGo.Application/UseCases/Hooks/UpgradeViaHook/`):
    - Command: StackName, TargetVersion, EnvironmentId?, Variables?, ApiKeyId
    - Handler-Logik:
      1. Deployment per StackName finden, Status == Running validieren
      2. Zielversion im Katalog suchen (`IProductSourceService` / `IProductCache.GetProductVersions()`)
      3. StackId für Zielversion ermitteln
      4. `UpgradeStackCommand` via MediatR senden
      5. Ergebnis mit previousVersion und newVersion zurückgeben
  - **API Endpoint** (`src/ReadyStackGo.Api/Endpoints/Hooks/UpgradeEndpoint.cs`):
    - `POST /api/hooks/upgrade` mit `[RequirePermission("Hooks", "Upgrade")]`
    - Request: `{ stackName: string, targetVersion: string, environmentId?: string, variables?: Record<string, string> }`
    - Response: `{ success: bool, deploymentId?: string, previousVersion?: string, newVersion?: string, message?: string }`
  - **Tests**: Unit (Running → Success, Unknown stack, Non-Running, Version not in catalog, Same version, Downgrade → Error, Variable merge) + Integration (API Key Auth, Endpoint-Tests)
  - Branch: `feature/cicd-upgrade-webhook`
  - Abhängig von: Feature 3

- [x] **Feature 6: Catalog Sync Webhook** (PR #85 gegen integration)
  - **API Endpoint** (`src/ReadyStackGo.Api/Endpoints/Hooks/SyncSourcesEndpoint.cs`):
    - `POST /api/hooks/sync-sources` mit `[RequirePermission("Hooks", "SyncSources")]`
    - Nutzt bestehenden `SyncStackSourcesCommand` (kein neuer Application Layer Code)
    - Response: `{ success: bool, stacksLoaded: number, sourcesSynced: number, message?: string }`
  - **Tests**: Integration (API Key Auth, Sync-Ergebnis)
  - Branch: `feature/cicd-sync-webhook`
  - Abhängig von: Feature 3

- [x] **Dokumentation & Website**
  - `docs/CI-CD/Pipeline-Integration.md` aktualisiert: API Key Erstellung, Redeploy/Upgrade/Sync Workflows
  - `docs/CI-CD/Pipeline-Examples.md` neu: GitHub Actions + Azure DevOps + curl Beispiele
  - Public Website Docs (DE/EN): `ci-cd-integration.md`
  - Roadmap v0.19 als Released markiert

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## PR-Abhängigkeiten

```
Feature 1 (Domain) → Feature 2 (Auth) → Feature 3 (CRUD + UI)
                                            ↓
                                   Feature 4 (Redeploy) ─┐
                                   Feature 5 (Upgrade)  ─┼→ Docs → Phase abschließen
                                   Feature 6 (Sync)     ─┘
```

Feature 4, 5, 6 können parallel nach Feature 3 implementiert werden.

## Bekannte Herausforderungen

1. **SQLite Schema-Migration**: `EnsureCreated()` legt neue Tabellen bei bestehender DB nicht an. Prüfen ob manuelles `CREATE TABLE IF NOT EXISTS` beim App-Start nötig ist oder ob die DB beim ersten Start mit neuer Version automatisch erweitert wird.
2. **Image Pull bei Redeploy**: Bestehende `DeploymentEngine` muss verifiziert werden, dass bei `docker create` frische Images gezogen werden (`--pull always` oder equivalentes Verhalten). Sonst könnte Redeploy gecachte Images nutzen.
3. **API Key Usage-Tracking**: SaveChanges bei jedem Request könnte bei hohem Traffic problematisch sein. Für v0.19 akzeptabel, ggf. später entkoppeln (in-memory counter mit periodischem Flush).
4. **Multi-Scheme Auth in FastEndpoints**: PolicyScheme muss gründlich getestet werden – bestehende JWT-Endpoints dürfen nicht brechen.

## Offene Punkte

- [x] Verifizieren ob `DeploymentEngine` bei Redeploy frische Images pullt (oder gecachte nutzt)
  - **Ergebnis**: `PullImageAsync` nutzt Docker's `CreateImageAsync` (= `docker pull`), kontaktiert Registry und lädt neue Layer. Funktioniert korrekt für CI/CD Redeploys.
- [x] Prüfen wie SQLite-Schema bei bestehenden Installationen erweitert wird (neue ApiKeys-Tabelle)
  - **Entscheidung**: `EnsureCreated()` beibehalten. Für pre-v1.0 akzeptabel – bestehende User löschen DB bei Upgrade.
- [x] Klären ob API Keys auch für reguläre REST-Endpoints (nicht nur Hooks) nutzbar sein sollen
  - **Entscheidung**: Nur für `/api/hooks/*` Endpoints. Klarere Security-Grenze, reguläre Endpoints erfordern JWT.

## Typischer Pipeline-Flow

### Dev/Test: Redeploy (frische Images)
```bash
curl -X POST https://rsgo.example.com/api/hooks/redeploy \
  -H "X-Api-Key: rsgo_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6" \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project"}'
```

### Formales Release: Sync + Upgrade
```bash
# 1. Katalog-Quellen synchronisieren (nach Git-Push des aktualisierten Manifests)
curl -X POST https://rsgo.example.com/api/hooks/sync-sources \
  -H "X-Api-Key: rsgo_..."

# 2. Upgrade auf neue Version
curl -X POST https://rsgo.example.com/api/hooks/upgrade \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project", "targetVersion": "6.5.0"}'
```
