
# ReadyStackGo – Technical Specification

## Table of Contents
1. API Overview
2. Endpoint Specification
3. Data Models
4. Commands & Queries
5. Services & Interfaces
6. Wizard API & State Machine
7. Deployment Engine – Process
8. Manifest Schema (formal)
9. Docker Integration
10. UI-API Contract

---

# 1. API Overview

ReadyStackGo provides a clearly defined HTTP API.
All endpoints are accessible under `/api/v1/`.

### Authentication
- During Wizard: **no auth**
- After that:
  - Local Login (JWT or Cookie)
  - Optional OIDC
  - Roles: `admin`, `operator`

### Standard Response
```json
{
  "success": true,
  "message": "optional",
  "data": {}
}
```

### Error Response
```json
{
  "success": false,
  "message": "Error description",
  "errorCode": "XYZ_ERROR"
}
```


# 2. Endpoint Specification

This chapter describes **all API endpoints** in detail.
Each endpoint contains:

- Path
- Method
- Role authorization
- Request Body
- Response Body
- Error codes

---

## 2.1 Container Endpoints

### **GET /api/v1/containers**
Lists all containers on the host.

**Roles:** admin, operator
**Auth:** required
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
Starts a container.

**Body:**
```json
{ "id": "string" }
```

**Roles:** admin, operator

---

### **POST /api/v1/containers/stop**
Stops a container.

**Body:**
```json
{ "id": "string" }
```

**Roles:** admin, operator

---

## 2.2 Wizard API

### **GET /api/v1/wizard/status**
Returns the current status of the Setup Wizard.

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
Creates the first admin user.

**Body:**
```json
{
  "username": "string",
  "password": "string"
}
```

Response:
```json
{ "success": true }
```

---

### **POST /api/v1/wizard/organization**
Creates the organization.

**Body:**
```json
{
  "id": "string",
  "name": "string"
}
```

---

### **POST /api/v1/wizard/connections**
Sets global connections.

```json
{
  "transport": "string",
  "persistence": "string",
  "eventStore": "string?"
}
```

---

### **POST /api/v1/wizard/install**
Installs the stack based on a manifest.

Response:
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
Lists all available manifests.

---

### **GET /api/v1/releases/current**
Returns the installed state.

---

### **POST /api/v1/releases/{version}/install**
Installs the specified manifest.

Error codes:
- `MANIFEST_NOT_FOUND`
- `DEPLOYMENT_FAILED`
- `INCOMPATIBLE_VERSION`

---

## 2.4 Admin API

### TLS

#### **GET /api/v1/admin/tls**
Shows TLS status.

#### **POST /api/v1/admin/tls/upload**
Upload of a custom certificate (multipart).

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

### Security (optional later)

#### **POST /api/v1/admin/security/oidc**
#### **POST /api/v1/admin/security/local-admin**

---



# 3. Data Models (Domain & DTO)

This chapter contains all **data models** that ReadyStackGo needs.
They are divided into three categories:

- **Domain Models** – internal business objects
- **DTOs** – API input and output
- **Config Models** – objects representing JSON configuration files

---

## 3.1 Domain Models

### **ContainerInfo**
Represents a Docker container on the host.

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
Represents the currently installed version.

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
Describes what steps are necessary to install a manifest.

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

ReadyStackGo uses a **Dispatcher Pattern** instead of MediatR.
All actions run through:

- **Commands** (write/state-changing)
- **Queries** (read)

Each Command/Query is executed via the `IDispatcher`.

---

## 4.1 Commands

### **StartContainerCommand**
Starts a Docker container.

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
Installs a manifest.

```csharp
public sealed record InstallStackCommand(string StackVersion)
    : ICommand<InstallResultDto>;
```

Handler executes:

1. Load manifest
2. Check version
3. Generate DeploymentPlan
4. Execute deployment
5. Update rsgo.release.json

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

## 4.4 Dispatcher Implementation

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

## 4.5 Advantages of the Dispatcher

- no reflection magic like MediatR
- full compilability of all handlers
- transparent resolution via DI
- own policies / pipelines easily integratable
- 100% compatible with FastEndpoints

---



# 5. Services & Interfaces

This chapter describes the most important internal services of ReadyStackGo.
Each service follows the interface-first principle and has a clearly defined responsibility.

---

# 5.1 IDockerService

Abstracts the Docker API.

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

Manages all config files (rsgo-config Volume).

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

**Implementation:** Files are always completely replaced (Write-All), never patched.

---

# 5.3 ITlsService

Creates certificates, validates certificates, and loads custom certificates.

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

Loads release manifests and validates their schema.

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

Executes a manifest completely.

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

Generates environment variables for each context.

```csharp
public interface IEnvVarService
{
    Task<Dictionary<string, string>> GenerateForContextAsync(
        string contextName,
        ReleaseManifest manifest
    );
}
```

The result consists of:

- system variables
- feature flags
- context connections
- manifest env overrides

---

# 5.7 IWebhookService (Future)

Will be used for CI/CD triggers and external events.

---

# 5.8 IService Locator (forbidden)

ReadyStackGo consistently uses DI and never a global Service Locator.

---



# 6. Wizard API & State Machine

The ReadyStackGo Wizard is based entirely on a clearly defined **State Machine**.
The API controls exclusively transitions of this state machine.

---

## 6.1 Wizard States

The Wizard knows the following states:

| State | Description |
|-------|-------------|
| `NotStarted` | rsgo.config does not exist or is empty |
| `AdminCreated` | The administrator was created |
| `OrganizationSet` | Organization was defined |
| `ConnectionsSet` | Connections were saved |
| `Installed` | Stack is installed, Wizard deactivated |

All states are stored in `rsgo.system.json`:

```json
{
  "wizardState": "OrganizationSet"
}
```

---

## 6.2 State Logic

### Start Conditions:
- Wizard is active when `wizardState != Installed`

### Transitions:

```
NotStarted → AdminCreated → OrganizationSet → ConnectionsSet → Installed
```

### Invalid transitions generate errors:

- e.g., `ConnectionsSet → AdminCreated` is forbidden

---

## 6.3 Wizard API Endpoints

### 1. **GET /api/v1/wizard/status**
Returns the current state.

---

### 2. **POST /api/v1/wizard/admin**
Creates the first admin.

Validations:
- Username must not be empty
- Password must meet minimum length

Result:
- wizardState = `AdminCreated`

---

### 3. **POST /api/v1/wizard/organization**
Saves:

- Organization ID
- Organization Name

Result:
- wizardState = `OrganizationSet`

---

### 4. **POST /api/v1/wizard/connections**
Saves the basic connections:

- Transport
- Persistence
- EventStore (optional)

Result:
- wizardState = `ConnectionsSet`

---

### 5. **POST /api/v1/wizard/install**
Installs the complete stack.

Process:
1. Select manifest
2. Generate deployment plan
3. Execute Deployment Engine
4. Save release file
5. wizardState = `Installed`

---

## 6.4 Wizard Error Codes

| Code | Meaning |
|------|---------|
| `WIZARD_INVALID_STATE` | API was called in wrong state |
| `WIZARD_ALREADY_COMPLETED` | Wizard is already completed |
| `WIZARD_STEP_INCOMPLETE` | Previous step missing |
| `DEPLOYMENT_FAILED` | Manifest could not be installed |

---

## 6.5 Example: Request Flow

1. User opens `/wizard`
2. UI calls: `GET /wizard/status`
3. Display current step
4. User submits form data
5. API saves config
6. Wizard goes to next step

After step 5:
- Redirect to login page

---

## 6.6 Wizard UI (for later implementation)

4-page stepper:

1. Admin
2. Organization
3. Connections
4. Installation

Wizard is **Fullscreen** to avoid distractions.

---



# 7. Deployment Engine – Process (Detail)

The Deployment Engine is the central mechanism with which ReadyStackGo
installs, updates, or validates a complete stack based on a release manifest.
This chapter describes the complete internal logic.

---

## 7.1 Overview (High-Level Flow)

The installation process runs in 10 steps:

1. Load manifest
2. Version and schema check
3. Collect old container list
4. Generate DeploymentPlan
5. Ensure Docker network
6. Execute context-wise actions
7. Deploy gateway last
8. Perform health checks (optional / later)
9. Update release file
10. Return result to API

---

## 7.2 DeploymentPlan Generation

The DeploymentPlan describes exactly all operations necessary
to install the release. Example:

```json
[
  { "type": "stop", "context": "project" },
  { "type": "remove", "context": "project" },
  { "type": "create", "context": "project" },
  { "type": "start", "context": "project" }
]
```

### Rules:

- Each context is completely replaced → no in-place updates
- Gateway context always as last step
- Internal contexts first
- Exposed ports only on Gateway

---

## 7.3 Docker Network

Before each deployment, it is ensured that the network exists:

```csharp
await _docker.NetworkEnsureExistsAsync(system.DockerNetwork);
```

Name:
```
rsgo-net
```

All containers are started in it.

---

## 7.4 EnvVar Generation

For each context, the Engine calls:

```csharp
var env = await _envVarService.GenerateForContextAsync(contextName, manifest);
```

This object contains:

- `RSGO_ORG_ID`
- `RSGO_STACK_VERSION`
- `RSGO_FEATURE_*`
- `RSGO_CONNECTION_*`
- Manifest Overrides

Example:

```json
{
  "RSGO_ORG_ID": "customer-a",
  "RSGO_CONNECTION_persistence": "Server=sql;Database=ams",
  "ROUTE_PROJECT": "http://ams-project"
}
```

---

## 7.5 Container Lifecycle – Technical Steps

### **1. Stop**
Stops running containers.

```csharp
await _docker.StopAsync(containerName);
```

### **2. Remove**
Completely removes containers.

```csharp
await _docker.RemoveAsync(containerName);
```

### **3. Create**
Creates container based on manifest.

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
Starts container (if not auto-started).

```csharp
await _docker.StartAsync(containerName);
```

---

## 7.6 Gateway Deployment (Special Handling)

The Gateway context is special:

- gets TLS parameters
- is publicly accessible
- is therefore **always deployed last**

### Example:

```json
"gateway": {
  "context": "edge-gateway",
  "protocol": "https",
  "publicPort": 8443,
  "internalHttpPort": 8080
}
```

The container is created with these ports:

- **exposed:** 8080
- **published:** 8443

---

## 7.7 Error Handling

### Hard failures
Stop the deployment completely:

- Image cannot be loaded
- Container cannot be created
- Network error
- Manifest invalid

Error Codes:

| Code | Description |
|------|-------------|
| `DEPLOYMENT_FAILED` | General error |
| `DOCKER_NETWORK_ERROR` | Network could not be created |
| `CONTAINER_START_FAILED` | Container cannot start |
| `INVALID_MANIFEST` | Schema invalid |

### Soft failures
Only warnings (visible later in UI):

- Health check not OK
- Container takes longer to start

---

## 7.8 Update Release File

After successful installation:

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

## 7.9 Return to API

Result:

```json
{
  "success": true,
  "data": {
    "installedVersion": "4.3.0"
  }
}
```

On error:

```json
{
  "success": false,
  "errorCode": "DEPLOYMENT_FAILED",
  "message": "Container 'project' could not be started."
}
```

---

# → End of Block 7/20


# 8. Manifest Schema (formal)

A manifest is the central file that describes the entire stack to be installed.
This chapter defines the **complete JSON Schema** that ReadyStackGo uses for manifests.

---

## 8.1 Main Structure

A manifest consists of the following main elements:

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

## 8.2 JSON Schema (complete)

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

## 8.3 Example Manifest (complete & commented)

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
    "notes": "This release contains the new dashboard."
  }
}
```

---

## 8.4 Schema Versioning

### Rules:
1. **schemaVersion** only increases when manifest structure changed.
2. Backwards compatibility is preserved where possible.
3. Old manifests may still be installed.
4. With incompatible versions, installation is refused:

```json
{
  "success": false,
  "errorCode": "SCHEMA_INCOMPATIBLE"
}
```

---

## 8.5 Manifest Storage Locations

ReadyStackGo searches for manifests:

1. **/manifests in the Admin container**
2. later: via a registry (e.g., GitHub Releases, Azure DevOps Artifact Feed)

The name corresponds to the stack version:

```
manifest-4.3.0.json
manifest-4.4.1.json
```

---

# → End of Block 8/20


# 9. Docker Integration (Detail)

ReadyStackGo integrates directly with the customer's Docker host.
This happens exclusively via the **Docker Socket**, which is mounted as a volume
into the Admin container:

```
-v /var/run/docker.sock:/var/run/docker.sock
```

This gives ReadyStackGo:

- full access to containers
- full access to images
- access to networks
- access to logs
- access to events

This is necessary to fully control stacks.

---

## 9.1 Docker.DotNet Library

ReadyStackGo uses the official library:

```xml
<PackageReference Include="Docker.DotNet" Version="3.125.5" />
```

This communicates directly with the Docker Socket via HTTP.

---

## 9.2 Container Lifecycle internally

For each context container, the following steps are executed:

### 1. Stop container
```csharp
await client.Containers.StopContainerAsync(id, new ContainerStopParameters());
```

### 2. Remove container
```csharp
await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });
```

### 3. Create container
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

### 4. Start container
```csharp
await client.Containers.StartContainerAsync(id, null);
```

---

## 9.3 Network Management

### Create or ensure:

```csharp
await client.Networks.CreateNetworkAsync(new NetworksCreateParameters {
    Name = "rsgo-net"
});
```

If already present, it silently continues.

---

## 9.4 Ports & Mappings

Internal ports are always set:

```json
"ports": [
  { "private": 8080, "public": null, "protocol": "tcp" }
]
```

Gateway additionally sets public ports:

```json
"ports": [
  { "private": 8080, "public": 8443, "protocol": "tcp" }
]
```

---

## 9.5 Logs

ReadyStackGo can later stream live logs:

```csharp
await client.Containers.GetContainerLogsAsync(id, false, new ContainerLogsParameters {
    ShowStdout = true,
    ShowStderr = true,
    Follow = true
});
```

This is not part of version 1.0.

---

## 9.6 Docker Events (Future)

Docker Events enable:

- Detection of container crashes
- Monitoring
- Auto-Healing later

Will be built into a later version.

---

## 9.7 Security Aspects

Access to the Docker Socket is potentially dangerous.
Therefore important:

- Admin container is secured via HTTPS
- Login roles control access
- No direct shell access
- Containers cannot be exec'd

---

# → End of Block 9/20


# 10. UI–API Contract

This chapter defines the complete **contract between the React frontend (Tailwind + TailAdmin)**
and the ReadyStackGo API.

The UI works **strictly typed** via TypeScript interfaces that exactly match the API DTOs.

---

# 10.1 Basic Principle: Thin Client, Thick Server

The UI:

- contains **no logic** that mutates system states
- only calls defined endpoints
- only reacts to the Wizard State Machine
- reads container status, release info, features, TLS info, etc.

The API contains **100% of the business logic**.

---

# 10.2 HTTP Conventions

All endpoints:

- path-based (`/api/v1/...`)
- return format: JSON
- errors as:

```json
{
  "success": false,
  "errorCode": "XYZ",
  "message": "Error description"
}
```

UI must interpret **errorCode**, not message.

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

All Wizard calls have **no return data** except success.

Example:

```ts
POST /api/v1/wizard/admin
{
  username: "admin",
  password: "xyz123..."
}
```

---

# 10.5 UI Page Structure

## 10.5.1 Login Page

- Username
- Password
- POST /auth/login
- Store token in LocalStorage or Cookie

---

## 10.5.2 Dashboard

The UI calls:

- `/api/v1/containers`
- `/api/v1/releases/current`

and shows:

- Container Status
- Stack Version
- Actions (admin only)

---

## 10.5.3 Containers Page

Actions:

- start/stop (operator, admin)
- logs (later)
- details

---

## 10.5.4 Releases Page

Actions:

- Load versions (`GET /releases`)
- Installation (`POST /releases/{version}/install`)

---

## 10.5.5 Feature Flags Page

- List of all features
- Toggle switch
- POST `/admin/features`

---

## 10.5.6 TLS Page

- Display TLS status
- Certificate upload (PFX)
- POST `/admin/tls/upload`

---

## 10.5.7 Contexts Page

- Simple/Advanced Mode switch
- Global connections
- Context overrides

---

# 10.6 Validation Rules (Frontend)

The frontend performs **only minimal validation**:

- Check required fields
- Format validation (e.g., Port = number)
- Show feedback on errors

All deeper rules lie in the API.

---

# 10.7 Error Handling

The UI checks:

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

# 10.8 Wizard UI Logic

### Rules:

- UI always shows the step based on `/wizard/status`
- no navigation by user possible
- no going back
- after installation → Redirect `/login`

---

# 10.9 UI State Management

Recommendation:

- State via React Query / Zustand as "server state"
- minimal use of Redux or Context API
- UI is fully API-driven

---

# → End of Block 10/20


# 11. Authentication & Authorization (Technical Details)

This chapter describes the complete technical implementation of the security layer of ReadyStackGo.

---

# 11.1 Authentication Modes

ReadyStackGo supports two main modes:

1. **Local Authentication (Default)**
2. **OpenID Connect – external Identity Provider (activatable later)**

---

## 11.1.1 Local Authentication

The first user (Admin) is created in the Wizard.
Data is stored in `rsgo.security.json`.

### Password Hash Format

```json
{
  "username": "admin",
  "passwordHash": "<base64>",
  "passwordSalt": "<base64>"
}
```

Recommended algorithm:

- PBKDF2-HMAC-SHA256
- Iterations: 210,000
- Salt: 16-32 bytes random
- Hash: 32-64 bytes

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

## 11.1.3 JWT Structure

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
- stored locally in `rsgo.security.json` or generated internally
- exchangeable later via Admin UI

---

# 11.2 Role Model

There are two roles:

| Role     | Description |
|----------|-------------|
| **admin** | Full access to all functions |
| **operator** | Can start/stop containers |

---

## 11.2.1 Role Definition in Config

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

# 11.3 Authorization in Endpoints

Each endpoint defines roles explicitly:

```csharp
public override void Configure()
{
    Get("/api/containers");
    Roles("admin", "operator");
}
```

### Wizard Endpoints:
- no authentication
- not accessible after Wizard completion

---

# 11.4 External Identity Provider (OIDC)

ReadyStackGo can later be connected via OIDC to:

- Keycloak
- ams.identity
- Azure AD (later)

### Configuration Structure:

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

### Process (future)

1. UI → Redirect to IdP
2. Login happens at IdP
3. Token → ReadyStackGo
4. Extract roles from claims
5. Grant / deny access

---

# 11.5 Local Admin Fallback

Configurable:

```json
{
  "localAdminFallbackEnabled": true
}
```

When enabled:

- If IdP is offline, local admin remains login-capable
- If disabled → *only* IdP allows logins

---

# 11.6 HTTP Security

### HTTPS is provided by Gateway

Gateway receives:
- Certificate
- HTTPS Port
- Exposed Port

Internally everything communicates via HTTP.

### Admin container itself:
- can be accessible via HTTPS (for setup)
- terminates TLS on itself in Wizard mode

---

# 11.7 Security Headers

All responses contain:

- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection: 1`
- `Strict-Transport-Security` (if https)

---

# 11.8 Anti-CSRF

When JWT via Cookie:
- UI sends X-CSRF-Header
- Server checks token in header and cookie

Currently: JWT via LocalStorage recommended.

---

# 11.9 Rate Limiting (Future)

Planned:
- Default: 100 requests/min per IP
- Admin endpoints more restrictive

---

# → End of Block 11/20


# 12. Logging, Monitoring & Diagnostics

This chapter describes the logging and monitoring concept of ReadyStackGo,
as well as how errors are captured, stored, and provided to the UI.

---

# 12.1 Logging in Admin Container

ReadyStackGo uses by default:

- **Microsoft.Extensions.Logging**
- Output to **Console**
- Output to **FileLog** (optional, later)
- Structured Logs via **Serilog** (planned)
- Log level configurable

Standard log levels:

```
Information
Warning
Error
Critical
```

---

## 12.1.1 Log Storage Location

By default:

```
/app/logs/rsgo-admin.log
```

Rotating logs (planned):

- `rsgo-admin.log`
- `rsgo-admin.log.1`
- `rsgo-admin.log.2`

---

# 12.2 Logging in Deployment Process

During deployment:

- each step is logged
- errors are additionally written to a separate deployment log
- UI can later retrieve deployment logs

Example log entry:

```
[INFO] [Deployment] Starting context 'project' (image registry/ams.project-api:6.4.0)
[ERROR] [Docker] Failed to start container 'project': port already in use
```

---

# 12.3 UI Log Streaming (Future Feature)

Later the following API should exist:

```
GET /api/v1/containers/{id}/logs?follow=true
```

This streams:

- stdout
- stderr

---

# 12.4 Event Log

An internal EventLog stores:

| Timestamp | Category | Event |
|-----------|----------|-------|
| 2025-03-11 08:12 | Deploy | Install stackVersion=4.3.0 |
| 2025-03-11 08:14 | TLS | Custom certificate uploaded |
| 2025-03-12 09:20 | Auth | Login failed for user admin |

API:

```
GET /api/v1/admin/events
```

---

# 12.5 Error Codes (global)

Each error receives a unique code, e.g.:

| Code | Description |
|------|-------------|
| `DEPLOYMENT_FAILED` | Error in deployment process |
| `INVALID_MANIFEST` | Manifest faulty |
| `SCHEMA_INCOMPATIBLE` | Manifest schema not compatible |
| `WIZARD_INVALID_STATE` | Wizard was called in wrong phase |
| `DOCKER_NETWORK_ERROR` | Docker network error |
| `CONTAINER_START_FAILED` | Container could not start |
| `AUTH_FAILED` | Login invalid |
| `TLS_INVALID` | Certificate invalid |

---

# 12.6 Error Handling in Code

Example for Deployment:

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

# 12.7 Health Checks (Future)

Planned:

- Regularly check `/health` endpoints of services
- Display results in UI
- Optional alerts in Admin UI

---

# → End of Block 12/20


# 13. Deployment Plans & Order Logic (Deep Details)

This chapter describes the internal logic with which ReadyStackGo determines
**in what order containers are installed, removed, or updated**.

This is crucial so that the stack can be rolled out deterministically, safely, and predictably.

---

# 13.1 Basic Principles

ReadyStackGo follows these rules:

1. **Each context is completely replaced**
   → never "in-place updates", never diff-based changes.

2. **Contexts without external ports first**
   → internal APIs, workers, service bus listeners, EventStore, Identity, ...

3. **Gateways always last**
   → so that public endpoints only go online when internal services are running.

4. **Start order follows dependencies (dependsOn)**
   → e.g.: BFF → Project API → Identity
   → otherwise deterministic alphabetical sorting.

5. Errors stop the entire process
   → no "partially installed".

---

# 13.2 Determining the Order

Algorithm in pseudocode:

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

# 13.3 Example

Manifest excerpt:

```json
"contexts": {
  "project": { "internal": true },
  "identity": { "internal": true },
  "bffDesktop": { "internal": false, "dependsOn": ["project","identity"] },
  "edge-gateway": { "internal": false }
}
```

Installation order:

1. identity
2. project
3. bffDesktop
4. edge-gateway

---

# 13.4 DeploymentAction Generation

For each service, 4 steps are generated:

1. **stop**
2. **remove**
3. **create**
4. **start**

Example:

```json
[
  { "type": "stop", "context": "identity" },
  { "type": "remove", "context": "identity" },
  { "type": "create", "context": "identity" },
  { "type": "start", "context": "identity" }
]
```

---

# 13.5 Ports & Access

Internal contexts:

- set no public ports
- set only "private" container ports (Exposed Ports)

Gateway:

- sets private port → internal HTTP port (e.g., 8080)
- sets public port → HTTPS port (e.g., 8443)

---

# 13.6 Validation Before Deployment

Before deployment, the following are checked:

1. **All container images available?**
2. **Is the publicPort already in use?**
3. **Schema version compatible?**
4. **Connection strings working?** (Basic Regex level)
5. **Gateway context exists?**

---

# 13.7 Handling Dependencies

The algorithm allows:

- direct dependencies (1 level)
- deep dependencies (multiple levels)
- cycles are detected and throw error:

```
errorCode: "MANIFEST_DEPENDENCY_CYCLE"
```

---

# 13.8 Parallelization (Future Optimization)

Potential optimizations:

- start internal services in parallel
- external services sequentially
- Gateway always after all others

This optimization is planned for later versions.

---

# 13.9 Errors During Order Evaluation

Error codes:

| Code | Meaning |
|------|---------|
| `MANIFEST_DEPENDENCY_MISSING` | A dependsOn reference points to unknown context |
| `MANIFEST_DEPENDENCY_CYCLE` | A cyclic dependency was detected |
| `MANIFEST_GATEWAY_INVALID` | The defined gateway context does not exist |

---

# → End of Block 13/20


# 14. Multi-Node Architecture (Planning & Specification for v1.0+)

Even though ReadyStackGo initially works **Single-Node**, the entire system
is designed from the start so that an extended **Multi-Node infrastructure**
can build upon it. This chapter describes the planned feature scope
and technical requirements for future cluster capability.

---

# 14.1 Goals of Multi-Node Implementation

1. **Distribution of individual contexts** to different machines
2. **Role-based node assignment**
   - Gateway Node
   - Compute Node
   - Storage Node
3. **Central management** still via the Admin container
4. **No dependency on Kubernetes or Swarm**
5. **Full offline capability**
6. **Extensible node configuration** via `rsgo.nodes.json`

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

# 14.3 Node Roles

| Role | Meaning |
|------|---------|
| `default` | Standard node, everything may run on it |
| `gateway` | Node for edge-gateway and public API |
| `compute` | Node for compute-intensive contexts |
| `storage` | Node for e.g., eventstore, db-proxy etc. |

---

# 14.4 Deployment Strategy in Multi-Node Mode

For each context in the manifest:

```json
"contexts": {
  "project": {
    "nodeRole": "compute"
  }
}
```

The deployment algorithm does:

```
node = findNodeWithRole(context.nodeRole)
dockerService = GetDockerService(node)
deploy(context) on dockerService
```

---

# 14.5 Docker Remote API

Remote Nodes need:

- Docker Engine with activated TCP Listener **or**
- SSH Tunnel (planned)
- TLS secured connections

Example:

```
tcp://host:2376
```

---

# 14.6 Node Discovery (Future)

Optional mechanisms:

- mDNS Autodiscovery
- Node heartbeat
- Cluster status display in UI

---

# 14.7 Limitations in v1.0

- Wizard supports only one node
- Node management only from v1.1
- No automatic load balancing
- No self-healing containers

---

# → End of Block 14/20


# 15. CI/CD Integration (Build, Release, Deployment Automation)

This chapter describes how ReadyStackGo can be fully integrated into modern CI/CD pipelines
(Azure DevOps, GitHub Actions, GitLab CI).
This is essential for automated releases, pre-releases, and QA deployments.

---

# 15.1 Goals of CI/CD Integration

1. **Automated builds of all context containers**
2. **Automated tagging according to SemVer (x.y.z)**
3. **Automated pushing to Docker Hub or own registry**
4. **Automated creation of release manifest**
5. **Automated provision of pre-releases**
6. **Trigger for ReadyStackGo installations on development servers**

---

# 15.2 Requirements for Each Context Repository

Each microservice context (e.g., Project, Memo, Discussion) needs:

```
/build
    Dockerfile
    version.txt
```

`version.txt` contains:

```
6.4.0
```

---

# 15.3 Pipeline Steps (Azure DevOps Example)

### 1. Determine version
- Read `version.txt`
- Increment patch version or release version automatically

### 2. Docker Build

```
docker build -t registry/ams.project-api:$(VERSION) .
```

### 3. Push

```
docker push registry/ams.project-api:$(VERSION)
```

### 4. Manifest Update

A script creates/updates:

```
manifest-$(STACK_VERSION).json
```

with new container versions.

### 5. Publish Artifact

- Manifest is published as build artifact
- Optionally copied directly to ReadyStackGo directory

---

# 15.4 Pre-Release Support

Pre-release containers are tagged with:

```
6.4.0-alpha.1
6.4.0-beta.2
6.4.0-rc.1
```

Manifest can reference these versions, e.g.:

```json
"version": "6.4.0-beta.2"
```

---

# 15.5 Trigger for Development Servers

Azure DevOps Pipeline can after successful build:

1. call a **Webhook URL** of ReadyStackGo:
```
POST /api/v1/hooks/deploy
{ "version": "4.3.0-alpha.7" }
```

2. ReadyStackGo loads the manifest
3. Deployment starts automatically

This is optional and only possible in dev mode.

---

# 15.6 Release Manifest Generation (Detail)

A PowerShell or Node.js script automatically generates:

- `manifest-<stackVersion>.json`
- `changelog`
- `schemaVersion`

The structure:

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

# 15.7 Automated QA Deployments

A QA server can provide a special webpage:

- "Deploy latest pre-release"
- "Deploy specific version"
- "Rollback last version"

This uses ReadyStackGo as backend.

---

# 15.8 Security Aspects in CI/CD

- Access to registry via Service Connection
- Webhooks signed with Secret Token
- ReadyStackGo validates Origin

---

# → End of Block 15/20


# 16. Error Codes, Exceptions & Return Standards (Deep Spec)

This chapter describes the complete error and return model
for ReadyStackGo. The goal is a **consistent, machine-readable definition**
that both UI and external tools like CI/CD can process unambiguously.

---

# 16.1 General Response Standard

Every API response follows exactly this format:

```json
{
  "success": true,
  "data": { ... },
  "errorCode": null,
  "message": null
}
```

On errors:

```json
{
  "success": false,
  "data": null,
  "errorCode": "XYZ_ERROR",
  "message": "Human-readable description"
}
```

The UI **interprets errorCode, not message**.

---

# 16.2 Universal Error Codes

These error codes are valid API-wide:

| Code | Meaning |
|------|---------|
| `UNKNOWN_ERROR` | Fallback for unexpected errors |
| `INVALID_REQUEST` | Payload invalid, required fields missing |
| `UNAUTHORIZED` | No token present |
| `FORBIDDEN` | Role has no permission |
| `NOT_FOUND` | Resource does not exist |
| `OPERATION_NOT_ALLOWED` | Action not allowed in this state |

---

# 16.3 Wizard-related Errors

| Code | Description |
|------|-------------|
| `WIZARD_INVALID_STATE` | Step may not be executed currently |
| `WIZARD_ALREADY_COMPLETED` | Wizard already completed |
| `WIZARD_STEP_INCOMPLETE` | Previous step missing |
| `WIZARD_ORG_INVALID` | Organization invalid |
| `WIZARD_CONNECTIONS_INVALID` | Connection details invalid |

---

# 16.4 Manifest/Release-related Errors

| Code | Description |
|------|-------------|
| `INVALID_MANIFEST` | JSON not parseable or structurally wrong |
| `MANIFEST_NOT_FOUND` | Version does not exist |
| `SCHEMA_INCOMPATIBLE` | Manifest schema too old/new |
| `MANIFEST_DEPENDENCY_MISSING` | dependsOn references unknown context |
| `MANIFEST_DEPENDENCY_CYCLE` | Circular dependency |
| `MANIFEST_GATEWAY_INVALID` | Gateway context missing or invalid |

---

# 16.5 Deployment Errors

| Code | Meaning |
|------|---------|
| `DEPLOYMENT_FAILED` | General deployment error |
| `DOCKER_NETWORK_ERROR` | Network creation failed |
| `CONTAINER_CREATE_FAILED` | Container could not be created |
| `CONTAINER_START_FAILED` | Container could not be started |
| `IMAGE_PULL_FAILED` | Image could not be loaded |

---

# 16.6 TLS/Certificate Errors

| Code | Meaning |
|------|---------|
| `TLS_INVALID` | Certificate invalid |
| `TLS_INSTALL_FAILED` | Upload/installation failed |
| `TLS_MODE_UNSUPPORTED` | Mode not supported |

---

# 16.7 Auth Errors

| Code | Meaning |
|------|---------|
| `AUTH_FAILED` | Wrong user or password |
| `OIDC_CONFIG_INVALID` | OIDC details invalid |
| `TOKEN_EXPIRED` | JWT expired |
| `TOKEN_INVALID` | JWT invalid or manipulated |

---

# 16.8 Error Handling in API (Example)

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

# 16.9 Error Handling in UI

Example for TypeScript:

```ts
if (!res.success) {
    switch (res.errorCode) {
        case "INVALID_MANIFEST":
        case "DEPLOYMENT_FAILED":
        case "WIZARD_INVALID_STATE":
            toast.error(res.message);
            break;
        default:
            toast.error("An unexpected error occurred.");
    }
}
```

---

# 16.10 Mapping Error Codes to HTTP Status Codes

| HTTP Code | When? |
|-----------|-------|
| `200` | Success |
| `400` | Client-side error (e.g., invalid manifest) |
| `401` | No login |
| `403` | Wrong role |
| `404` | Resource not found |
| `500` | Unexpected error |

---

# → End of Block 16/20


# 17. ReadyStackGo Admin Container Architecture (Runtime Internals)

This chapter describes how the ReadyStackGo Admin container is structured internally,
how it starts, what processes run, and which modules call each other.

---

# 17.1 Container Startup Process

When the container starts, the following happens:

1. **Configuration Bootstrap**
   - Check if `/app/config/rsgo.system.json` exists
   - If not → Wizard mode

2. **TLS Bootstrap**
   - If no certificate exists
   - → Generate Self-Signed
   - → Create rsgo.tls.json

3. **Build Dependency Injection**
   - DockerService
   - ConfigStore
   - TLSService
   - ManifestProvider
   - DeploymentEngine
   - EnvVarService

4. **Start API**
   - Initialize FastEndpoints
   - Serve Static Files (React UI)

5. **Start Wizard or Login**
   - Wizard UI if wizardState != Installed
   - otherwise Admin Login UI

---

# 17.2 Folder Structure in Container

```
/app
    /api
    /ui
    /manifests
    /config                <-- rsgo-config Volume
    /certs
    /logs
```

### Additionally the host mount:
```
/var/run/docker.sock    <-- Docker API Access
```

---

# 17.3 Architecture Diagram (Text form)

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

# 17.4 API Layer

Implemented with:

- FastEndpoints
- Filters for Auth
- Global Error Middleware
- Logging

```
/api/v1/...  --> Dispatcher --> Application --> Domain
```

---

# 17.5 Application Layer

Consists of:

- Commands
- Queries
- Handlers
- Policies (e.g., order, manifest logic)

Example structure:

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

# 17.6 Domain Layer

- purely object-oriented
- completely independent of the system
- no Docker dependencies

Example:

```
Domain/
    Entities/
        ReleaseStatus.cs
        DeploymentPlan.cs
    ValueObjects/
        ContextId.cs
```

---

# 17.7 Infrastructure Layer

Contains implementations for:

- DockerService
- TlsService
- FileConfigStore
- ManifestProvider

Communication:
- DockerService → Docker.DotNet
- FileConfigStore → JSON files
- TLSService → System.Security.Cryptography

---

# 17.8 Runtime Processes

The Admin container contains the following background processes (planned):

## 1. Manifest Watcher
- checks if new manifests are available
- loads new versions automatically (for pre-release mode)

## 2. Container Health Watcher
- checks container status
- marks "unhealthy"
- API shows state

## 3. Log Rotator
- manages log files in volume

---

# 17.9 Garbage Collection of Old Containers

After installations:

- Old containers → removed
- Old images → optionally removed
- Dangling volumes → optionally removed

Optional cleanup mode:

```
POST /api/v1/admin/system/cleanup
```

---

# 17.10 Memory & Performance

Admin container resource consumption:

- CPU: ~1-2% idle
- RAM: 100-150 MB
- Storage: depends on logs & config (~10 MB)

Deployment process can briefly use more CPU.

---

# → End of Block 17/20


# 18. TLS/SSL System (Deep Dive)

This chapter describes the complete TLS/SSL implementation of ReadyStackGo –
including certificate creation, validation, exchange, and integration into the gateway context.

---

# 18.1 Basic Principles

1. **TLS is configured centrally in ReadyStackGo.**
2. **The Gateway context terminates TLS traffic.**
3. The Admin container uses TLS **only in Wizard** to ensure secure setup.
4. Installation always starts with a **Self-Signed certificate** (Default).
5. A **Custom certificate** can be imported later via the UI (PFX).

---

# 18.2 TLS Configuration File: rsgo.tls.json

Example:

```json
{
  "mode": "SelfSigned",
  "certificatePath": "/app/certs/selfsigned.pfx",
  "certificatePassword": "r$go123!",
  "httpsPort": 8443,
  "terminatingContext": "edge-gateway"
}
```

Explanation:

| Field | Meaning |
|-------|---------|
| mode | SelfSigned or Custom |
| certificatePath | Path to PFX file |
| certificatePassword | Password of PFX |
| httpsPort | Port where Gateway terminates TLS |
| terminatingContext | Context name of Gateway |

---

# 18.3 Create Self-Signed Certificate

The Self-Signed certificate is generated on first start:

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

# 18.4 Custom Certificates (Upload via UI)

UI sends a multipart request:

```
POST /api/v1/admin/tls/upload
```

Backend checks:

1. Is file PFX?
2. Password correct?
3. Certificate valid?
4. Contains private keys?

On success:

- File to `/app/certs/custom.pfx`
- `rsgo.tls.json` → mode = "Custom"
- Gateway container is started with custom certificate on next installation

---

# 18.5 Gateway TLS Integration

The Gateway context is described in the manifest like this:

```json
"gateway": {
  "context": "edge-gateway",
  "protocol": "https",
  "publicPort": 8443,
  "internalHttpPort": 8080
}
```

When creating the container, certificate files are mounted:

```csharp
HostConfig = new HostConfig {
    Binds = new List<string> {
        "/app/config/rsgo.tls.json:/tls/config.json",
        "/app/certs:/tls/certs"
    }
}
```

The Gateway reads:

```
/tls/config.json
/tls/certs/*
```

---

# 18.6 Certificate Rotation

Switch from Self-Signed to Custom happens:

1. Upload
2. Validation
3. Update rsgo.tls.json
4. Next deployment uses custom certificate

**No downtime**, as certificate only becomes active on Gateway restart.

---

# 18.7 Certificate Validation

The Admin container checks:

- Expiration date
- Private key present
- KeyUsage = DigitalSignature + KeyEncipherment
- SAN entries present?

UI shows warnings:

```
Certificate expires in 23 days.
```

---

# 18.8 TLS Error Codes

| Code | Description |
|------|-------------|
| `TLS_INVALID` | Certificate could not be validated |
| `TLS_NO_PRIVATE_KEY` | PFX contains no private key |
| `TLS_PASSWORD_WRONG` | Password for PFX wrong |
| `TLS_INSTALL_FAILED` | File could not be saved |

---

# 18.9 Future: ACME/Let's Encrypt Integration (optional)

Planned:

- ACME Challenge via Gateway
- Domain validation
- Auto-Renewal

---

# → End of Block 18/20


# 19. ReadyStackGo Configuration System (rsgo-config Volume)

This chapter describes the complete **configuration system** of ReadyStackGo.
All configurations are centrally located in the `rsgo-config` volume, which is mounted when starting the Admin container:

```
-v rsgo-config:/app/config
```

---

# 19.1 Structure of rsgo-config Volume

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

Each file has a clearly defined purpose.

---

# 19.2 rsgo.system.json

Central system configuration:

```json
{
  "wizardState": "Installed",
  "dockerNetwork": "rsgo-net",
  "stackVersion": "4.3.0"
}
```

Fields:

- `wizardState` → controls Wizard
- `dockerNetwork` → network name
- `stackVersion` → installed version

---

# 19.3 rsgo.security.json

Stores all security-relevant data:

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

Description in Block 18. Important:

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

Global and context-dependent connection parameters:

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

UI shows:

- Simple Mode: only "global" visible
- Advanced Mode: "contexts" with overrides

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

Cross-context!

---

# 19.7 rsgo.release.json

Stores the currently installed version:

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

For multi-node capability:

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

# 19.9 Changes to Config Files

Concept:

- **never patch**, always replace complete file
- all writes via `IConfigStore.SaveAsync()`
- atomic writes (temp → replace)

---

# 19.10 UI Synchronization

The Admin UI loads all files via:

```
GET /api/v1/admin/config
```

Response:

```json
{
  "system": { ... },
  "tls": { ... },
  "contexts": { ... },
  "features": { ... },
  "release": { ... }
}
```

Changes happen via dedicated endpoints.

---

# 19.11 Validation Logic

- UI validates only basic structure
- API validates values (regex, required, constraints)
- Deployment refuses installation with invalid config

---

# 19.12 Backup & Restore (Future)

Planned:

```
GET /api/v1/admin/config/backup
POST /api/v1/admin/config/restore
```

Complete ZIP with all files.

---

# → End of Block 19/20


# 20. Future Architecture, Extensibility & Plugin System (Outlook)

This final chapter describes how ReadyStackGo can be extended in the future –
modular, scalable, and open to customer-specific or community-driven extensions.

---

# 20.1 Future Vision

ReadyStackGo is not just a deployment tool, but a
**modular platform core** that will support the following functions long-term:

- Multi-Node Cluster
- Auto-Healing
- High Availability
- Canary Deployments
- Blue/Green Deployments
- Organizations with multiple environments
- Plugin system for individual extensions
- Monitoring & Metrics
- API Gateway Routing Editor

---

# 20.2 Plugin System – Design

ReadyStackGo will receive a plugin system that allows:

- adding own endpoints
- embedding own menus in UI
- executing own deployment steps
- providing additional context variables
- connecting external tools (EventStore, Grafana etc.)

## 20.2.1 Plugin Directory

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

## 20.2.3 Plugin Startup Class (Example)

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

## 20.2.4 Plugin Loader Process

1. Scans `/app/plugins`
2. Loads assemblies
3. Finds all classes implementing `IRsgoPlugin`
4. Executes `ConfigureServices`
5. Executes `ConfigureApi`
6. UI automatically loads additional menu items

---

# 20.3 Deployment Plugins

Later possible:

- Pre-Deployment Hooks
- Post-Deployment Hooks
- Custom Health Checks
- Custom EnvVar Provider

Example:

```json
{
  "hooks": {
    "beforeCreate": "ProjectInsights.Hooks.ValidateBeforeCreate",
    "afterStart": "ProjectInsights.Hooks.NotifyTeams"
  }
}
```

---

# 20.4 Telemetry & Monitoring (Future)

Planned:

- Integration of Prometheus
- Integration of Grafana Dashboards
- EventStore Monitoring
- Container Health Dashboard

Data points:

- CPU/RAM per container
- Start time
- Crash count
- Restart count
- Deployment duration

---

# 20.5 Organizations & Environments (Future Version)

Later it should be possible, per organization:

- to define multiple environments
- to have own releases per environment

Example:

```
/orgs/customer-a/dev
/orgs/customer-a/test
/orgs/customer-a/prod
```

Each environment has its own:

- TLS settings
- Manifests
- Contexts
- Feature flags

---

# 20.6 Extension of Wizard Functions (Future)

New Wizard steps are conceivable:

- Environment Setup (dev/test/prod)
- Node Discovery
- Storage Setup (SQL, EventStore, Redis)
- License Management

---

# 20.7 Extension of Deployment Engine

Possible future features:

### 1. Live Rollbacks
Containers are not completely deleted, but automatically rolled back after failed deployment.

### 2. Blue/Green Deployments
- two partitions ("blue" and "green")
- Gateway switches between them

### 3. Canary Deployments
- small percentage of traffic goes to new version
- Monitoring decides on release

---

# 20.8 Extension of UI

New modules can be created:

- Routing Editor for Gateway
- Live Logs
- System Dashboard
- Cluster Topology Visualization
- Audit Logs

---

# 20.9 Extension of Config Files

Planned:

- `rsgo.nodes.json` → Multi-Node
- `rsgo.environments.json` → dev/test/prod
- `rsgo.plugins.json` → Plugin Management
- `rsgo.metrics.json` → Metrics Configuration

---

# 20.10 Conclusion

ReadyStackGo is already fully functional as version 1.0,
but is also designed as a platform for long-term extensions.

This solid foundation makes it possible that:

- Claude
- you yourself
- your team
- the community

can develop ReadyStackGo into a **complete on-premises container platform** –
comparable to Portainer, but **tailored** to your microservice architecture.

---

# → End of Block 20/20
**Technical Specification complete!**
