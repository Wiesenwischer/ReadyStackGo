
# ReadyStackGo – Technische Spezifikation

## Inhaltsverzeichnis
1. API-Übersicht  
2. Endpunkt-Spezifikation  
3. Datenmodelle  
4. Commands & Queries  
5. Services & Schnittstellen  
6. Wizard API & State Machine  
7. Deployment Engine – Ablauf  
8. Manifest-Schema (formal)  
9. Docker-Integration  
10. UI-API-Contract

---

# 1. API-Übersicht

ReadyStackGo stellt eine klar definierte HTTP-API bereit.  
Alle Endpunkte sind unter `/api/v1/` erreichbar.

### Authentifizierung
- Während Wizard: **keine Auth**
- Danach:  
  - Local Login (JWT oder Cookie)  
  - Optional OIDC  
  - Rollen: `admin`, `operator`

### Standard-Response
```json
{
  "success": true,
  "message": "optional",
  "data": {}
}
```

### Fehler-Response
```json
{
  "success": false,
  "message": "Fehlerbeschreibung",
  "errorCode": "XYZ_ERROR"
}
```


# 2. Endpunkt-Spezifikation

Dieses Kapitel beschreibt **alle API-Endpunkte** im Detail.  
Jeder Endpunkt enthält:

- Pfad  
- Methode  
- Rollenberechtigung  
- Request-Body  
- Response-Body  
- Fehlercodes  

---

## 2.1 Container-Endpunkte

### **GET /api/v1/containers**
Listet alle Container des Hosts.

**Rollen:** admin, operator  
**Auth:** erforderlich  
**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "string",
      "name": "string",
      "image": "string",
      "state": "running|exited|paused",
      "created": "2025-03-10T12:00:00Z",
      "ports": [
        { "private": 8080, "public": 8443, "protocol": "tcp" }
      ]
    }
  ]
}
```

---

### **POST /api/v1/containers/start**
Startet einen Container.

**Body:**
```json
{ "id": "string" }
```

**Rollen:** admin, operator

---

### **POST /api/v1/containers/stop**
Stoppt einen Container.

**Body:**
```json
{ "id": "string" }
```

**Rollen:** admin, operator

---

## 2.2 Wizard API

### **GET /api/v1/wizard/status**
Liefert den aktuellen Status des Setup-Wizards.

```json
{
  "success": true,
  "data": {
    "state": "NotStarted|AdminCreated|OrganizationSet|ConnectionsSet|Installed"
  }
}
```

---

### **POST /api/v1/wizard/admin**
Legt den ersten Admin-Benutzer an.

**Body:**
```json
{
  "username": "string",
  "password": "string"
}
```

Antwort:
```json
{ "success": true }
```

---

### **POST /api/v1/wizard/organization**
Legt die Organisation an.

**Body:**
```json
{
  "id": "string",
  "name": "string"
}
```

---

### **POST /api/v1/wizard/connections**
Setzt globale Verbindungen.

```json
{
  "transport": "string",
  "persistence": "string",
  "eventStore": "string?"
}
```

---

### **POST /api/v1/wizard/install**
Installiert den Stack anhand eines Manifests.

Antwort:
```json
{
  "success": true,
  "data": {
    "installedVersion": "4.3.0"
  }
}
```

---

## 2.3 Release Management

### **GET /api/v1/releases**
Listet alle verfügbaren Manifeste.

---

### **GET /api/v1/releases/current**
Liefert den installierten Stand.

---

### **POST /api/v1/releases/{version}/install**
Installiert das angegebene Manifest.

Fehlercodes:
- `MANIFEST_NOT_FOUND`
- `DEPLOYMENT_FAILED`
- `INCOMPATIBLE_VERSION`

---

## 2.4 Admin API

### TLS

#### **GET /api/v1/admin/tls**
Zeigt TLS-Status an.

#### **POST /api/v1/admin/tls/upload**
Upload eines Custom-Zertifikats (multipart).

---

### Feature Flags

#### **GET /api/v1/admin/features**
#### **POST /api/v1/admin/features**

---

### Contexts

#### **GET /api/v1/admin/contexts**
#### **POST /api/v1/admin/contexts**

Simple/Advanced Mode.

---

### Security (optional später)

#### **POST /api/v1/admin/security/oidc**
#### **POST /api/v1/admin/security/local-admin**

---



# 3. Datenmodelle (Domain & DTO)

Dieses Kapitel enthält alle **Datenmodelle**, die ReadyStackGo benötigt.  
Sie sind in drei Kategorien aufgeteilt:

- **Domain-Modelle** – interne Geschäftsobjekte  
- **DTOs** – API-Ein- und Ausgabe  
- **Config-Modelle** – Objekte, die JSON-Konfigurationsdateien repräsentieren  

---

## 3.1 Domain Modelle

### **ContainerInfo**
Repräsentiert einen Docker-Container auf dem Host.

```csharp
public sealed class ContainerInfo
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Image { get; init; }
    public string State { get; init; }
    public DateTime Created { get; init; }
    public List<PortMapping> Ports { get; init; }
}

public sealed class PortMapping
{
    public int Private { get; init; }
    public int? Public { get; init; }
    public string Protocol { get; init; }
}
```

---

### **ReleaseStatus**
Repräsentiert die aktuell installierte Version.

```csharp
public sealed class ReleaseStatus
{
    public string InstalledStackVersion { get; init; }
    public Dictionary<string, string> InstalledContexts { get; init; }
    public DateTime InstallDate { get; init; }
}
```

---

### **DeploymentPlan**
Beschreibt, welche Schritte nötig sind, um ein Manifest zu installieren.

```csharp
public sealed class DeploymentPlan
{
    public List<DeploymentAction> Actions { get; init; }
}

public sealed class DeploymentAction
{
    public string Type { get; init; } // stop | remove | create | start
    public string ContextName { get; init; }
}
```

---

## 3.2 DTOs (API Contracts)

### **ContainerDto**

```csharp
public sealed class ContainerDto
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Image { get; init; }
    public string State { get; init; }
    public DateTime Created { get; init; }
    public IEnumerable<PortMappingDto> Ports { get; init; }
}

public sealed class PortMappingDto
{
    public int Private { get; init; }
    public int? Public { get; init; }
    public string Protocol { get; init; }
}
```

---

### **WizardStatusDto**

```csharp
public sealed class WizardStatusDto
{
    public string State { get; init; }
}
```

---

### **InstallResultDto**

```csharp
public sealed class InstallResultDto
{
    public string InstalledVersion { get; init; }
}
```

---

## 3.3 Config Model (JSON Files)

### **SystemSettings**

```csharp
public sealed class SystemSettings
{
    public OrganizationInfo Organization { get; init; }
    public string BaseUrl { get; init; }
    public int HttpPort { get; init; }
    public int HttpsPort { get; init; }
    public string DockerNetwork { get; init; }
    public string Mode { get; init; }
    public string WizardState { get; init; }
}
```

---

### **SecuritySettings**

```csharp
public sealed class SecuritySettings
{
    public string AuthMode { get; init; }
    public LocalAdminSettings LocalAdmin { get; init; }
    public OidcSettings ExternalProvider { get; init; }
    public bool LocalAdminFallbackEnabled { get; init; }
}
```

---

### **TlsSettings**

```csharp
public sealed class TlsSettings
{
    public string TlsMode { get; init; }
    public string CertificatePath { get; init; }
    public string CertificatePassword { get; init; }
    public int HttpsPort { get; init; }
    public bool HttpEnabled { get; init; }
    public string TerminatingContext { get; init; }
}
```

---

### **ContextSettings**

```csharp
public sealed class ContextSettings
{
    public string Mode { get; init; } // Simple | Advanced
    public Dictionary<string, string> GlobalConnections { get; init; }
    public Dictionary<string, ContextConnectionOverride> Contexts { get; init; }
}

public sealed class ContextConnectionOverride
{
    public Dictionary<string, string> Connections { get; init; }
}
```

---

### **FeatureSettings**

```csharp
public sealed class FeatureSettings
{
    public Dictionary<string, bool> Features { get; init; }
}
```

---

### **ReleaseFile**

```csharp
public sealed class ReleaseFile
{
    public string InstalledStackVersion { get; init; }
    public Dictionary<string, string> InstalledContexts { get; init; }
    public DateTime InstallDate { get; init; }
}
```

---



# 4. Commands & Queries

ReadyStackGo verwendet ein **Dispatcher Pattern** anstelle von MediatR.  
Alle Aktionen laufen über:

- **Commands** (schreiben/zustandsverändernd)
- **Queries** (lesen)

Jeder Command/Query wird über den `IDispatcher` ausgeführt.

---

## 4.1 Commands

### **StartContainerCommand**
Startet einen Docker-Container.

```csharp
public sealed record StartContainerCommand(string Id) : ICommand<bool>;
```

#### Handler:
```csharp
public sealed class StartContainerHandler 
    : ICommandHandler<StartContainerCommand, bool>
{
    private readonly IDockerService _docker;

    public StartContainerHandler(IDockerService docker) 
        => _docker = docker;

    public Task<bool> HandleAsync(StartContainerCommand cmd, CancellationToken ct)
        => _docker.StartAsync(cmd.Id);
}
```

---

### **StopContainerCommand**
```csharp
public sealed record StopContainerCommand(string Id) : ICommand<bool>;
```

---

### **InstallStackCommand**
Installiert ein Manifest.

```csharp
public sealed record InstallStackCommand(string StackVersion)
    : ICommand<InstallResultDto>;
```

Handler führt aus:

1. Manifest laden  
2. Version prüfen  
3. DeploymentPlan erzeugen  
4. Deployment ausführen  
5. rsgo.release.json aktualisieren  

---

## 4.2 Queries

### **ListContainersQuery**
```csharp
public sealed record ListContainersQuery(bool IncludeStopped)
    : IQuery<List<ContainerInfo>>;
```

---

### **GetReleaseStatusQuery**
```csharp
public sealed record GetReleaseStatusQuery() 
    : IQuery<ReleaseStatus>;
```

---

## 4.3 Dispatcher Interface

```csharp
public interface IDispatcher
{
    Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
    Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
```

---

## 4.4 Dispatcher Implementierung

```csharp
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _sp;

    public Dispatcher(IServiceProvider sp)
        => _sp = sp;

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> cmd, CancellationToken ct)
    {
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(cmd.GetType(), typeof(TResult));
        dynamic handler = _sp.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)cmd, ct);
    }

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct)
    {
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        dynamic handler = _sp.GetRequiredService(handlerType);
        return handler.HandleAsync((dynamic)query, ct);
    }
}
```

---

## 4.5 Vorteile des Dispatchers

- keine Reflection-Magic wie MediatR
- volle Kompilierbarkeit aller Handler
- transparente Auflösung per DI
- eigene Policies / Pipelines leicht integrierbar
- 100% kompatibel mit FastEndpoints

---



# 5. Services & Schnittstellen

Dieses Kapitel beschreibt die wichtigsten internen Services von ReadyStackGo.  
Jeder Service folgt dem Interface-First-Prinzip und hat eine klar definierte Verantwortung.

---

# 5.1 IDockerService

Abstrahiert die Docker API.

```csharp
public interface IDockerService
{
    Task<IReadOnlyList<ContainerInfo>> ListAsync(bool includeStopped);
    Task<bool> StartAsync(string id);
    Task<bool> StopAsync(string id);
    Task<bool> RemoveAsync(string name);
    Task<bool> CreateAndStartAsync(ContainerCreateModel model);
    Task<bool> NetworkEnsureExistsAsync(string name);
}
```

### ContainerCreateModel

```csharp
public sealed class ContainerCreateModel
{
    public string Name { get; init; }
    public string Image { get; init; }
    public Dictionary<string, string> Env { get; init; }
    public IEnumerable<int> ExposedPorts { get; init; }
    public string Network { get; init; }
    public string RestartPolicy { get; init; } = "unless-stopped";
}
```

---

# 5.2 IConfigStore

Verwaltet alle Config-Dateien (rsgo-config Volume).

```csharp
public interface IConfigStore
{
    Task<SystemSettings> LoadSystemAsync();
    Task SaveSystemAsync(SystemSettings settings);

    Task<SecuritySettings> LoadSecurityAsync();
    Task SaveSecurityAsync(SecuritySettings settings);

    Task<TlsSettings> LoadTlsAsync();
    Task SaveTlsAsync(TlsSettings settings);

    Task<ContextSettings> LoadContextsAsync();
    Task SaveContextsAsync(ContextSettings settings);

    Task<FeatureSettings> LoadFeaturesAsync();
    Task SaveFeaturesAsync(FeatureSettings settings);

    Task<ReleaseFile> LoadReleaseAsync();
    Task SaveReleaseAsync(ReleaseFile file);
}
```

**Umsetzung:** Dateien werden immer komplett ersetzt (Write-All), niemals gepatcht.

---

# 5.3 ITlsService

Erzeugt Zertifikate, prüft Zertifikate und lädt Custom-Zertifikate.

```csharp
public interface ITlsService
{
    Task<TlsGenerateResult> GenerateSelfSignedAsync(string commonName);
    Task<bool> ValidateCustomCertificateAsync(string path, string password);
    Task<bool> InstallCustomCertificateAsync(string path, string password);
}
```

### TlsGenerateResult

```csharp
public sealed class TlsGenerateResult
{
    public string Path { get; init; }
    public string Password { get; init; }
}
```

---

# 5.4 IManifestProvider

Lädt Release-Manifeste und validiert deren Schema.

```csharp
public interface IManifestProvider
{
    Task<IReadOnlyList<ReleaseManifest>> LoadAllAsync();
    Task<ReleaseManifest> LoadVersionAsync(string version);
    Task<bool> ExistsAsync(string version);
}
```

---

# 5.5 IDeploymentEngine

Führt ein Manifest vollständig aus.

```csharp
public interface IDeploymentEngine
{
    Task<DeploymentResult> InstallAsync(ReleaseManifest manifest);
}
```

### DeploymentResult

```csharp
public sealed class DeploymentResult
{
    public bool Success { get; init; }
    public string Error { get; init; }
    public ReleaseFile UpdatedRelease { get; init; }
}
```

---

# 5.6 IEnvVarService

Generiert Environment-Variablen für jeden Kontext.

```csharp
public interface IEnvVarService
{
    Task<Dictionary<string, string>> GenerateForContextAsync(
        string contextName,
        ReleaseManifest manifest
    );
}
```

Das Ergebnis setzt sich zusammen aus:

- system variables  
- feature flags  
- context connections  
- manifest env overrides  

---

# 5.7 IWebhookService (Future)

Wird für CI/CD-Trigger und externe Events genutzt.

---

# 5.8 IService Locator (verboten)

ReadyStackGo nutzt konsequent DI und niemals einen globalen Service Locator.

---



# 6. Wizard API & State Machine

Der ReadyStackGo-Wizard basiert vollständig auf einer klar definierten **State Machine**.  
Die API steuert ausschließlich Übergänge dieser State Machine.

---

## 6.1 Wizard States

Der Wizard kennt folgende Zustände:

| State | Beschreibung |
|-------|--------------|
| `NotStarted` | rsgo.config existiert nicht oder ist leer |
| `AdminCreated` | Der Administrator wurde angelegt |
| `OrganizationSet` | Organisation wurde definiert |
| `ConnectionsSet` | Verbindungen wurden gespeichert |
| `Installed` | Stack ist installiert, Wizard deaktiviert |

Alle Zustände werden in `rsgo.system.json` gespeichert:

```json
{
  "wizardState": "OrganizationSet"
}
```

---

## 6.2 Zustandslogik

### Startbedingungen:
- Wizard ist aktiv, wenn `wizardState != Installed`

### Übergänge:

```
NotStarted → AdminCreated → OrganizationSet → ConnectionsSet → Installed
```

### Ungültige Übergänge erzeugen Fehler:

- z. B. `ConnectionsSet → AdminCreated` ist verboten

---

## 6.3 API-Endpunkte des Wizards

### 1. **GET /api/v1/wizard/status**
Gibt den aktuellen Zustand zurück.

---

### 2. **POST /api/v1/wizard/admin**
Legt den ersten Admin an.

Validierungen:
- Username darf nicht leer sein
- Passwort muss Mindestlänge erfüllen

Ergebnis:
- wizardState = `AdminCreated`

---

### 3. **POST /api/v1/wizard/organization**
Speichert:

- Organization ID  
- Organization Name  

Ergebnis:
- wizardState = `OrganizationSet`

---

### 4. **POST /api/v1/wizard/connections**
Speichert die grundlegenden Verbindungen:

- Transport
- Persistence
- EventStore (optional)

Ergebnis:
- wizardState = `ConnectionsSet`

---

### 5. **POST /api/v1/wizard/install**
Installiert den vollständigen Stack.

Ablauf:
1. Manifest auswählen
2. Deploymentplan erzeugen
3. Deployment Engine ausführen
4. Release-Datei speichern
5. wizardState = `Installed`

---

## 6.4 Wizard Fehlercodes

| Code | Bedeutung |
|------|-----------|
| `WIZARD_INVALID_STATE` | API wurde im falschen Zustand aufgerufen |
| `WIZARD_ALREADY_COMPLETED` | Wizard ist bereits abgeschlossen |
| `WIZARD_STEP_INCOMPLETE` | Vorheriger Schritt fehlt |
| `DEPLOYMENT_FAILED` | Manifest konnte nicht installiert werden |

---

## 6.5 Beispiel: Request Flow

1. Benutzer öffnet `/wizard`  
2. UI ruft: `GET /wizard/status`  
3. Anzeige des aktuellen Schritts  
4. Benutzer sendet Formulardaten  
5. API speichert Config  
6. Wizard geht zum nächsten Schritt  

Nach Schritt 5:
- Weiterleitung zur Login-Seite

---

## 6.6 Wizard UI (für spätere Implementierung)

4-seitiger Stepper:

1. Admin  
2. Organisation  
3. Verbindungen  
4. Installation

Wizard ist **Fullscreen**, um Ablenkungen zu vermeiden.

---



# 7. Deployment Engine – Ablauf (Detail)

Die Deployment Engine ist der zentrale Mechanismus, mit dem ReadyStackGo  
einen vollständigen Stack anhand eines Release-Manifests installiert, aktualisiert  
oder validiert. Dieses Kapitel beschreibt die komplette interne Logik.

---

## 7.1 Gesamtüberblick (High-Level Flow)

Der Ablauf einer Installation erfolgt in 10 Schritten:

1. Manifest laden  
2. Versions- und Schema-Prüfung  
3. Alte Containerliste sammeln  
4. DeploymentPlan erzeugen  
5. Docker Netzwerk sicherstellen  
6. Kontextweise Aktionen ausführen  
7. Gateway zuletzt deployen  
8. Healthchecks durchführen (optional / später)  
9. Release-Datei aktualisieren  
10. Ergebnis an API zurückgeben

---

## 7.2 Erzeugung des DeploymentPlans

Der DeploymentPlan beschreibt exakt alle Operationen, die notwendig sind,  
um das Release zu installieren. Beispiel:

```json
[
  { "type": "stop", "context": "project" },
  { "type": "remove", "context": "project" },
  { "type": "create", "context": "project" },
  { "type": "start", "context": "project" }
]
```

### Regeln:

- Jeder Kontext wird vollständig ersetzt → kein In-Place Update  
- Gateway-Kontext immer als letzter Schritt  
- Interne Kontexte zuerst  
- Exposed Ports nur am Gateway  

---

## 7.3 Docker Netzwerk

Vor jedem Deployment wird sichergestellt, dass das Netzwerk existiert:

```csharp
await _docker.NetworkEnsureExistsAsync(system.DockerNetwork);
```

Name:  
```
rsgo-net
```

Alle Container werden darin gestartet.

---

## 7.4 EnvVar Generierung

Für jeden Kontext ruft die Engine:

```csharp
var env = await _envVarService.GenerateForContextAsync(contextName, manifest);
```

Dieses Objekt enthält:

- `RSGO_ORG_ID`
- `RSGO_STACK_VERSION`
- `RSGO_FEATURE_*`
- `RSGO_CONNECTION_*`
- Manifest Overrides

Beispiel:

```json
{
  "RSGO_ORG_ID": "kunde-a",
  "RSGO_CONNECTION_persistence": "Server=sql;Database=ams",
  "ROUTE_PROJECT": "http://ams-project"
}
```

---

## 7.5 Container Lifecycle – technische Schritte

### **1. Stop**
Stoppt laufende Container.

```csharp
await _docker.StopAsync(containerName);
```

### **2. Remove**
Entfernt Container vollständig.

```csharp
await _docker.RemoveAsync(containerName);
```

### **3. Create**
Erstellt Container anhand des Manifests.

```csharp
await _docker.CreateAndStartAsync(new ContainerCreateModel {
    Name = contextName,
    Image = imageTag,
    Env = envVars,
    Network = network,
    ExposedPorts = ports
});
```

### **4. Start**
Startet Container (falls nicht automatisch gestartet).

```csharp
await _docker.StartAsync(containerName);
```

---

## 7.6 Gateway Deployment (Special Handling)

Der Gateway-Kontext ist besonders:

- bekommt TLS-Parameter
- ist öffentlich erreichbar
- wird daher **immer zuletzt** deployed

### Beispiel:

```json
"gateway": {
  "context": "edge-gateway",
  "protocol": "https",
  "publicPort": 8443,
  "internalHttpPort": 8080
}
```

Der Container wird mit diesen Ports erstellt:

- **exposed:** 8080  
- **published:** 8443  

---

## 7.7 Fehlerbehandlung

### Hard failures
Stoppen das Deployment vollständig:

- Image kann nicht geladen werden  
- Container kann nicht erstellt werden  
- Netzwerkfehler  
- Manifest ungültig  

Error Codes:

| Code | Beschreibung |
|------|--------------|
| `DEPLOYMENT_FAILED` | Allgemeiner Fehler |
| `DOCKER_NETWORK_ERROR` | Netzwerk konnte nicht erzeugt werden |
| `CONTAINER_START_FAILED` | Container kann nicht starten |
| `INVALID_MANIFEST` | Schema ungültig |

### Soft failures
Nur Warnungen (später in der UI sichtbar):

- Healthcheck nicht OK  
- Container benötigt länger zum Starten  

---

## 7.8 Release-Datei aktualisieren

Nach erfolgreicher Installation:

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3",
    "discussion": "3.5.9"
  },
  "installDate": "2025-04-12T10:22:00Z"
}
```

---

## 7.9 Rückgabe an API

Ergebnis:

```json
{
  "success": true,
  "data": {
    "installedVersion": "4.3.0"
  }
}
```

Bei Fehler:

```json
{
  "success": false,
  "errorCode": "DEPLOYMENT_FAILED",
  "message": "Container 'project' konnte nicht gestartet werden."
}
```

---

# → Ende von Block 7/20


# 8. Manifest-Schema (formal)

Ein Manifest ist die zentrale Datei, welche den gesamten zu installierenden Stack beschreibt.  
Dieses Kapitel definiert das **vollständige JSON-Schema**, welches ReadyStackGo für Manifeste verwendet.

---

## 8.1 Hauptstruktur

Ein Manifest besteht aus folgenden Hauptelementen:

```json
{
  "manifestVersion": "string",
  "stackVersion": "string",
  "schemaVersion": 1,
  "releaseDate": "2025-03-01",
  "gateway": { ... },
  "contexts": { ... },
  "features": { ... },
  "metadata": { ... }
}
```

---

## 8.2 JSON Schema (komplett)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ReadyStackGo Manifest",
  "type": "object",
  "required": [
    "manifestVersion",
    "stackVersion",
    "schemaVersion",
    "contexts"
  ],
  "properties": {
    "manifestVersion": { "type": "string" },
    "stackVersion": { "type": "string" },
    "schemaVersion": { "type": "number" },
    "releaseDate": { "type": "string", "format": "date" },

    "gateway": {
      "type": "object",
      "properties": {
        "context": { "type": "string" },
        "protocol": { "type": "string", "enum": ["http", "https"] },
        "publicPort": { "type": "number" },
        "internalHttpPort": { "type": "number" }
      }
    },

    "contexts": {
      "type": "object",
      "patternProperties": {
        "^[a-zA-Z0-9_-]+$": {
          "type": "object",
          "required": ["image", "version", "containerName"],
          "properties": {
            "image": { "type": "string" },
            "version": { "type": "string" },
            "containerName": { "type": "string" },
            "internal": { "type": "boolean" },
            "dependsOn": {
              "type": "array",
              "items": { "type": "string" }
            },
            "env": {
              "type": "object",
              "additionalProperties": { "type": "string" }
            },
            "ports": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "private": { "type": "number" },
                  "public": { "type": ["number", "null"] },
                  "protocol": {
                    "type": "string",
                    "enum": ["tcp", "udp"]
                  }
                }
              }
            }
          }
        }
      }
    },

    "features": {
      "type": "object",
      "patternProperties": {
        "^[a-zA-Z0-9_-]+$": {
          "type": "object",
          "properties": {
            "default": { "type": "boolean" },
            "description": { "type": "string" }
          }
        }
      }
    },

    "metadata": {
      "type": "object",
      "properties": {
        "description": { "type": "string" },
        "notes": { "type": "string" }
      }
    }
  }
}
```

---

## 8.3 Beispielmanifest (komplett & kommentiert)

```json
{
  "manifestVersion": "1.0.0",
  "stackVersion": "4.3.0",
  "schemaVersion": 12,
  "releaseDate": "2025-03-01",

  "gateway": {
    "context": "edge-gateway",
    "protocol": "https",
    "publicPort": 8443,
    "internalHttpPort": 8080
  },

  "contexts": {
    "project": {
      "image": "registry/ams.project-api",
      "version": "6.4.0",
      "containerName": "ams-project",
      "internal": true,
      "dependsOn": [],
      "env": {},
      "ports": []
    },
    "bffDesktop": {
      "image": "registry/ams.bff-desktop",
      "version": "1.3.0",
      "containerName": "ams-bff-desktop",
      "internal": false,
      "dependsOn": ["project"],
      "env": {
        "ROUTE_PROJECT": "http://ams-project"
      },
      "ports": []
    }
  },

  "features": {
    "newColorTheme": { "default": true },
    "discussionV2": { "default": false }
  },

  "metadata": {
    "description": "Full AMS Release 4.3.0",
    "notes": "Dieses Release enthält das neue Dashboard."
  }
}
```

---

## 8.4 Schema-Versionierung

### Regeln:
1. **schemaVersion** steigt nur, wenn Manifest-Struktur geändert wurde.  
2. Backwards-Kompatibilität wird möglichst erhalten.  
3. Alte Manifeste dürfen weiterhin installiert werden.  
4. Bei inkompatiblen Versionen wird Installation verweigert:

```json
{
  "success": false,
  "errorCode": "SCHEMA_INCOMPATIBLE"
}
```

---

## 8.5 Manifest-Speicherorte

ReadyStackGo sucht Manifeste:

1. **/manifests im Admin-Container**
2. später: über eine Registry (z. B. GitHub Releases, Azure DevOps Artifact Feed)

Der Name entspricht der Stack-Version:

```
manifest-4.3.0.json
manifest-4.4.1.json
```

---

# → Ende von Block 8/20


# 9. Docker-Integration (Detail)

ReadyStackGo integriert sich direkt in den Docker-Host des Kunden.  
Dies geschieht ausschließlich über den **Docker Socket**, der als Volume  
in den Admin-Container gemountet wird:

```
-v /var/run/docker.sock:/var/run/docker.sock
```

Dadurch erhält ReadyStackGo:

- vollen Zugriff auf Container  
- vollen Zugriff auf Images  
- Zugriff auf Netzwerke  
- Zugriff auf Logs  
- Zugriff auf Events  

Dies ist notwendig, um Stacks vollständig steuern zu können.

---

## 9.1 Docker.DotNet Bibliothek

ReadyStackGo nutzt die offizielle Bibliothek:

```xml
<PackageReference Include="Docker.DotNet" Version="3.125.5" />
```

Diese kommuniziert direkt mit dem Docker Socket via HTTP.

---

## 9.2 Container Lifecycle intern

Für jeden Kontext-Container werden folgende Schritte ausgeführt:

### 1. Container stoppen
```csharp
await client.Containers.StopContainerAsync(id, new ContainerStopParameters());
```

### 2. Container entfernen
```csharp
await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });
```

### 3. Container erstellen
```csharp
await client.Containers.CreateContainerAsync(new CreateContainerParameters {
    Image = model.Image,
    Name = model.Name,
    Env = model.Env.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),
    HostConfig = new HostConfig {
        NetworkMode = model.Network,
        RestartPolicy = new RestartPolicy { Name = model.RestartPolicy }
    }
});
```

### 4. Container starten
```csharp
await client.Containers.StartContainerAsync(id, null);
```

---

## 9.3 Netzwerkverwaltung

### Erstellen oder sicherstellen:

```csharp
await client.Networks.CreateNetworkAsync(new NetworksCreateParameters {
    Name = "rsgo-net"
});
```

Falls bereits vorhanden, wird stillschweigend weitergemacht.

---

## 9.4 Ports & Mappings

Interne Ports werden immer gesetzt:

```json
"ports": [
  { "private": 8080, "public": null, "protocol": "tcp" }
]
```

Gateway setzt zusätzlich öffentliche Ports:

```json
"ports": [
  { "private": 8080, "public": 8443, "protocol": "tcp" }
]
```

---

## 9.5 Logs

ReadyStackGo kann später Live-Logs streamen:

```csharp
await client.Containers.GetContainerLogsAsync(id, false, new ContainerLogsParameters {
    ShowStdout = true,
    ShowStderr = true,
    Follow = true
});
```

Dies ist jedoch nicht Teil von Version 1.0.

---

## 9.6 Docker Events (Future)

Docker Events ermöglichen:

- Erkennung von Container-Crashes  
- Monitoring  
- Auto-Healing später  

Wird in einer späteren Version eingebaut.

---

## 9.7 Sicherheitsaspekte

Der Zugriff auf den Docker Socket ist potentiell gefährlich.  
Daher wichtig:

- Admin-Container wird über HTTPS abgesichert  
- Login-Rollen steuern Zugriff  
- Kein direkter Shell-Zugriff  
- Container können nicht exec'd werden  

---

# → Ende von Block 9/20


# 10. UI–API Contract

Dieses Kapitel definiert den vollständigen **Vertrag zwischen dem React-Frontend (Tailwind + TailAdmin)**  
und der ReadyStackGo API.  

Die UI arbeitet **strictly typed** über TypeScript-Interfaces, die exakt den DTOs der API entsprechen.

---

# 10.1 Grundprinzip: Thin Client, Thick Server

Die UI:

- enthält **keine Logik**, die Systemzustände mutiert  
- ruft ausschließlich definierte Endpunkte auf  
- reagiert nur auf die Wizard-State Machine  
- liest Container-Status, Release-Infos, Features, TLS-Infos usw.  

Die API enthält **100% der Geschäftslogik**.

---

# 10.2 HTTP-Konventionen

Alle Endpunkte:

- pfadbasiert (`/api/v1/...`)
- Rückgabeformat: JSON
- Fehler als:

```json
{
  "success": false,
  "errorCode": "XYZ",
  "message": "Fehlerbeschreibung"
}
```

UI muss **errorCode** interpretieren, nicht message.

---

# 10.3 TypeScript DTOs (Frontend-Side)

## 10.3.1 Container DTO

```ts
export interface ContainerDto {
  id: string;
  name: string;
  image: string;
  state: "running" | "exited" | "paused";
  created: string; // ISO date
  ports: PortMappingDto[];
}

export interface PortMappingDto {
  private: number;
  public: number | null;
  protocol: "tcp" | "udp";
}
```

---

## 10.3.2 Wizard

```ts
export interface WizardStatusDto {
  state: 
    | "NotStarted"
    | "AdminCreated"
    | "OrganizationSet"
    | "ConnectionsSet"
    | "Installed";
}
```

---

## 10.3.3 Release Status

```ts
export interface ReleaseStatusDto {
  installedStackVersion: string;
  installedContexts: Record<string, string>;
  installDate: string;
}
```

---

## 10.3.4 TLS Status DTO

```ts
export interface TlsStatusDto {
  tlsMode: "SelfSigned" | "Custom";
  certificatePath: string;
  httpEnabled: boolean;
  httpsPort: number;
  terminatingContext: string;
}
```

---

## 10.3.5 Feature Flags

```ts
export interface FeatureFlagsDto {
  features: Record<string, boolean>;
}
```

---

# 10.4 UI Requests

## 10.4.1 Login

```ts
POST /api/v1/auth/login

Body:
{
  username: string;
  password: string;
}

Response:
{
  success: true;
  token: string;
}
```

---

## 10.4.2 Wizard Calls

Alle Wizard-Calls haben **keine Rückgabedaten** außer success.

Beispiel:

```ts
POST /api/v1/wizard/admin
{
  username: "admin",
  password: "xyz123..."
}
```

---

# 10.5 UI Seitenstruktur

## 10.5.1 Login Page

- Username
- Passwort
- POST /auth/login
- Token speichern im LocalStorage oder Cookie

---

## 10.5.2 Dashboard

Die UI ruft:

- `/api/v1/containers`  
- `/api/v1/releases/current`  

und zeigt:

- Container Status  
- Stack Version  
- Actions (nur admin)  

---

## 10.5.3 Containers Page

Aktionen:

- start/stop (operator, admin)
- logs (später)
- details

---

## 10.5.4 Releases Page

Aktionen:

- Versionen laden (`GET /releases`)
- Installation (`POST /releases/{version}/install`)

---

## 10.5.5 Feature Flags Page

- Liste aller Features
- Toggle-Switch
- POST `/admin/features`

---

## 10.5.6 TLS Page

- Anzeige des TLS-Status
- Zertifikats-Upload (PFX)
- POST `/admin/tls/upload`

---

## 10.5.7 Contexts Page

- Simple/Advanced Mode Schalter
- Globale Verbindungen
- Kontext-Overrides

---

# 10.6 Validierungsregeln (Frontend)

Das Frontend führt **nur minimale Validierung** durch:

- Pflichtfelder prüfen
- Formatprüfung (z. B. Port = Zahl)
- Feedback anzeigen bei Fehlern

Alle tiefergehenden Regeln liegen in der API.

---

# 10.7 Fehlerbehandlung

Die UI prüft:

```ts
if (!response.success) {
    switch (response.errorCode) {
        case "WIZARD_INVALID_STATE":
        case "DEPLOYMENT_FAILED":
        case "INVALID_MANIFEST":
            showError(response.message);
            break;
    }
}
```

---

# 10.8 Wizard UI Logik

### Regeln:

- UI zeigt immer den Schritt basierend auf `/wizard/status`
- keine Navigation durch den Benutzer möglich
- keine Rücksprünge
- nach Installation → Redirect `/login`

---

# 10.9 UI State Management

Empfehlung:

- Zustand über React Query / Zustand als "server state"
- minimale Verwendung von Redux oder Context API
- UI ist vollständig API-getrieben

---

# → Ende von Block 10/20


# 11. Authentifizierung & Autorisierung (Technische Details)

Dieses Kapitel beschreibt die komplette technische Implementierung der Sicherheitsschicht von ReadyStackGo.

---

# 11.1 Authentifizierungsmodi

ReadyStackGo unterstützt zwei Hauptmodi:

1. **Local Authentication (Default)**
2. **OpenID Connect – externer Identity Provider (später aktivierbar)**

---

## 11.1.1 Local Authentication

Der erste Benutzer (Admin) wird im Wizard angelegt.
Daten werden in `rsgo.security.json` gespeichert.

### Password Hash Format

```json
{
  "username": "admin",
  "passwordHash": "<base64>",
  "passwordSalt": "<base64>"
}
```

Empfohlener Algorithmus:

- PBKDF2-HMAC-SHA256  
- Iterationen: 210.000  
- Salt: 16–32 Bytes zufällig  
- Hash: 32–64 Bytes

---

## 11.1.2 Login Flow

### Request
```
POST /api/v1/auth/login
```

### Response
```json
{
  "success": true,
  "token": "<JWT>"
}
```

---

## 11.1.3 JWT Aufbau

Header:
```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

Claims:
```json
{
  "sub": "admin",
  "role": "admin",
  "exp": 1714579200
}
```

Secret:
- lokal gespeichert in `rsgo.security.json` oder intern generiert  
- später austauschbar über Admin UI  

---

# 11.2 Rollenmodell

Es gibt zwei Rollen:

| Rolle     | Beschreibung |
|-----------|--------------|
| **admin** | Vollzugriff auf alle Funktionen |
| **operator** | Darf Container starten/stoppen |

---

## 11.2.1 Rollen-Definition in Config

```json
{
  "roles": {
    "admin": {
      "canManageConfig": true,
      "canDeploy": true,
      "canRestartContainers": true
    },
    "operator": {
      "canManageConfig": false,
      "canDeploy": false,
      "canRestartContainers": true
    }
  }
}
```

---

# 11.3 Autorisierung in Endpoints

Jeder Endpoint definiert Rollen explizit:

```csharp
public override void Configure()
{
    Get("/api/containers");
    Roles("admin", "operator");
}
```

### Wizard-Endpoints:
- keine Authentifizierung  
- nicht erreichbar nach Abschluss des Wizards  

---

# 11.4 Externer Identity Provider (OIDC)

ReadyStackGo kann später über OIDC angebunden werden an:

- Keycloak  
- ams.identity  
- Azure AD (später)  

### Konfigurationsstruktur:

```json
{
  "externalProvider": {
    "authority": "https://identity.local",
    "clientId": "rsgo-admin-ui",
    "clientSecret": "<secret>",
    "adminRoleClaim": "role",
    "adminRoleValue": "rsgo-admin",
    "operatorRoleValue": "rsgo-operator"
  }
}
```

### Ablauf (zukünftig)

1. UI → Redirect zum IdP  
2. Login erfolgt beim IdP  
3. Token → ReadyStackGo  
4. Rollen extrahieren aus Claims  
5. Zugriff gewähren / verweigern  

---

# 11.5 Local Admin Fallback

Konfigurierbar:

```json
{
  "localAdminFallbackEnabled": true
}
```

Wenn aktiviert:

- Wenn IdP offline ist, bleibt lokaler Admin loginfähig  
- Falls deaktiviert → *nur* IdP erlaubt Logins  

---

# 11.6 HTTP Security

### HTTPS wird durch Gateway bereitgestellt

Gateway erhält:
- Zertifikat
- HTTPS-Port
- Exposed-Port

Intern kommuniziert alles über HTTP.

### Admin-Container selbst:
- kann per HTTPS erreichbar sein (für Setup)
- terminiert TLS bei sich selbst im Wizard-Modus  

---

# 11.7 Security Headers

Alle Responses enthalten:

- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection: 1`
- `Strict-Transport-Security` (falls https)

---

# 11.8 Anti-CSRF

Wenn JWT per Cookie:
- UI sendet X-CSRF-Header  
- Server prüft Token im Header und Cookie  

Aktuell: JWT via LocalStorage empfohlen.

---

# 11.9 Rate Limiting (Future)

Geplant:
- Default: 100 requests/min pro IP  
- Admin-Endpunkte restriktiver  

---

# → Ende von Block 11/20


# 12. Logging, Monitoring & Fehlerdiagnose

Dieses Kapitel beschreibt das Logging- und Monitoring-Konzept von ReadyStackGo,  
sowie wie Fehler erfasst, gespeichert und der UI bereitgestellt werden.

---

# 12.1 Logging im Admin-Container

ReadyStackGo nutzt standardmäßig:

- **Microsoft.Extensions.Logging**
- Ausgabe an **Console**
- Ausgabe an **FileLog** (optional, später)
- Strukturierte Logs über **Serilog** (planned)
- Log-Level konfigurierbar

Standard-Loglevel:

```
Information
Warning
Error
Critical
```

---

## 12.1.1 Log-Speicherort

Per Default:

```
/app/logs/rsgo-admin.log
```

Rotierende Logs (geplant):

- `rsgo-admin.log`
- `rsgo-admin.log.1`
- `rsgo-admin.log.2`

---

# 12.2 Logging im Deploymentprozess

Während des Deployments:

- jeder Schritt wird geloggt
- Fehler werden zusätzlich in ein separates Deployment-Log geschrieben
- UI kann später Deployment-Logs abrufen

Beispiel-Logeintrag:

```
[INFO] [Deployment] Starting context 'project' (image registry/ams.project-api:6.4.0)
[ERROR] [Docker] Failed to start container 'project': port already in use
```

---

# 12.3 UI Log Streaming (Future Feature)

Später soll folgende API existieren:

```
GET /api/v1/containers/{id}/logs?follow=true
```

Diese streamt:

- stdout
- stderr

---

# 12.4 Ereignisprotokoll (Event Log)

Ein internes EventLog speichert:

| Timestamp | Kategorie | Ereignis |
|----------|-----------|----------|
| 2025-03-11 08:12 | Deploy | Install stackVersion=4.3.0 |
| 2025-03-11 08:14 | TLS | Custom certificate uploaded |
| 2025-03-12 09:20 | Auth | Login failed for user admin |

API:

```
GET /api/v1/admin/events
```

---

# 12.5 Fehlercodes (global)

Jeder Fehler erhält einen eindeutigen Code, z. B.:

| Code | Beschreibung |
|------|--------------|
| `DEPLOYMENT_FAILED` | Fehler im Deploymentprozess |
| `INVALID_MANIFEST` | Manifest fehlerhaft |
| `SCHEMA_INCOMPATIBLE` | Manifest-Schema nicht kompatibel |
| `WIZARD_INVALID_STATE` | Wizard wurde in falscher Phase aufgerufen |
| `DOCKER_NETWORK_ERROR` | Docker Netzwerkfehler |
| `CONTAINER_START_FAILED` | Container konnte nicht starten |
| `AUTH_FAILED` | Login ungültig |
| `TLS_INVALID` | Zertifikat ungültig |

---

# 12.6 Fehlerbehandlung im Code

Beispiel für Deployment:

```csharp
try
{
    await _docker.StartAsync(contextName);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to start container {Context}", contextName);
    return new DeploymentResult {
        Success = false,
        Error = $"CONTAINER_START_FAILED: {ex.Message}"
    };
}
```

---

# 12.7 Healthchecks (Future)

Geplant:

- `/health` Endpunkte der Dienste regelmäßig prüfen
- Ergebnisse in der UI anzeigen
- Optionale Alerts im Admin-UI

---

# → Ende von Block 12/20


# 13. Deployment-Pläne & Reihenfolge-Logik (Tiefe Details)

Dieses Kapitel beschreibt die interne Logik, mit der ReadyStackGo bestimmt,
**in welcher Reihenfolge Container installiert, entfernt, oder aktualisiert werden**.

Dies ist entscheidend, damit der Stack deterministisch, sicher und vorhersagbar
ausgerollt werden kann.

---

# 13.1 Grundprinzipien

ReadyStackGo verfolgt folgende Regeln:

1. **Jeder Kontext wird vollständig ersetzt**  
   → niemals „in-place updates“, niemals diff-basierte Änderungen.

2. **Kontexte ohne externe Ports zuerst**  
   → interne APIs, Worker, Service-Bus-Listener, EventStore, Identity, …

3. **Gateways immer zuletzt**  
   → damit die öffentlichen Endpunkte erst online gehen, wenn die internen Dienste laufen.

4. **Start-Reihenfolge folgt den Abhängigkeiten (dependsOn)**  
   → z. B.: BFF → Project API → Identity  
   → ansonsten deterministische Alphabet-Sortierung.

5. Fehler stoppen den gesamten Vorgang  
   → kein „teilweise installiert“.

---

# 13.2 Bestimmung der Reihenfolge

Algorithmus in Pseudocode:

```
contexts = manifest.contexts

internal = contexts where internal = true
external = contexts where internal = false

order_internal_by_dependencies(internal)
order_external_by_dependencies(external)

install_order = internal + external

gateway = manifest.gateway.context
move gateway to end
```

---

# 13.3 Beispiel

Manifestausschnitt:

```json
"contexts": {
  "project": { "internal": true },
  "identity": { "internal": true },
  "bffDesktop": { "internal": false, "dependsOn": ["project","identity"] },
  "edge-gateway": { "internal": false }
}
```

Installationsreihenfolge:

1. identity  
2. project  
3. bffDesktop  
4. edge-gateway  

---

# 13.4 DeploymentAction-Erzeugung

Für jeden Dienst werden 4 Schritte erzeugt:

1. **stop**  
2. **remove**  
3. **create**  
4. **start**

Beispiel:

```json
[
  { "type": "stop", "context": "identity" },
  { "type": "remove", "context": "identity" },
  { "type": "create", "context": "identity" },
  { "type": "start", "context": "identity" }
]
```

---

# 13.5 Ports & Zugriffe

Interne Kontexte:

- setzen keine public ports  
- setzen nur „private“ Container-Ports (Exposed Ports)

Gateway:

- setzt private port → interner HTTP-Port (z. B. 8080)
- setzt public port → HTTPS-Port (z. B. 8443)

---

# 13.6 Validierung vor Deployment

Vor dem Deployment werden geprüft:

1. **Alle Container-Images verfügbar?**  
2. **Wird der publicPort bereits benutzt?**  
3. **Schema-Version kompatibel?**  
4. **Funktionieren die Connection Strings?** (Basic Regex-Level)  
5. **Gateway-Kontext existiert?**

---

# 13.7 Umgang mit Abhängigkeiten

Der Algorithmus erlaubt:

- direkte Dependencies (1 Ebene)
- tiefe Dependencies (mehrere Ebenen)
- Zyklen werden erkannt und werfen Fehler:

```
errorCode: "MANIFEST_DEPENDENCY_CYCLE"
```

---

# 13.8 Parallelisierung (Future Optimization)

Potenzielle Optimierungen:

- interne Dienste parallel starten  
- externe Dienste sequentiell  
- Gateway immer nach allen anderen

Diese Optimierung wird für spätere Versionen eingeplant.

---

# 13.9 Fehler während der Reihenfolge-Auswertung

Fehlercodes:

| Code | Bedeutung |
|------|-----------|
| `MANIFEST_DEPENDENCY_MISSING` | Ein dependsOn-Verweis zeigt auf einen unbekannten Kontext |
| `MANIFEST_DEPENDENCY_CYCLE` | Ein zyklisches Abhängigkeitsverhältnis wurde erkannt |
| `MANIFEST_GATEWAY_INVALID` | Der definierte Gateway-Kontext existiert nicht |

---

# → Ende von Block 13/20


# 14. Multi-Node Architektur (Planung & Spezifikation für v1.0+)

Auch wenn ReadyStackGo zunächst **Single-Node** arbeitet, ist das gesamte System  
von Anfang an so entworfen, dass eine erweiterte **Multi-Node-Infrastruktur**  
darauf aufbauen kann. Dieses Kapitel beschreibt den geplanten Funktionsumfang  
und die technischen Anforderungen für zukünftige Cluster-Fähigkeit.

---

# 14.1 Ziele der Multi-Node Umsetzung

1. **Verteilung einzelner Kontexte** auf unterschiedliche Maschinen  
2. **Rollenbasierte Node-Zuweisung**  
   - Gateway-Node  
   - Compute-Node  
   - Storage-Node  
3. **Zentrales Management** weiterhin über den Admin-Container  
4. **Keine Abhängigkeit von Kubernetes oder Swarm**  
5. **Volle Offline-Fähigkeit**  
6. **Erweiterbare Node-Konfiguration** über `rsgo.nodes.json`

---

# 14.2 rsgo.nodes.json (Format)

```json
{
  "nodes": [
    {
      "nodeId": "local",
      "name": "Local Node",
      "dockerHost": "unix:///var/run/docker.sock",
      "roles": ["default"],
      "enabled": true
    },
    {
      "nodeId": "remote-01",
      "name": "Remote Server 01",
      "dockerHost": "tcp://192.168.0.12:2375",
      "roles": ["compute"],
      "enabled": true
    }
  ]
}
```

---

# 14.3 Node-Rollen

| Rolle | Bedeutung |
|--------|----------|
| `default` | Standardnode, auf dem alles laufen darf |
| `gateway` | Node für edge-gateway und public API |
| `compute` | Node für rechenintensive Kontexte |
| `storage` | Node für z. B. eventstore, db-proxy etc. |

---

# 14.4 Deployment-Strategie im Multi-Node-Modus

Für jeden Kontext im Manifest:

```json
"contexts": {
  "project": {
    "nodeRole": "compute"
  }
}
```

Der Deployment-Algorithmus macht:

```
node = findNodeWithRole(context.nodeRole)
dockerService = GetDockerService(node)
deploy(context) on dockerService
```

---

# 14.5 Docker Remote API

Remote Nodes benötigen:

- Docker Engine mit aktiviertem TCP Listener **oder**
- SSH Tunnel (geplant)  
- TLS gesicherte Verbindungen

Beispiel:

```
tcp://host:2376
```

---

# 14.6 Node Discovery (Future)

Optionale Mechanismen:

- mDNS Autodiscovery  
- Node-Herzschlag  
- Cluster-Status Anzeige in der UI  

---

# 14.7 Einschränkungen in v1.0

- Wizard unterstützt nur einen Node  
- Node-Management erst ab v1.1  
- Keine automatische Lastverteilung  
- Keine selbstheilenden Container  

---

# → Ende von Block 14/20


# 15. CI/CD Integration (Build, Release, Deployment-Automation)

Dieses Kapitel beschreibt, wie ReadyStackGo vollständig in moderne CI/CD-Pipelines  
(Azure DevOps, GitHub Actions, GitLab CI) integriert werden kann.  
Dies ist essenziell für automatisierte Releases, Pre-Releases und QA-Deployments.

---

# 15.1 Zielsetzung der CI/CD-Integration

1. **Automatisierte Builds aller Kontext-Container**
2. **Automatisiertes Tagging nach SemVer (x.y.z)**
3. **Automatisiertes Pushen zu Docker Hub oder eigener Registry**
4. **Automatisierte Erstellung des Release-Manifests**
5. **Automatisiertes Bereitstellen von Pre-Releases**
6. **Trigger für ReadyStackGo-Installationen auf Entwicklungsservern**

---

# 15.2 Anforderungen an jedes Kontext-Repository

Jeder Microservice-Kontext (z. B. Project, Memo, Discussion) benötigt:

```
/build
    Dockerfile
    version.txt
```

`version.txt` enthält:

```
6.4.0
```

---

# 15.3 Pipeline-Schritte (Azure DevOps Beispiel)

### 1. Version bestimmen
- Lese `version.txt`
- erhöhe Patch-Version oder Release-Version autom.

### 2. Docker Build

```
docker build -t registry/ams.project-api:$(VERSION) .
```

### 3. Push

```
docker push registry/ams.project-api:$(VERSION)
```

### 4. Manifest Update

Ein Skript erzeugt/aktualisiert:

```
manifest-$(STACK_VERSION).json
```

mit neuen Container-Versionen.

### 5. Publish Artefakt

- Manifest wird als Build-Artefakt veröffentlicht
- Optional direkt in ReadyStackGo-Verzeichnis kopiert

---

# 15.4 Pre-Release Support

Pre-Release Container werden mit Tags versehen:

```
6.4.0-alpha.1
6.4.0-beta.2
6.4.0-rc.1
```

Manifest kann diese Versionen referenzieren, z. B.:

```json
"version": "6.4.0-beta.2"
```

---

# 15.5 Trigger für Entwicklungsserver

Azure DevOps Pipeline kann nach erfolgreichem Build:

1. eine **Webhook-URL** von ReadyStackGo aufrufen:
```
POST /api/v1/hooks/deploy
{ "version": "4.3.0-alpha.7" }
```

2. ReadyStackGo lädt das Manifest  
3. Deployment startet automatisch  

Dies ist optional und nur im Dev-Modus möglich.

---

# 15.6 Release-Manifesterzeugung (Detail)

Ein PowerShell- oder Node.js-Skript erzeugt automatisch:

- `manifest-<stackVersion>.json`
- `changelog`
- `schemaVersion`

Die Struktur:

```json
{
  "stackVersion": "4.3.0",
  "contexts": {
    "project": { "version": "6.4.0" },
    "memo": { "version": "4.1.3" }
  }
}
```

---

# 15.7 Automatisierte QA-Deployments

Ein QA-Server kann eine spezielle Webseite bereitstellen:

- „Deploy latest pre-release“
- „Deploy specific version“
- „Rollback last version“

Diese verwendet ReadyStackGo als Backend.

---

# 15.8 Sicherheitsaspekte in CI/CD

- Zugriff auf Registry über Service Connection
- Webhooks signiert mit Secret Token
- ReadyStackGo validiert Origin

---

# → Ende von Block 15/20


# 16. Fehlercodes, Exceptions & Rückgabestandards (Deep Spec)

Dieses Kapitel beschreibt das vollständige Fehler- und Rückgabemodell  
für ReadyStackGo. Ziel ist eine **konsequente, maschinenlesbare Definition**,  
die sowohl UI, als auch externe Tools wie CI/CD eindeutig verarbeiten können.

---

# 16.1 Allgemeiner Response-Standard

Jede API-Antwort folgt exakt diesem Format:

```json
{
  "success": true,
  "data": { ... },
  "errorCode": null,
  "message": null
}
```

Bei Fehlern:

```json
{
  "success": false,
  "data": null,
  "errorCode": "XYZ_ERROR",
  "message": "Menschlich lesbare Beschreibung"
}
```

Die UI **interpretiert errorCode, nicht message**.

---

# 16.2 Universelle Fehlercodes

Diese Fehlercodes sind API-weit gültig:

| Code | Bedeutung |
|------|-----------|
| `UNKNOWN_ERROR` | Fallback für unerwartete Fehler |
| `INVALID_REQUEST` | Payload ungültig, Pflichtfelder fehlen |
| `UNAUTHORIZED` | Kein Token vorhanden |
| `FORBIDDEN` | Rolle hat kein Recht |
| `NOT_FOUND` | Ressource existiert nicht |
| `OPERATION_NOT_ALLOWED` | Aktion in diesem Zustand nicht erlaubt |

---

# 16.3 Wizard-bezogene Fehler

| Code | Beschreibung |
|------|--------------|
| `WIZARD_INVALID_STATE` | Schritt darf aktuell nicht ausgeführt werden |
| `WIZARD_ALREADY_COMPLETED` | Wizard bereits abgeschlossen |
| `WIZARD_STEP_INCOMPLETE` | Vorheriger Schritt fehlt |
| `WIZARD_ORG_INVALID` | Organisation ungültig |
| `WIZARD_CONNECTIONS_INVALID` | Verbindungsangaben ungültig |

---

# 16.4 Manifest- / Release-bezogene Fehler

| Code | Beschreibung |
|------|-------------|
| `INVALID_MANIFEST` | JSON nicht parsebar oder strukturell falsch |
| `MANIFEST_NOT_FOUND` | Version existiert nicht |
| `SCHEMA_INCOMPATIBLE` | Manifest-Schema zu alt/neu |
| `MANIFEST_DEPENDENCY_MISSING` | dependsOn verweist auf unbekannten Kontext |
| `MANIFEST_DEPENDENCY_CYCLE` | Zirkuläre Abhängigkeit |
| `MANIFEST_GATEWAY_INVALID` | Gateway-Kontext fehlt oder ungültig |

---

# 16.5 Deployment-Fehler

| Code | Bedeutung |
|------|-----------|
| `DEPLOYMENT_FAILED` | Allgemeiner Fehler im Deployment |
| `DOCKER_NETWORK_ERROR` | Netzwerkaufnahme fehlgeschlagen |
| `CONTAINER_CREATE_FAILED` | Container konnte nicht erzeugt werden |
| `CONTAINER_START_FAILED` | Container konnte nicht gestartet werden |
| `IMAGE_PULL_FAILED` | Image konnte nicht geladen werden |

---

# 16.6 TLS / Zertifikatsfehler

| Code | Bedeutung |
|------|-----------|
| `TLS_INVALID` | Zertifikat ungültig |
| `TLS_INSTALL_FAILED` | Upload/Installation fehlgeschlagen |
| `TLS_MODE_UNSUPPORTED` | Modus nicht unterstützt |

---

# 16.7 Auth-Fehler

| Code | Bedeutung |
|------|-----------|
| `AUTH_FAILED` | Falscher Benutzer oder Passwort |
| `OIDC_CONFIG_INVALID` | OIDC-Angaben ungültig |
| `TOKEN_EXPIRED` | JWT abgelaufen |
| `TOKEN_INVALID` | JWT ungültig oder manipuliert |

---

# 16.8 Fehlerbehandlung in der API (Beispiel)

```csharp
try
{
    var result = await _dispatcher.SendAsync(new InstallStackCommand(version));
    return TypedResults.Ok(Response.Success(result));
}
catch (InvalidManifestException ex)
{
    _logger.LogWarning(ex, "Manifest invalid");
    return TypedResults.BadRequest(Response.Error("INVALID_MANIFEST", ex.Message));
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error");
    return TypedResults.Problem(Response.Error("UNKNOWN_ERROR", ex.Message));
}
```

---

# 16.9 Fehlerbehandlung in der UI

Beispiel für TypeScript:

```ts
if (!res.success) {
    switch (res.errorCode) {
        case "INVALID_MANIFEST":
        case "DEPLOYMENT_FAILED":
        case "WIZARD_INVALID_STATE":
            toast.error(res.message);
            break;
        default:
            toast.error("Ein unerwarteter Fehler ist aufgetreten.");
    }
}
```

---

# 16.10 Mapping der Fehlercodes auf HTTP Status Codes

| HTTP Code | Wann? |
|-----------|-------|
| `200` | Erfolg |
| `400` | Client-seitiger Fehler (z.B. invalid manifest) |
| `401` | Kein Login |
| `403` | Falsche Rolle |
| `404` | Ressource nicht gefunden |
| `500` | Unerwarteter Fehler |

---

# → Ende von Block 16/20


# 17. ReadyStackGo Admin-Container Architektur (Runtime Internals)

Dieses Kapitel beschreibt, wie der ReadyStackGo-Admin-Container intern aufgebaut ist,  
wie er startet, welche Prozesse laufen und welche Module sich gegenseitig aufrufen.

---

# 17.1 Startprozess des Containers

Beim Start des Containers passiert folgendes:

1. **Configuration Bootstrap**
   - Prüfen, ob `/app/config/rsgo.system.json` existiert
   - Falls nicht → Wizard-Modus

2. **TLS Bootstrap**
   - Wenn kein Zertifikat existiert
   - → Self-Signed generieren
   - → rsgo.tls.json erstellen

3. **Dependency Injection aufbauen**
   - DockerService
   - ConfigStore
   - TLSService
   - ManifestProvider
   - DeploymentEngine
   - EnvVarService

4. **API starten**
   - FastEndpoints initialisieren
   - Static Files (React UI) bereitstellen

5. **Wizard oder Login starten**
   - Wizard UI, falls wizardState != Installed
   - sonst Admin-Login UI

---

# 17.2 Ordnerstruktur im Container

```
/app
    /api
    /ui
    /manifests
    /config                <-- rsgo-config Volume
    /certs
    /logs
```

### Dazu kommt das Host-Mount:
```
/var/run/docker.sock    <-- Docker API Zugriff
```

---

# 17.3 Architekturdiagramm (Textform)

```
+-----------------------+
| ReadyStackGo (Admin)  |
|   - API               |
|   - Wizard            |
|   - TLS Engine        |
|   - Config Store      |
|   - Deployment Engine |
|   - Manifest Loader   |
+-----------+-----------+
            |
            | Docker Socket
            v
+-------------------------------+
| Docker Engine (Host)         |
|  - Container Lifecycle Mgmt   |
|  - Networks                  |
|  - Images                    |
+-------------------------------+
```

---

# 17.4 API-Schicht

Implementiert mit:

- FastEndpoints
- Filters für Auth
- Global Error Middleware
- Logging

```
/api/v1/...  --> Dispatcher --> Application --> Domain
```

---

# 17.5 Application-Schicht

Besteht aus:

- Commands
- Queries
- Handlern
- Policies (z. B. Reihenfolge, Manifest-Logik)

Beispielstruktur:

```
Application/
    Commands/
        InstallStack/
        StartContainer/
        StopContainer/
    Queries/
        ListContainers/
        GetReleaseStatus/
```

---

# 17.6 Domain-Schicht

- rein objektorientiert
- komplett unabhängig vom System
- keine Docker-Abhängigkeiten

Beispiel:

```
Domain/
    Entities/
        ReleaseStatus.cs
        DeploymentPlan.cs
    ValueObjects/
        ContextId.cs
```

---

# 17.7 Infrastructure-Schicht

Enthält Implementierungen für:

- DockerService
- TlsService
- FileConfigStore
- ManifestProvider

Kommunikation:
- DockerService → Docker.DotNet
- FileConfigStore → JSON-Dateien
- TLSService → System.Security.Cryptography

---

# 17.8 Runtime Prozesse

Der Admin-Container enthält folgende Hintergrundprozesse (geplant):

## 1. Manifest-Watcher
- prüft, ob neue Manifeste verfügbar sind
- lädt neue Versionen automatisch (für Pre-Release-Modus)

## 2. Container Health Watcher
- prüft Container-Status
- markiert „unhealthy“
- API zeigt Zustand an

## 3. Log Rotator
- verwaltet Log-Dateien im Volume

---

# 17.9 Garbage Collection von alten Containern

Nach Installationen:

- Alte Container → entfernt
- Alte Images → optional entfernt
- Dangling Volumes → optional entfernt

Optionaler Cleanup-Modus:

```
POST /api/v1/admin/system/cleanup
```

---

# 17.10 Memory & Performance

Admin-Container Ressourcenverbrauch:

- CPU: ~1–2% im Leerlauf
- RAM: 100–150 MB
- Storage: abhängig von Logs & Config (~10 MB)

Deploymentprozess kann kurzzeitig mehr CPU nutzen.

---

# → Ende von Block 17/20


# 18. TLS/SSL-System (Deep Dive)

Dieses Kapitel beschreibt die komplette TLS/SSL-Implementierung von ReadyStackGo –  
einschließlich Zertifikatserstellung, Validierung, Austausch und Integration in den Gateway-Kontext.

---

# 18.1 Grundprinzipien

1. **TLS wird zentral in ReadyStackGo konfiguriert.**  
2. **Der Gateway-Kontext terminiert den TLS-Traffic.**  
3. Der Admin-Container nutzt TLS **nur im Wizard**, um ein sicheres Setup zu gewährleisten.  
4. Installation beginnt immer mit einem **Self-Signed-Zertifikat** (Default).  
5. Ein **Custom-Zertifikat** kann später über die UI importiert werden (PFX).

---

# 18.2 TLS-Konfigurationsdatei: rsgo.tls.json

Beispiel:

```json
{
  "mode": "SelfSigned",
  "certificatePath": "/app/certs/selfsigned.pfx",
  "certificatePassword": "r$go123!",
  "httpsPort": 8443,
  "terminatingContext": "edge-gateway"
}
```

Erläuterung:

| Feld | Bedeutung |
|------|-----------|
| mode | SelfSigned oder Custom |
| certificatePath | Pfad zur PFX-Datei |
| certificatePassword | Passwort der PFX |
| httpsPort | Port, an dem der Gateway TLS terminiert |
| terminatingContext | Kontextname des Gateways |

---

# 18.3 Self-Signed-Zertifikat erstellen

Das Self-Signed-Zertifikat wird beim ersten Start erzeugt:

```csharp
public async Task<TlsGenerateResult> GenerateSelfSignedAsync(string cn)
{
    using var rsa = RSA.Create(4096);

    var certReq = new CertificateRequest(
        $"CN={cn}",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1
    );

    certReq.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(false, false, 0, false));

    var cert = certReq.CreateSelfSigned(
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddYears(10));

    var password = GeneratePassword();

    File.WriteAllBytes("/app/certs/selfsigned.pfx", cert.Export(X509ContentType.Pfx, password));

    return new TlsGenerateResult {
        Path = "/app/certs/selfsigned.pfx",
        Password = password
    };
}
```

---

# 18.4 Custom-Zertifikate (Upload über UI)

UI sendet eine multipart Anfrage:

```
POST /api/v1/admin/tls/upload
```

Backend prüft:

1. Ist Datei PFX?  
2. Passwort korrekt?  
3. Zertifikat gültig?  
4. Enthält private keys?  

Bei Erfolg:

- Datei nach `/app/certs/custom.pfx`
- `rsgo.tls.json` → mode = "Custom"
- Gateway-Container wird bei nächster Installation mit Custom-Zertifikat gestartet

---

# 18.5 Gateway TLS-Integration

Der Gateway-Kontext wird im Manifest so beschrieben:

```json
"gateway": {
  "context": "edge-gateway",
  "protocol": "https",
  "publicPort": 8443,
  "internalHttpPort": 8080
}
```

Beim Erstellen des Containers werden die Zertifikatsdateien gemountet:

```csharp
HostConfig = new HostConfig {
    Binds = new List<string> {
        "/app/config/rsgo.tls.json:/tls/config.json",
        "/app/certs:/tls/certs"
    }
}
```

Der Gateway liest:

```
/tls/config.json
/tls/certs/*
```

---

# 18.6 Zertifikatrotation

Wechsel von Self-Signed zu Custom erfolgt:

1. Upload  
2. Validierung  
3. rsgo.tls.json aktualisieren  
4. Nächstes Deployment nutzt Custom-Zertifikat  

**Keine Downtime**, da Zertifikat erst beim Neustart des Gateways aktiv wird.

---

# 18.7 Zertifikatsvalidierung

Der Admin-Container prüft:

- Ablaufdatum
- Private Key vorhanden
- KeyUsage = DigitalSignature + KeyEncipherment
- SAN-Einträge vorhanden?

UI zeigt Warnungen an:

```
Zertifikat läuft in 23 Tagen ab.
```

---

# 18.8 TLS-Fehlercodes

| Code | Beschreibung |
|------|-------------|
| `TLS_INVALID` | Zertifikat konnte nicht validiert werden |
| `TLS_NO_PRIVATE_KEY` | PFX enthält keinen privaten Schlüssel |
| `TLS_PASSWORD_WRONG` | Passwort für PFX falsch |
| `TLS_INSTALL_FAILED` | Datei konnte nicht gespeichert werden |

---

# 18.9 Zukunft: ACME/Let's Encrypt Integration (optional)

Geplant:

- ACME Challenge via Gateway
- Domain-Validierung
- Auto-Renewal

---

# → Ende von Block 18/20


# 19. ReadyStackGo-Konfigurationssystem (rsgo-config Volume)

Dieses Kapitel beschreibt das vollständige **Konfigurationssystem** von ReadyStackGo.  
Alle Konfigurationen liegen zentral im `rsgo-config`-Volume, das beim Start des  
Admin-Containers gemountet wird:

```
-v rsgo-config:/app/config
```

---

# 19.1 Struktur des rsgo-config Volumes

```
/app/config
    rsgo.system.json
    rsgo.security.json
    rsgo.tls.json
    rsgo.contexts.json
    rsgo.features.json
    rsgo.release.json
    rsgo.nodes.json (future)
    custom-files/ (future)
```

Jede Datei hat einen klar definierten Zweck.

---

# 19.2 rsgo.system.json

Zentrale Systemkonfiguration:

```json
{
  "wizardState": "Installed",
  "dockerNetwork": "rsgo-net",
  "stackVersion": "4.3.0"
}
```

Felder:

- `wizardState` → steuert Wizard  
- `dockerNetwork` → Netzwerkname  
- `stackVersion` → installierte Version  

---

# 19.3 rsgo.security.json

Speichert alle Security-relevanten Daten:

```json
{
  "localUsers": [
    {
      "username": "admin",
      "passwordHash": "base64",
      "passwordSalt": "base64",
      "role": "admin"
    }
  ],
  "jwtSecret": "base64",
  "oidc": null
}
```

---

# 19.4 rsgo.tls.json

Beschreibung in Block 18. Wichtig:

```json
{
  "mode": "Custom",
  "certificatePath": "/app/certs/custom.pfx",
  "certificatePassword": "xyz",
  "httpsPort": 8443,
  "terminatingContext": "edge-gateway"
}
```

---

# 19.5 rsgo.contexts.json

Globale und kontextabhängige Verbindungsparameter:

```json
{
  "global": {
    "transport": "Server=sql;Database=transport;",
    "persistence": "Server=sql;Database=persistence;",
    "eventStore": null
  },
  "contexts": {
    "project": {
      "overrides": {
        "transport": null,
        "persistence": null,
        "eventStore": null
      }
    }
  },
  "advancedMode": false
}
```

UI zeigt:

- Simple Mode: nur „global“ sichtbar  
- Advanced Mode: „contexts“ mit Overrides  

---

# 19.6 rsgo.features.json

Feature Flags:

```json
{
  "features": {
    "newColorTheme": true,
    "discussionV2": false
  }
}
```

Kontextübergreifend!

---

# 19.7 rsgo.release.json

Speichert die aktuell installierte Version:

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3"
  },
  "installDate": "2025-03-12T10:22:00Z"
}
```

---

# 19.8 rsgo.nodes.json (Future)

Für Multi-Node-Fähigkeit:

```json
{
  "nodes": [
    {
      "nodeId": "local",
      "dockerHost": "unix:///var/run/docker.sock",
      "roles": [ "default" ],
      "enabled": true
    }
  ]
}
```

---

# 19.9 Änderungen an Config-Dateien

Konzept:

- **niemals patchen**, immer vollständige Datei ersetzen  
- alle Writes über `IConfigStore.SaveAsync()`  
- atomare Writes (temp → replace)

---

# 19.10 UI Synchronisation

Die Admin UI lädt alle Dateien über:

```
GET /api/v1/admin/config
```

Antwort:

```json
{
  "system": { ... },
  "tls": { ... },
  "contexts": { ... },
  "features": { ... },
  "release": { ... }
}
```

Änderungen erfolgen über dedizierte Endpunkte.

---

# 19.11 Validierungslogik

- UI validiert nur Grundstruktur  
- API validiert Werte (regex, required, constraints)  
- Deployment verweigert Installation bei ungültiger Config  

---

# 19.12 Backup & Restore (Future)

Geplant:

```
GET /api/v1/admin/config/backup
POST /api/v1/admin/config/restore
```

Komplette ZIP mit allen Dateien.

---

# → Ende von Block 19/20


# 19. Standard-Configfiles (rsgo.*.json) – Vollständige Spezifikation

ReadyStackGo speichert Systemzustände, Verbindungen, Security-Infos und TLS-Daten
in einer Reihe klar definierter JSON-Konfigurationsdateien im Volume:

```
/app/config  (Volume: rsgo-config)
```

Dieses Kapitel beschreibt jede Datei vollständig.

---

# 19.1 Übersicht der Dateien

| Datei | Beschreibung |
|-------|--------------|
| rsgo.system.json | Wizard-State, Netzwerkname, Stack-Info |
| rsgo.security.json | Benutzer, Passwörter, JWT Secret |
| rsgo.tls.json | TLS-Einstellungen, Zertifikate |
| rsgo.connections.json | Globale Verbindungen (Transport, Persistence, EventStore) |
| rsgo.contexts.json | Kontext-spezifische Overrides |
| rsgo.features.json | Feature Flags |
| rsgo.release.json | Installierte Stack-Version |

---

# 19.2 rsgo.system.json

Beispiel:

```json
{
  "wizardState": "Installed",
  "organization": {
    "id": "kunde-a",
    "name": "Kunde A GmbH"
  },
  "dockerNetwork": "rsgo-net",
  "stackInstalled": true
}
```

### Felder

| Feld | Meaning |
|------|---------|
| wizardState | aktueller Wizard-Status |
| organization | Organisation, unter der der Stack läuft |
| dockerNetwork | Name des Docker Netzwerks |
| stackInstalled | ob der Stack bereits installiert wurde |

---

# 19.3 rsgo.security.json

```json
{
  "users": [
    {
      "username": "admin",
      "passwordHash": "base64",
      "passwordSalt": "base64",
      "role": "admin"
    }
  ],
  "jwtSecret": "F38719...==",
  "localAdminFallbackEnabled": true,
  "oidc": {
    "enabled": false,
    "authority": "",
    "clientId": "",
    "clientSecret": "",
    "roleClaim": "role",
    "adminRole": "rsgo-admin",
    "operatorRole": "rsgo-operator"
  }
}
```

---

# 19.4 rsgo.tls.json

```json
{
  "mode": "SelfSigned",
  "certificatePath": "/app/certs/selfsigned.pfx",
  "certificatePassword": "r$go123!",
  "httpsPort": 8443,
  "terminatingContext": "edge-gateway"
}
```

---

# 19.5 rsgo.connections.json

```json
{
  "transport": "Server=sql;Database=ams;User=sa;Password=xyz",
  "persistence": "Server=sql;Database=ams;User=sa;Password=xyz",
  "eventStore": "esdb://eventstore:2113?tls=false"
}
```

---

# 19.6 rsgo.contexts.json

```json
{
  "mode": "simple",
  "contexts": {
    "project": {
      "transport": "Server=sql;Database=ams",
      "persistence": "Server=sql;Database=ams",
      "additionalConnections": []
    },
    "memo": {},
    "discussion": {}
  }
}
```

### mode

| Modus | Bedeutung |
|-------|-----------|
| simple | alle Kontexte nutzen globale Verbindungen |
| advanced | jeder Kontext kann eigene Verbindungen nutzen |

---

# 19.7 rsgo.features.json

```json
{
  "features": {
    "newColorTheme": true,
    "discussionV2": false,
    "extraLogging": true
  }
}
```

---

# 19.8 rsgo.release.json

```json
{
  "installedStackVersion": "4.3.0",
  "installedContexts": {
    "project": "6.4.0",
    "memo": "4.1.3",
    "discussion": "3.5.9"
  },
  "installDate": "2025-02-12T10:22:00Z"
}
```

---

# 19.9 Validierungsregeln für alle Dateien

- fehlende Dateien → werden neu erstellt  
- ungültiges JSON → Fehler `INVALID_CONFIG_FILE`  
- unvollständige Werte → Default-Werte setzen  
- alle Dateien werden **vollständig ersetzt**, niemals gepatcht  

---

# 19.10 Backup-Konzept (geplant)

Später werden automatische Backups im Volume erstellt:

- `rsgo.system.json.bak`  
- `rsgo.tls.json.bak`  
- usw.

inkl. Wiederherstellungsfunktion.

---

# → Ende von Block 19/20


# 20. Zukunftsarchitektur, Erweiterbarkeit & Plugin-System (Ausblick)

Dieses letzte Kapitel beschreibt, wie ReadyStackGo in der Zukunft erweitert werden kann –  
modular, skalierbar und offen für kundenspezifische oder Community-getriebene Erweiterungen.

---

# 20.1 Zukunftsvision

ReadyStackGo ist nicht nur ein Deployment-Tool, sondern ein  
**modularer Plattform-Kern**, der langfristig folgende Funktionen unterstützt:

- Multi-Node Cluster
- Auto-Healing
- High Availability
- Canary Deployments
- Blue/Green Deployments
- Organisationen mit mehreren Umgebungen
- Plugin-System für individuelle Erweiterungen
- Monitoring & Metrics
- API Gateway Routing Editor

---

# 20.2 Plugin-System – Entwurf

ReadyStackGo soll ein Plugin-System erhalten, das es erlaubt:

- eigene Endpoints hinzuzufügen  
- eigene Menüs im UI einzubinden  
- eigene Deployment-Schritte auszuführen  
- zusätzliche Kontextvariablen bereitzustellen  
- externe Tools anzubinden (EventStore, Grafana etc.)

## 20.2.1 Plugin-Verzeichnis

```
/app/plugins
    /PluginA/
        plugin.json
        plugin.dll
    /PluginB/
        plugin.json
```

---

## 20.2.2 plugin.json Format

```json
{
  "name": "ProjectInsights",
  "version": "1.0.0",
  "author": "YourCompany",
  "startupClass": "ProjectInsights.PluginStartup",
  "ui": {
    "menuLabel": "Insights",
    "route": "/insights"
  }
}
```

---

## 20.2.3 Plugin Startup Class (Beispiel)

```csharp
public class ProjectInsightsPlugin : IRsgoPlugin
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IInsightsProvider, DefaultInsightsProvider>();
    }

    public void ConfigureApi(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/insights", async context => {
            // ...
        });
    }
}
```

---

## 20.2.4 Plugin Loader Ablauf

1. Scannt `/app/plugins`  
2. Lädt Assemblies  
3. Findet alle Klassen, die `IRsgoPlugin` implementieren  
4. Führt `ConfigureServices` aus  
5. Führt `ConfigureApi` aus  
6. UI lädt automatisch zusätzliche Menüpunkte

---

# 20.3 Deployment-Plugins

Später möglich:

- Pre-Deployment Hooks  
- Post-Deployment Hooks  
- Custom Healthchecks  
- Custom EnvVar Provider  

Beispiel:

```json
{
  "hooks": {
    "beforeCreate": "ProjectInsights.Hooks.ValidateBeforeCreate",
    "afterStart": "ProjectInsights.Hooks.NotifyTeams"
  }
}
```

---

# 20.4 Telemetrie & Monitoring (Zukunft)

Geplant:

- Integration von Prometheus  
- Integration von Grafana Dashboards  
- EventStore Monitoring  
- Container Health Dashboard

Datenpunkte:

- CPU/RAM pro Container  
- Startzeit  
- Crash Count  
- Restart Count  
- Deployment-Dauer  

---

# 20.5 Organisationen & Umgebungen (Future Version)

Später soll es möglich sein, pro Organisation:

- mehrere Umgebungen zu definieren  
- pro Umgebung eigene Releases zu haben  

Beispiel:

```
/orgs/kunde-a/dev
/orgs/kunde-a/test
/orgs/kunde-a/prod
```

Jede Umgebung besitzt eigene:

- TLS Einstellungen  
- Manifeste  
- Kontexte  
- Feature Flags  

---

# 20.6 Erweiterung der Wizard-Funktionen (Future)

Neue Wizard-Schritte sind denkbar:

- Environment Setup (dev/test/prod)  
- Node Discovery  
- Storage Setup (SQL, EventStore, Redis)  
- Lizenzverwaltung  

---

# 20.7 Erweiterung der Deployment Engine

Mögliche künftige Features:

### 1. Live Rollbacks
Container werden nicht vollständig gelöscht, sondern nach fehlgeschlagenem Deployment automatisch zurückgerollt.

### 2. Blue/Green Deployments
- zwei Partitionen („blue“ und „green“)
- Gateway wechselt zwischen diesen

### 3. Canary Deployments
- kleiner Prozentsatz des Traffics geht auf neue Version  
- Monitoring entscheidet über Freigabe

---

# 20.8 Erweiterung der UI

Neue Module sollen entstehen können:

- Routing Editor für Gateway  
- Live Logs  
- System Dashboard  
- Cluster Topologie Visualisierung  
- Audit Logs

---

# 20.9 Erweiterung der Configfiles

Geplant:

- `rsgo.nodes.json` → Multi-Node  
- `rsgo.environments.json` → dev/test/prod  
- `rsgo.plugins.json` → Plugin-Management  
- `rsgo.metrics.json` → Metrik-Konfiguration  

---

# 20.10 Fazit

ReadyStackGo ist bereits als Version 1.0 vollständig funktionsfähig,  
aber gleichzeitig als Plattform für langfristige Erweiterungen ausgelegt.

Diese solide Grundlage macht es möglich, dass:

- Claude  
- du selbst  
- dein Team  
- die Community  

ReadyStackGo zu einer **vollständigen On-Premises Container-Plattform** ausbauen kann –
vergleichbar mit Portainer, aber **maßgeschneidert** für eure Microservice-Architektur.

---

# → Ende von Block 20/20  
**Technische Spezifikation vollständig!**
