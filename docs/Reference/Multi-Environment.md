# v0.4 Multi-Environment Specification

**Version:** 0.4.0
**Status:** Planning

---

## Overview

This specification defines the multi-environment feature for ReadyStackGo v0.4. Organizations can manage multiple isolated environments (e.g., Development, Testing, Production), each with independent configurations, Docker hosts, and deployed stacks.

**Key Principles:**
- **Minimal Variant:** Single Docker host per environment (multi-node support deferred to v2.0+)
- **Docker Host Uniqueness:** Each Docker host URL can only be used by one environment (prevents conflicts)
- **Environment Isolation:** Each environment has separate connection strings, Docker host, and deployed containers
- **Domain-Driven Design:** Organization and Environment are core domain aggregates with proper validation
- **User Experience:** Simple environment switching in UI with context-aware dashboard
- **Backward Compatibility:** v0.3 single-environment configurations automatically migrate to default environment

---

## Terminology

| Term | Definition |
|------|------------|
| **Organization** | A tenant entity defined in wizard Step 2 (organizationId, organizationName) - **Aggregate Root** |
| **Environment** | An isolated deployment context within an organization (e.g., "dev", "test", "prod") - **Entity** within Organization aggregate |
| **Active Environment** | The currently selected environment in the UI, stored in user session |
| **Default Environment** | The first environment created during wizard, typically "production" |
| **Docker Host** | A single Docker daemon endpoint (e.g., `tcp://192.168.1.10:2375` or `unix:///var/run/docker.sock`) - Must be unique across all environments |

---

## Use Cases

### UC-1: Create Multiple Environments
**Actor:** Admin User
**Preconditions:** Wizard completed (v0.3), logged in
**Flow:**
1. Admin navigates to Settings → Environments
2. Clicks "Add Environment"
3. Enters environment details (ID, name, Docker host URL)
4. Configures connection strings (Transport, Persistence, EventStore)
5. System validates and saves environment configuration
6. New environment appears in environment selector

### UC-2: Switch Between Environments
**Actor:** Admin User
**Preconditions:** Multiple environments exist
**Flow:**
1. Admin clicks environment selector dropdown in header
2. Selects different environment (e.g., "test" → "production")
3. UI updates active environment context
4. Dashboard, containers, stacks refresh to show selected environment's data
5. All subsequent operations affect only the active environment

### UC-3: Deploy Stack to Specific Environment
**Actor:** Admin User
**Preconditions:** Active environment selected
**Flow:**
1. Admin clicks "Deploy Stack" in dashboard
2. Selects manifest version
3. System deploys containers to active environment's Docker host
4. Containers use active environment's connection strings
5. Deployment status shows in active environment's dashboard

### UC-4: Migrate from v0.3 to v0.4
**Actor:** System
**Preconditions:** Existing v0.3 installation with rsgo.contexts.json
**Flow:**
1. User upgrades to v0.4
2. On first startup, system detects v0.3 configuration
3. System creates default environment ("production")
4. Migrates existing rsgo.contexts.json → rsgo.contexts.production.json
5. Updates rsgo.system.json with environments array
6. Logs migration success

---

## Data Model Changes

### System Configuration (`rsgo.system.json`)

**v0.3 Schema:**
```json
{
  "organizationId": "my-company",
  "organizationName": "My Company Ltd.",
  "wizardState": "Installed",
  "installedVersion": "v0.3.0",
  "createdAt": "2025-01-19T10:30:00Z",
  "updatedAt": "2025-01-19T10:35:00Z"
}
```

**v0.4 Schema (Extended with Type Discriminator):**
```json
{
  "organizationId": "my-company",
  "organizationName": "My Company Ltd.",
  "wizardState": "Installed",
  "installedVersion": "v0.4.0",
  "createdAt": "2025-01-19T10:30:00Z",
  "updatedAt": "2025-01-20T14:20:00Z",
  "environments": [
    {
      "$type": "docker-socket",
      "id": "production",
      "name": "Production",
      "socketPath": "unix:///var/run/docker.sock",
      "isDefault": true,
      "createdAt": "2025-01-19T10:35:00Z"
    },
    {
      "$type": "docker-socket",
      "id": "test",
      "name": "Test Environment",
      "socketPath": "tcp://192.168.1.20:2375",
      "isDefault": false,
      "createdAt": "2025-01-20T14:20:00Z"
    }
  ]
}
```

**Schema Definition:**
```typescript
interface SystemConfig {
  organizationId: string;
  organizationName: string;
  wizardState: WizardState;
  installedVersion: string;
  createdAt: string; // ISO 8601
  updatedAt: string; // ISO 8601
  environments: Environment[]; // NEW in v0.4 - Polymorphic array
}

// Base interface for all environment types
interface Environment {
  $type: string;        // Type discriminator: "docker-socket", "docker-api", "docker-agent"
  id: string;           // Lowercase alphanumeric + hyphens (e.g., "production", "dev-env")
  name: string;         // Display name (e.g., "Production", "Development")
  isDefault: boolean;   // True for the default environment
  createdAt: string;    // ISO 8601
}

// Docker Socket Environment (v0.4 - ONLY THIS TYPE)
interface DockerSocketEnvironment extends Environment {
  $type: "docker-socket";
  socketPath: string;   // e.g., "unix:///var/run/docker.sock" or "npipe://./pipe/docker_engine"
}

// Future types (v0.5+)
interface DockerApiEnvironment extends Environment {
  $type: "docker-api";
  apiUrl: string;       // e.g., "tcp://192.168.1.10:2375"
  useTls: boolean;
  tlsCertPath?: string;
  tlsKeyPath?: string;
}

interface DockerAgentEnvironment extends Environment {
  $type: "docker-agent";
  agentUrl: string;     // e.g., "tcp://192.168.1.20:9001"
  agentSecret: string;
}
```

**Validation Rules:**
- At least one environment must exist after wizard completion
- Exactly one environment must have `isDefault: true`
- Environment IDs must be unique within organization
- Environment ID format: `/^[a-z0-9-]+$/` (lowercase, numbers, hyphens only)
- **Connection strings must be unique across all environments** (enforced via polymorphic `GetConnectionString()` method)
- `$type` field is required for JSON deserialization (type discriminator pattern)

### Configuration File Structure (v0.4)

**v0.3 Structure (OLD):**
```
/app/config/
  rsgo.system.json      ← Organization + wizard state
  rsgo.security.json    ← Admin credentials
  rsgo.tls.json         ← TLS certificates
  rsgo.contexts.json    ← Global connection strings (REMOVED in v0.4)
```

**v0.4 Structure (NEW):**
```
/app/config/
  rsgo.system.json                              ← Organization + environments
  rsgo.security.json                            ← Admin credentials
  rsgo.tls.json                                 ← TLS certificates
  deployments/
    production/
      readystack-core.deployment.json           ← Stack deployment config
      monitoring-stack.deployment.json
    test/
      readystack-core.deployment.json
```

**Key Change:** Connection strings moved from global `rsgo.contexts.json` to per-deployment configuration files.

---

## Wizard Changes

### Current v0.3 Wizard (4 Steps)
1. Create Admin Account
2. Set Organization
3. Configure Connections (Simple mode) ← **WILL BE REMOVED**
4. Complete Setup

### Proposed v0.4 Wizard (3 Steps)

**New Simplified Wizard:**
1. **Create Admin Account**
   - Username
   - Password (BCrypt hashed)

2. **Set Organization**
   - Organization ID (e.g., "acme-corp")
   - Organization Name (e.g., "Acme Corporation")

3. **Complete Setup**
   - Wizard finished
   - User redirected to dashboard
   - Can create environments via Settings UI

**Key Changes:**
- ✅ **Removed:** "Configure Connections" step (now stack-specific, not global)
- ✅ **Removed:** Mandatory environment creation during wizard
- ✅ **Simplified:** From 4 steps to 3 steps
- ✅ **Flexible:** Users can create environments when needed, not forced during setup

**Why This Approach:**
1. **Separation of Concerns:** Connection strings belong to stack deployments, not wizard setup
2. **Flexibility:** Not all users need environments immediately (e.g., testing, demo scenarios)
3. **Simplicity:** Faster onboarding, less overwhelming for new users
4. **Alignment with Domain Model:** Organization can exist without environments

### Wizard State Enum Update

```csharp
// v0.3
public enum WizardState
{
    NotStarted,
    AdminCreated,
    OrganizationSet,
    ConnectionsSet,  // ← REMOVED in v0.4
    Installed
}

// v0.4 (Simplified)
public enum WizardState
{
    NotStarted,
    AdminCreated,
    OrganizationSet,
    Installed        // Direct transition after OrganizationSet
}
```

**Breaking Change:** The `ConnectionsSet` state is removed. Existing v0.3 installations with `ConnectionsSet` will be automatically migrated to `Installed` during upgrade.

---

## API Changes

### New Endpoints

**Get All Environments**
```http
GET /api/environments
Authorization: Bearer {token}

Response 200 OK:
{
  "environments": [
    {
      "id": "production",
      "name": "Production",
      "dockerHost": "tcp://192.168.1.10:2375",
      "isDefault": true,
      "createdAt": "2025-01-19T10:35:00Z"
    },
    {
      "id": "test",
      "name": "Test Environment",
      "dockerHost": "tcp://192.168.1.20:2375",
      "isDefault": false,
      "createdAt": "2025-01-20T14:20:00Z"
    }
  ]
}
```

**Create Environment**
```http
POST /api/environments
Authorization: Bearer {token}
Content-Type: application/json

{
  "id": "staging",
  "name": "Staging Environment",
  "dockerHost": "tcp://192.168.1.30:2375"
}

Response 201 Created:
{
  "id": "staging",
  "name": "Staging Environment",
  "dockerHost": "tcp://192.168.1.30:2375",
  "isDefault": false,
  "createdAt": "2025-01-20T15:00:00Z"
}
```

**Update Environment**
```http
PUT /api/environments/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Staging (Updated)",
  "dockerHost": "tcp://192.168.1.35:2375"
}

Response 200 OK
```

**Delete Environment**
```http
DELETE /api/environments/{id}
Authorization: Bearer {token}

Response 204 No Content
```
**Constraints:** Cannot delete default environment or environment with active deployments.

**Get Environment Connections**
```http
GET /api/environments/{id}/connections
Authorization: Bearer {token}

Response 200 OK:
{
  "environmentId": "production",
  "connectionMode": "Simple",
  "simple": {
    "transport": "amqp://rabbitmq-prod:5672",
    "persistence": "Host=db-prod;Port=5432;...",
    "eventStore": "esdb://eventstore-prod:2113?tls=false"
  }
}
```

**Update Environment Connections**
```http
PUT /api/environments/{id}/connections
Authorization: Bearer {token}
Content-Type: application/json

{
  "connectionMode": "Simple",
  "simple": {
    "transport": "amqp://rabbitmq-prod:5672",
    "persistence": "Host=db-prod;Port=5432;...",
    "eventStore": "esdb://eventstore-prod:2113?tls=false"
  }
}

Response 200 OK
```

### Modified Endpoints

**Container Endpoints (Environment-Scoped)**
```http
# v0.3
GET /api/containers

# v0.4
GET /api/containers?environment={environmentId}
```

**Deployment Endpoints (Environment-Scoped)**
```http
# v0.3
POST /api/deployments

# v0.4
POST /api/deployments
{
  "environmentId": "production",  ← NEW REQUIRED FIELD
  "manifestPath": "/app/manifests/v1.0.0.json"
}
```

---

## Domain Model

### Architecture

**Aggregate Design:** Organization is the **Aggregate Root**, and Environment is an **Entity** within the Organization aggregate. This design ensures all invariants (uniqueness constraints, default environment rules) can be enforced within a single transactional boundary.

**New Domain Structure:**

```
src/ReadyStackGo.Domain/
├── Auth/
│   ├── User.cs
│   └── UserRole.cs
├── Organization/                      ← NEW in v0.4
│   ├── Organization.cs                ← Aggregate Root
│   ├── Environment.cs                 ← Abstract base class (Entity)
│   ├── DockerSocketEnvironment.cs     ← Concrete implementation (v0.4 ONLY)
│   ├── DockerApiEnvironment.cs        ← Future (v0.5+)
│   ├── DockerAgentEnvironment.cs      ← Future (v0.5+)
│   ├── KubernetesEnvironment.cs       ← Future (v2.0+)
│   ├── OrganizationId.cs              ← Value Object
│   ├── EnvironmentId.cs               ← Value Object
│   └── Exceptions/
│       └── OrganizationException.cs
└── Wizard/
    ├── WizardState.cs
    └── ConnectionMode.cs
```

**Type Hierarchy Diagram:**

```
Organization (Aggregate Root)
└── owns → Environment* (Abstract Entity)
            ├── DockerSocketEnvironment (v0.4)
            │   └── SocketPath: string
            ├── DockerApiEnvironment (v0.5+)
            │   ├── ApiUrl: string
            │   ├── UseTls: bool
            │   └── TlsCert/Key paths
            ├── DockerAgentEnvironment (v0.5+)
            │   ├── AgentUrl: string
            │   └── AgentSecret: string
            └── KubernetesEnvironment (v2.0+)
                ├── KubeConfigPath: string
                ├── Context: string
                └── Namespace: string
```

**Key Design Decision:**
- Environment is **NOT** a separate aggregate root
- Environment entities are owned by and managed through the Organization aggregate
- This ensures transactional consistency for invariants like "exactly one default environment" and "unique Docker hosts within organization"

### Domain Aggregate: Organization (Aggregate Root)

```csharp
namespace ReadyStackGo.Domain.Organization;

public class Organization
{
    public OrganizationId Id { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<Environment> _environments = new();
    public IReadOnlyCollection<Environment> Environments => _environments.AsReadOnly();

    private Organization() { }

    // Factory Method: Create Organization WITHOUT environments
    public static Organization Create(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new OrganizationException("Organization ID cannot be empty");

        if (!OrganizationId.IsValid(id))
            throw new OrganizationException($"Invalid organization ID format: {id}");

        if (string.IsNullOrWhiteSpace(name))
            throw new OrganizationException("Organization name cannot be empty");

        return new Organization
        {
            Id = new OrganizationId(id),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // Business Logic: Add new Docker Socket Environment (v0.4)
    public DockerSocketEnvironment AddDockerSocketEnvironment(
        string id,
        string name,
        string socketPath,
        bool setAsDefault = false)
    {
        ValidateNewEnvironment(id);

        var environment = DockerSocketEnvironment.Create(id, name, socketPath, isDefault: false);

        // Invariant: Connection string must be unique (polymorphic check)
        if (_environments.Any(e => e.GetConnectionString() == environment.GetConnectionString()))
            throw new OrganizationException(
                $"Connection '{environment.GetConnectionString()}' is already used by another environment");

        _environments.Add(environment);

        // If this is the first environment OR user explicitly wants it as default
        if (setAsDefault || _environments.Count == 1)
        {
            // Remove default from all other environments
            foreach (var env in _environments.Where(e => e.IsDefault && e.Id.Value != id))
            {
                env.UnmarkAsDefault();
            }
            environment.MarkAsDefault();
        }

        UpdatedAt = DateTime.UtcNow;

        return environment;
    }

    // Helper method for common validation
    private void ValidateNewEnvironment(string id)
    {
        if (_environments.Any(e => e.Id.Value == id))
            throw new OrganizationException($"Environment with ID '{id}' already exists");
    }

    // Business Logic: Remove Environment
    public void RemoveEnvironment(string environmentId)
    {
        var environment = _environments.FirstOrDefault(e => e.Id.Value == environmentId)
            ?? throw new OrganizationException($"Environment '{environmentId}' not found");

        // If deleting the default environment and other environments exist, promote another to default
        if (environment.IsDefault && _environments.Count > 1)
        {
            throw new OrganizationException(
                "Cannot delete default environment. Set another environment as default first.");
        }

        _environments.Remove(environment);
        UpdatedAt = DateTime.UtcNow;
    }

    // Business Logic: Change default Environment
    public void SetDefaultEnvironment(string environmentId)
    {
        var newDefault = _environments.FirstOrDefault(e => e.Id.Value == environmentId)
            ?? throw new OrganizationException($"Environment '{environmentId}' not found");

        // Remove default from all other environments
        foreach (var env in _environments.Where(e => e.IsDefault))
        {
            env.UnmarkAsDefault();
        }

        // Set new default
        newDefault.MarkAsDefault();
        UpdatedAt = DateTime.UtcNow;
    }

    // Business Logic: Update Environment Connection (polymorphic)
    public void UpdateEnvironmentConnection(string environmentId, string newConnectionString)
    {
        var environment = _environments.FirstOrDefault(e => e.Id.Value == environmentId)
            ?? throw new OrganizationException($"Environment '{environmentId}' not found");

        // Invariant: Connection string must be unique (except for this environment)
        if (_environments.Any(e => e.Id.Value != environmentId && e.GetConnectionString() == newConnectionString))
            throw new OrganizationException(
                $"Connection '{newConnectionString}' is already used by another environment");

        // Type-specific update (v0.4 only supports DockerSocketEnvironment)
        if (environment is DockerSocketEnvironment dockerSocketEnv)
        {
            dockerSocketEnv.UpdateSocketPath(newConnectionString);
        }
        else
        {
            throw new OrganizationException(
                $"Cannot update connection for environment type: {environment.GetTypeName()}");
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new OrganizationException("Organization name cannot be empty");

        Name = newName;
        UpdatedAt = DateTime.UtcNow;
    }

    public Environment? GetDefaultEnvironment()
    {
        return _environments.FirstOrDefault(e => e.IsDefault);
    }

    public bool HasEnvironments => _environments.Count > 0;

    public Environment? GetEnvironment(string environmentId)
    {
        return _environments.FirstOrDefault(e => e.Id.Value == environmentId);
    }
}

// Value Object
public record OrganizationId
{
    public string Value { get; }

    public OrganizationId(string value)
    {
        if (!IsValid(value))
            throw new OrganizationException($"Invalid organization ID: {value}");

        Value = value;
    }

    public static bool IsValid(string id) =>
        !string.IsNullOrWhiteSpace(id) &&
        Regex.IsMatch(id, @"^[a-z0-9-]+$");

    public override string ToString() => Value;
}
```

### Domain Entity: Environment (Polymorphic Type Hierarchy)

**Important:** Environment is an **Entity**, not an Aggregate Root. It can only be created and modified through the Organization aggregate.

**Design Pattern:** Environment uses **Polymorphism** (Strategy Pattern) instead of enums to support different environment types (Docker Socket, Docker API, Docker Agent, Kubernetes, etc.). Each concrete environment type encapsulates its own connection logic and validation rules.

**Type Hierarchy:**

```
Environment (Abstract Base)
├── DockerSocketEnvironment (v0.4 - ONLY THIS ONE)
├── DockerApiEnvironment (v0.5+ Future)
├── DockerAgentEnvironment (v0.5+ Future)
└── KubernetesEnvironment (v2.0+ Future)
```

#### Abstract Base Class: Environment

```csharp
namespace ReadyStackGo.Domain.Organization;

/// <summary>
/// Abstract base class for all environment types.
/// Uses polymorphism to support different container orchestration platforms.
/// </summary>
public abstract class Environment
{
    public EnvironmentId Id { get; protected set; }
    public string Name { get; protected set; }
    public bool IsDefault { get; protected set; }
    public DateTime CreatedAt { get; protected set; }

    protected Environment() { }

    // Template Methods (implemented by derived classes)

    /// <summary>
    /// Returns the connection string for this environment type.
    /// Examples: "unix:///var/run/docker.sock", "tcp://192.168.1.10:2375"
    /// </summary>
    public abstract string GetConnectionString();

    /// <summary>
    /// Validates connectivity to this environment's orchestrator.
    /// </summary>
    public abstract Task<bool> ValidateConnectionAsync();

    /// <summary>
    /// Returns human-readable type name for UI display.
    /// Examples: "Docker Socket", "Docker API", "Docker Agent"
    /// </summary>
    public abstract string GetTypeName();

    /// <summary>
    /// Returns type identifier for JSON serialization discriminator.
    /// Examples: "docker-socket", "docker-api", "docker-agent"
    /// </summary>
    public abstract string GetTypeIdentifier();

    // Internal methods (only Organization aggregate can call these)

    internal void MarkAsDefault()
    {
        IsDefault = true;
    }

    internal void UnmarkAsDefault()
    {
        IsDefault = false;
    }

    internal void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new OrganizationException("Environment name cannot be empty");

        Name = newName;
    }
}

// Value Object
public record EnvironmentId
{
    public string Value { get; }

    public EnvironmentId(string value)
    {
        if (!IsValid(value))
            throw new OrganizationException($"Invalid environment ID: {value}");

        Value = value;
    }

    public static bool IsValid(string id) =>
        !string.IsNullOrWhiteSpace(id) &&
        Regex.IsMatch(id, @"^[a-z0-9-]+$");

    public override string ToString() => Value;
}
```

#### Concrete Implementation: DockerSocketEnvironment (v0.4)

**v0.4 Implementation:** Only Docker Socket environments are supported in v0.4. This type connects to a Docker daemon via Unix socket or named pipe.

```csharp
namespace ReadyStackGo.Domain.Organization;

/// <summary>
/// Docker Socket environment - connects to Docker daemon via Unix socket or named pipe.
/// This is the ONLY environment type implemented in v0.4.
/// </summary>
public class DockerSocketEnvironment : Environment
{
    public string SocketPath { get; private set; }

    private DockerSocketEnvironment() { }

    // Factory Method (internal - only Organization can create)
    internal static DockerSocketEnvironment Create(
        string id,
        string name,
        string socketPath,
        bool isDefault = false)
    {
        if (!EnvironmentId.IsValid(id))
            throw new OrganizationException($"Invalid environment ID: {id}");

        if (string.IsNullOrWhiteSpace(name))
            throw new OrganizationException("Environment name cannot be empty");

        if (string.IsNullOrWhiteSpace(socketPath))
            throw new OrganizationException("Socket path cannot be empty");

        // Normalize socket path (add unix:// prefix if missing)
        var normalizedPath = NormalizeSocketPath(socketPath);
        ValidateSocketPath(normalizedPath);

        return new DockerSocketEnvironment
        {
            Id = new EnvironmentId(id),
            Name = name,
            SocketPath = normalizedPath,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };
    }

    public override string GetConnectionString() => SocketPath;

    public override async Task<bool> ValidateConnectionAsync()
    {
        try
        {
            // Strip unix:// prefix for file system check
            var path = SocketPath.Replace("unix://", "").Replace("npipe://", "");

            // On Linux/macOS: Check if Unix socket exists
            // On Windows: Named pipe check (more complex, simplified here)
            return await Task.FromResult(File.Exists(path));
        }
        catch
        {
            return false;
        }
    }

    public override string GetTypeName() => "Docker Socket";

    public override string GetTypeIdentifier() => "docker-socket";

    internal void UpdateSocketPath(string newSocketPath)
    {
        var normalizedPath = NormalizeSocketPath(newSocketPath);
        ValidateSocketPath(normalizedPath);
        SocketPath = normalizedPath;
    }

    private static string NormalizeSocketPath(string path)
    {
        if (path.StartsWith("unix://") || path.StartsWith("npipe://"))
            return path;

        // Auto-detect platform and add appropriate prefix
        if (OperatingSystem.IsWindows())
            return $"npipe://{path}";
        else
            return $"unix://{path}";
    }

    private static void ValidateSocketPath(string path)
    {
        if (!path.StartsWith("unix://") && !path.StartsWith("npipe://"))
            throw new OrganizationException($"Invalid socket path: {path}. Must start with unix:// or npipe://");
    }
}
```

#### Future Environment Types (v0.5+)

These types will be implemented in future releases:

**DockerApiEnvironment** (v0.5+):
```csharp
/// <summary>
/// Docker API environment - connects to Docker daemon via TCP (HTTP/HTTPS).
/// Supports TLS authentication.
/// </summary>
public class DockerApiEnvironment : Environment
{
    public string ApiUrl { get; private set; }          // e.g., "tcp://192.168.1.10:2375"
    public bool UseTls { get; private set; }
    public string? TlsCertPath { get; private set; }    // Optional client certificate
    public string? TlsKeyPath { get; private set; }     // Optional client key

    internal static DockerApiEnvironment Create(
        string id,
        string name,
        string apiUrl,
        bool useTls = false,
        string? tlsCertPath = null,
        string? tlsKeyPath = null,
        bool isDefault = false)
    {
        // Validation logic...
        throw new NotImplementedException("Docker API environments will be implemented in v0.5");
    }

    public override string GetConnectionString() => ApiUrl;
    public override async Task<bool> ValidateConnectionAsync() { /* HTTP ping */ }
    public override string GetTypeName() => "Docker API";
    public override string GetTypeIdentifier() => "docker-api";
}
```

**DockerAgentEnvironment** (v0.5+):
```csharp
/// <summary>
/// Docker Agent environment - connects to Portainer Edge Agent.
/// </summary>
public class DockerAgentEnvironment : Environment
{
    public string AgentUrl { get; private set; }        // e.g., "tcp://192.168.1.20:9001"
    public string AgentSecret { get; private set; }     // Edge agent secret key

    internal static DockerAgentEnvironment Create(
        string id,
        string name,
        string agentUrl,
        string agentSecret,
        bool isDefault = false)
    {
        throw new NotImplementedException("Docker Agent environments will be implemented in v0.5");
    }

    public override string GetConnectionString() => AgentUrl;
    public override async Task<bool> ValidateConnectionAsync() { /* Agent ping */ }
    public override string GetTypeName() => "Docker Agent";
    public override string GetTypeIdentifier() => "docker-agent";
}
```

**KubernetesEnvironment** (v2.0+):
```csharp
/// <summary>
/// Kubernetes environment - connects to a Kubernetes cluster.
/// </summary>
public class KubernetesEnvironment : Environment
{
    public string KubeConfigPath { get; private set; }
    public string Context { get; private set; }
    public string Namespace { get; private set; }

    public override string GetConnectionString() => $"{Context}@{Namespace}";
    public override async Task<bool> ValidateConnectionAsync() { /* kubectl ping */ }
    public override string GetTypeName() => "Kubernetes";
    public override string GetTypeIdentifier() => "kubernetes";
}
```

### Domain Validation Rules

**Organization:**
- ID must be lowercase alphanumeric with hyphens only (`^[a-z0-9-]+$`)
- Name cannot be empty
- IDs are immutable once created
- **Can exist without any environments** (flexible setup)
- If environments exist, at most one can be marked as default
- First added environment automatically becomes default
- **Connection strings must be unique across all environments** (polymorphic validation via `GetConnectionString()`)

**Environment (Base Class):**
- ID must be lowercase alphanumeric with hyphens only (`^[a-z0-9-]+$`)
- Name cannot be empty
- Environment IDs are immutable once created
- Cannot delete default environment unless another environment is set as default first
- Can delete all environments (organization can exist without environments)

**DockerSocketEnvironment (v0.4 Specific):**
- Socket path cannot be empty
- Socket path must start with `unix://` (Linux/macOS) or `npipe://` (Windows)
- Auto-normalization: `/var/run/docker.sock` → `unix:///var/run/docker.sock`
- Connection string uniqueness enforced at Organization aggregate level

**Future Environment Types (v0.5+):**
- **DockerApiEnvironment:** API URL validation, optional TLS certificate paths
- **DockerAgentEnvironment:** Agent URL validation, secret key required
- **KubernetesEnvironment:** Kubeconfig path validation, context and namespace validation

### Why Domain Aggregates?

**Benefits:**
1. **Encapsulation:** Business rules are enforced in the domain, not scattered across services
2. **Type Safety:** `OrganizationId` and `EnvironmentId` prevent string-based errors
3. **Polymorphism:** Environment types use Strategy Pattern instead of enums for extensibility
4. **Testability:** Domain logic can be tested independently without infrastructure
5. **Maintainability:** Clear separation between domain logic and persistence
6. **Scalability:** Easy to extend with new environment types without modifying existing code
7. **Transactional Consistency:** Single aggregate = single transaction boundary = ACID guarantees

---

## Persistence Strategy

### v0.4: JSON-Based File Storage

**Decision:** Continue using JSON files for v0.4 to maintain simplicity and consistency with v0.3.

**Rationale:**
- ✅ No external database setup required
- ✅ Simple deployment (mount `/app/config` volume)
- ✅ Easy backup and restore (copy config directory)
- ✅ Human-readable configuration
- ✅ Consistent with v0.3 architecture
- ✅ Adequate for single-user, single-organization use case

**File Structure:**

```
/app/config/
  rsgo.system.json                  ← Organization + Environments
  rsgo.security.json                ← Admin credentials
  rsgo.contexts.production.json     ← Production environment connections
  rsgo.contexts.test.json           ← Test environment connections
  rsgo.tls.json                     ← TLS configuration
```

**System Configuration Schema (`rsgo.system.json`):**

```json
{
  "organization": {
    "id": "acme-corp",
    "name": "Acme Corporation",
    "createdAt": "2025-01-19T10:00:00Z",
    "updatedAt": "2025-01-20T14:30:00Z",
    "environments": [
      {
        "$type": "docker-socket",
        "id": "production",
        "name": "Production",
        "socketPath": "unix:///var/run/docker.sock",
        "isDefault": true,
        "createdAt": "2025-01-19T10:00:00Z"
      },
      {
        "$type": "docker-socket",
        "id": "test",
        "name": "Test Environment",
        "socketPath": "tcp://192.168.1.20:2375",
        "isDefault": false,
        "createdAt": "2025-01-20T14:30:00Z"
      }
    ]
  },
  "wizardState": "Installed",
  "installedVersion": "v0.4.0"
}
```

**Repository Implementation:**

```csharp
public class OrganizationRepository : IOrganizationRepository
{
    private readonly string _configPath = "/app/config/rsgo.system.json";
    private readonly SemaphoreSlim _lock = new(1, 1); // Prevent concurrent writes
    private readonly JsonSerializerOptions _jsonOptions;

    public async Task<Organization> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_configPath))
                throw new InvalidOperationException("Organization not found. Complete the wizard first.");

            var json = await File.ReadAllTextAsync(_configPath);
            var dto = JsonSerializer.Deserialize<SystemConfigDto>(json, _jsonOptions);

            return MapToDomain(dto.Organization);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(Organization organization)
    {
        await _lock.WaitAsync();
        try
        {
            var json = File.Exists(_configPath)
                ? await File.ReadAllTextAsync(_configPath)
                : "{}";

            var dto = JsonSerializer.Deserialize<SystemConfigDto>(json, _jsonOptions)
                ?? new SystemConfigDto();

            dto.Organization = MapToDto(organization);
            dto.UpdatedAt = DateTime.UtcNow;

            var updatedJson = JsonSerializer.Serialize(dto, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, updatedJson);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Organization MapToDomain(OrganizationDto dto)
    {
        // Create organization without environments
        var org = Organization.Create(dto.Id, dto.Name);

        // Add environments if any exist (organization can exist without environments)
        if (dto.Environments != null && dto.Environments.Count > 0)
        {
            // Find default environment (if one exists)
            var defaultEnv = dto.Environments.FirstOrDefault(e => e.IsDefault);

            // Add default environment first (if exists)
            if (defaultEnv is DockerSocketEnvironment defaultSocketEnv)
            {
                org.AddDockerSocketEnvironment(
                    defaultSocketEnv.Id.Value,
                    defaultSocketEnv.Name,
                    defaultSocketEnv.SocketPath,
                    setAsDefault: true);
            }

            // Add remaining environments (polymorphic, but v0.4 only supports DockerSocket)
            foreach (var env in dto.Environments.Where(e => !e.IsDefault))
            {
                if (env is DockerSocketEnvironment socketEnv)
                {
                    org.AddDockerSocketEnvironment(
                        socketEnv.Id.Value,
                        socketEnv.Name,
                        socketEnv.SocketPath,
                        setAsDefault: false);
                }
                else
                {
                    // Future-proofing: Skip unsupported types instead of throwing
                    // This allows forward compatibility when loading v0.5+ configs in v0.4
                    _logger.LogWarning(
                        "Skipping unsupported environment type '{Type}' (ID: {Id}). Upgrade to a newer version to use this environment.",
                        env.GetTypeName(), env.Id.Value);
                }
            }
        }

        return org;
    }
}
```

**Concurrency Handling:**
- `SemaphoreSlim` prevents concurrent file writes
- Last-write-wins strategy (acceptable for single-user scenario)
- Future: Optimistic concurrency with version numbers

**Limitations:**
- ⚠️ No ACID transactions (file write is atomic, but not with other config files)
- ⚠️ Not suitable for multi-user scenarios (no locking across instances)
- ⚠️ Performance degrades with many environments (>100)

### Future: SQLite Migration (v0.5 or v0.6)

**When to migrate:**
- Multi-user support is added
- More than ~20 environments per organization
- Need for advanced querying or reporting

**Benefits of SQLite:**
- ✅ ACID transactions
- ✅ Better concurrency handling
- ✅ EF Core support (migrations, LINQ queries)
- ✅ Still file-based (no external database)
- ✅ Easy backup (single `.db` file)

**Migration Plan:**
1. Keep `IOrganizationRepository` interface unchanged
2. Create `SqliteOrganizationRepository` implementation
3. Provide migration tool: `rsgo.system.json` → `readystackgo.db`
4. Update documentation

**Schema (EF Core):**

```csharp
public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new OrganizationId(value));

        builder.Property(o => o.Name).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        // Environments as owned entities
        builder.OwnsMany(o => o.Environments, env =>
        {
            env.ToTable("Environments");
            env.WithOwner().HasForeignKey("OrganizationId");

            env.Property(e => e.Id)
                .HasConversion(
                    id => id.Value,
                    value => new EnvironmentId(value));

            env.Property(e => e.Name).IsRequired();
            env.Property(e => e.DockerHost).IsRequired();
            env.Property(e => e.IsDefault).IsRequired();
            env.Property(e => e.CreatedAt).IsRequired();

            env.HasIndex(e => new { e.OrganizationId, e.DockerHost }).IsUnique();
        });
    }
}
```

**Recommendation:** Defer SQLite migration to v0.5 or v0.6 when multi-user support is added.

### JSON Serialization with Type Discriminator

**Challenge:** Polymorphic environment types must be correctly serialized/deserialized to/from JSON. System.Text.Json requires custom converters to handle type hierarchies.

**Solution:** Implement a custom `JsonConverter<Environment>` that uses the `$type` field as a discriminator.

**EnvironmentJsonConverter Implementation:**

```csharp
public class EnvironmentJsonConverter : JsonConverter<Environment>
{
    private const string TypeDiscriminatorProperty = "$type";

    public override Environment Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Read the entire JSON object into a JsonDocument
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        // Extract the $type discriminator
        if (!root.TryGetProperty(TypeDiscriminatorProperty, out var typeProperty))
            throw new JsonException("Missing $type property in Environment JSON");

        var typeIdentifier = typeProperty.GetString();

        // Deserialize to the correct concrete type
        return typeIdentifier switch
        {
            "docker-socket" => JsonSerializer.Deserialize<DockerSocketEnvironment>(
                root.GetRawText(), options)!,

            "docker-api" => JsonSerializer.Deserialize<DockerApiEnvironment>(
                root.GetRawText(), options)!,

            "docker-agent" => JsonSerializer.Deserialize<DockerAgentEnvironment>(
                root.GetRawText(), options)!,

            "kubernetes" => JsonSerializer.Deserialize<KubernetesEnvironment>(
                root.GetRawText(), options)!,

            _ => throw new JsonException($"Unknown environment type: {typeIdentifier}")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        Environment value,
        JsonSerializerOptions options)
    {
        // Start writing the JSON object
        writer.WriteStartObject();

        // Write the $type discriminator first
        writer.WriteString(TypeDiscriminatorProperty, value.GetTypeIdentifier());

        // Write base properties
        writer.WriteString("id", value.Id.Value);
        writer.WriteString("name", value.Name);
        writer.WriteBoolean("isDefault", value.IsDefault);
        writer.WriteString("createdAt", value.CreatedAt.ToString("o")); // ISO 8601

        // Write type-specific properties
        switch (value)
        {
            case DockerSocketEnvironment dockerSocket:
                writer.WriteString("socketPath", dockerSocket.SocketPath);
                break;

            case DockerApiEnvironment dockerApi:
                writer.WriteString("apiUrl", dockerApi.ApiUrl);
                writer.WriteBoolean("useTls", dockerApi.UseTls);
                if (dockerApi.TlsCertPath != null)
                    writer.WriteString("tlsCertPath", dockerApi.TlsCertPath);
                if (dockerApi.TlsKeyPath != null)
                    writer.WriteString("tlsKeyPath", dockerApi.TlsKeyPath);
                break;

            case DockerAgentEnvironment dockerAgent:
                writer.WriteString("agentUrl", dockerAgent.AgentUrl);
                writer.WriteString("agentSecret", dockerAgent.AgentSecret);
                break;

            case KubernetesEnvironment kubernetes:
                writer.WriteString("kubeConfigPath", kubernetes.KubeConfigPath);
                writer.WriteString("context", kubernetes.Context);
                writer.WriteString("namespace", kubernetes.Namespace);
                break;

            default:
                throw new JsonException($"Unsupported environment type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }
}
```

**Registration in `ConfigStore`:**

```csharp
public class ConfigStore : IConfigStore
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigStore()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new EnvironmentJsonConverter() } // Register custom converter
        };
    }

    // ... rest of ConfigStore implementation
}
```

**Benefits:**
- ✅ Single `$type` field drives deserialization
- ✅ No need for separate JSON files per type
- ✅ Easy to add new environment types (just add case to switch)
- ✅ Type-safe deserialization with proper domain objects

**Example JSON with Mixed Types (v0.5+):**

```json
{
  "organization": {
    "id": "acme-corp",
    "name": "Acme Corporation",
    "environments": [
      {
        "$type": "docker-socket",
        "id": "local-dev",
        "name": "Local Development",
        "socketPath": "unix:///var/run/docker.sock",
        "isDefault": false,
        "createdAt": "2025-01-19T10:00:00Z"
      },
      {
        "$type": "docker-api",
        "id": "production",
        "name": "Production Cluster",
        "apiUrl": "tcp://prod-docker.acme.local:2376",
        "useTls": true,
        "tlsCertPath": "/app/config/tls/client-cert.pem",
        "tlsKeyPath": "/app/config/tls/client-key.pem",
        "isDefault": true,
        "createdAt": "2025-01-19T11:00:00Z"
      },
      {
        "$type": "docker-agent",
        "id": "remote-edge",
        "name": "Edge Location",
        "agentUrl": "tcp://edge.acme.local:9001",
        "agentSecret": "••••••••",
        "isDefault": false,
        "createdAt": "2025-01-20T09:00:00Z"
      }
    ]
  }
}
```

---

## Stack-Specific Configuration System

### Problem Statement

**Old Approach (v0.3 - WRONG):**
- Connection strings configured globally in wizard Step 3
- All stacks share the same connection strings
- No flexibility for different stack requirements
- Connection strings stored in `rsgo.contexts.json`

**New Approach (v0.4+ - CORRECT):**
- **Connection strings are stack-specific**, not global
- Each stack deployment can have different configuration values
- Configuration happens during **stack deployment**, not during wizard setup
- Supports multiple stack formats (prioritized by implementation phase)

### Implementation Strategy: Phased Approach

**Phase 1 - v0.4: Docker Compose Format (Portainer-style)**
- ✅ Use standard `docker-compose.yml` files
- ✅ Automatic environment variable detection from `${VARIABLE}` syntax
- ✅ Dynamic UI generation based on detected variables
- ✅ Quick deployment of existing stacks
- ✅ Familiar format for Docker users
- ⚠️ Limited validation (no type checking, no required field enforcement)

**Phase 2 - v0.5+: Custom Manifest Format (Enhanced)**
- ✅ Full validation with type checking and regex patterns
- ✅ Required field enforcement
- ✅ Display names and descriptions for better UX
- ✅ Sensitive field marking
- ✅ Default values and documentation
- ✅ Advanced configuration types (numbers, paths, database connections)

**Rationale:**
- Docker Compose allows **immediate deployment** of existing stacks
- Custom manifest format can be added later as **optional enhancement**
- Both formats can coexist (users choose which to use)

---

### Phase 1: Docker Compose Configuration (v0.4)

#### Format: Standard Docker Compose

**File:** `docker-compose.yml`

```yaml
version: '3.8'

services:
  api-gateway:
    image: readystack/api-gateway:1.0.0
    ports:
      - "${API_PORT:-8080}:8080"
    environment:
      - TRANSPORT_URL=${TRANSPORT_URL}
      - PERSISTENCE_CONNECTION=${PERSISTENCE_CONNECTION}
      - EVENTSTORE_URL=${EVENTSTORE_URL:-esdb://eventstore:2113}
    depends_on:
      - postgres
      - rabbitmq
      - eventstore

  order-service:
    image: readystack/order-service:1.0.0
    ports:
      - "${ORDER_SERVICE_PORT:-8081}:8080"
    environment:
      - TRANSPORT_URL=${TRANSPORT_URL}
      - PERSISTENCE_CONNECTION=${PERSISTENCE_CONNECTION}
      - EVENTSTORE_URL=${EVENTSTORE_URL:-esdb://eventstore:2113}
    depends_on:
      - postgres
      - rabbitmq
      - eventstore

  postgres:
    image: postgres:15
    environment:
      - POSTGRES_DB=${POSTGRES_DB:-readystack}
      - POSTGRES_USER=${POSTGRES_USER:-admin}
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"

  eventstore:
    image: eventstore/eventstore:latest
    environment:
      - EVENTSTORE_INSECURE=true
    ports:
      - "2113:2113"

volumes:
  postgres-data:
```

#### Variable Detection Algorithm

**How ReadyStackGo Detects Variables:**

1. **Parse `docker-compose.yml`** using YAML parser
2. **Scan all `environment` sections** for `${VARIABLE}` or `${VARIABLE:-default}` patterns
3. **Extract variable names and defaults**:
   - `${TRANSPORT_URL}` → Variable: `TRANSPORT_URL`, Default: `null`
   - `${API_PORT:-8080}` → Variable: `API_PORT`, Default: `8080`
4. **Generate UI input fields** dynamically
5. **Store configuration** per deployment

**C# Implementation (Pseudocode):**

```csharp
public class DockerComposeParser
{
    public List<EnvironmentVariable> ExtractVariables(string composeYaml)
    {
        var variables = new List<EnvironmentVariable>();
        var yaml = new YamlStream();
        yaml.Load(new StringReader(composeYaml));

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var services = (YamlMappingNode)root.Children["services"];

        foreach (var service in services.Children)
        {
            var serviceNode = (YamlMappingNode)service.Value;

            if (serviceNode.Children.ContainsKey("environment"))
            {
                var envNode = serviceNode.Children["environment"];

                if (envNode is YamlSequenceNode envList)
                {
                    foreach (var envItem in envList.Children)
                    {
                        var envString = ((YamlScalarNode)envItem).Value;

                        // Match patterns: ${VAR} or ${VAR:-default}
                        var matches = Regex.Matches(envString, @"\$\{([^}:]+)(?::-(.*))?\}");

                        foreach (Match match in matches)
                        {
                            var varName = match.Groups[1].Value;
                            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;

                            if (!variables.Any(v => v.Name == varName))
                            {
                                variables.Add(new EnvironmentVariable
                                {
                                    Name = varName,
                                    DefaultValue = defaultValue,
                                    DisplayName = FormatDisplayName(varName), // "TRANSPORT_URL" → "Transport URL"
                                    Required = defaultValue == null
                                });
                            }
                        }
                    }
                }
            }
        }

        return variables;
    }

    private string FormatDisplayName(string varName)
    {
        // Convert "TRANSPORT_URL" to "Transport URL"
        return string.Join(" ", varName.Split('_')
            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
    }
}

public class EnvironmentVariable
{
    public string Name { get; set; }
    public string? DefaultValue { get; set; }
    public string DisplayName { get; set; }
    public bool Required { get; set; }
}
```

#### Deployment Configuration Storage

When a stack is deployed to an environment, its configuration is stored per deployment instance:

**File:** `/app/config/deployments/{environmentId}/{stackName}.deployment.json`

```json
{
  "deploymentId": "deployment-12345",
  "environmentId": "production",
  "stackName": "readystack-core",
  "composeFile": "docker-compose.yml",
  "deployedAt": "2025-01-20T10:00:00Z",
  "configuration": {
    "TRANSPORT_URL": "amqp://prod-rabbitmq.acme.local:5672",
    "PERSISTENCE_CONNECTION": "Host=prod-db.acme.local;Port=5432;Database=readystack_prod;Username=prod_admin;Password=***",
    "EVENTSTORE_URL": "esdb://prod-eventstore.acme.local:2113?tls=true",
    "API_PORT": "8080",
    "ORDER_SERVICE_PORT": "8081",
    "POSTGRES_DB": "readystack_prod",
    "POSTGRES_USER": "admin",
    "POSTGRES_PASSWORD": "***"
  },
  "containers": [
    {
      "serviceName": "api-gateway",
      "containerId": "abc123def456",
      "status": "running",
      "ports": ["8080:8080"]
    },
    {
      "serviceName": "order-service",
      "containerId": "def456ghi789",
      "status": "running",
      "ports": ["8081:8080"]
    },
    {
      "serviceName": "postgres",
      "containerId": "ghi789jkl012",
      "status": "running",
      "ports": []
    },
    {
      "serviceName": "rabbitmq",
      "containerId": "jkl012mno345",
      "status": "running",
      "ports": ["5672:5672", "15672:15672"]
    },
    {
      "serviceName": "eventstore",
      "containerId": "mno345pqr678",
      "status": "running",
      "ports": ["2113:2113"]
    }
  ]
}
```

**Key Properties:**
- `deploymentId`: Unique identifier for this deployment
- `environmentId`: Which environment the stack is deployed to
- `stackName`: Name of the stack (derived from compose file or user input)
- `composeFile`: Original compose file name
- `configuration`: Flat key-value pairs for all environment variables
- `containers`: List of deployed containers with Docker IDs

#### Deployment Flow with Docker Compose (v0.4)

1. **User uploads `docker-compose.yml`** via UI or selects from existing stacks
2. **System parses compose file** and extracts environment variables
3. **Selects environment** (e.g., "Production")
4. **Configuration UI appears** - dynamically generated from detected variables
   - Shows all detected variables
   - Pre-fills defaults from `${VAR:-default}` syntax
   - Marks variables without defaults as required
5. **User provides/confirms values**
   - `TRANSPORT_URL`: `amqp://prod-rabbitmq:5672`
   - `PERSISTENCE_CONNECTION`: `Host=prod-db;Port=5432;Database=readystack_prod;...`
   - `EVENTSTORE_URL`: `esdb://prod-es:2113` (default already filled)
   - `POSTGRES_PASSWORD`: `***` (required, no default)
6. **System validates basic requirements** (non-empty required fields)
7. **Deployment starts** - Docker Compose executed with environment variables
8. **Configuration saved** to deployment file

#### UI Mockup: Docker Compose Deployment (v0.4)

```
┌─────────────────────────────────────────────────────────────┐
│ Deploy Stack from Docker Compose                            │
│ Environment: Production                                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│ Stack Configuration                                          │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Stack Name *                                             │ │
│ │ ┌──────────────────────────────────────────────────────┐│ │
│ │ │ readystack-core                                      ││ │
│ │ └──────────────────────────────────────────────────────┘│ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ Environment Variables (8 detected)                           │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ TRANSPORT_URL *                                          │ │
│ │ ┌──────────────────────────────────────────────────────┐│ │
│ │ │ amqp://prod-rabbitmq.acme.local:5672                ││ │
│ │ └──────────────────────────────────────────────────────┘│ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ PERSISTENCE_CONNECTION *                                 │ │
│ │ ┌──────────────────────────────────────────────────────┐│ │
│ │ │ Host=prod-db;Port=5432;Database=readystack_prod;... ││ │
│ │ └──────────────────────────────────────────────────────┘│ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ EVENTSTORE_URL                                           │ │
│ │ ┌──────────────────────────────────────────────────────┐│ │
│ │ │ esdb://eventstore:2113                               ││ │  ← Default from compose
│ │ └──────────────────────────────────────────────────────┘│ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ POSTGRES_PASSWORD *                                      │ │
│ │ ┌──────────────────────────────────────────────────────┐│ │
│ │ │ ••••••••                                             ││ │  ← Password field
│ │ └──────────────────────────────────────────────────────┘│ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ [Show all variables (4 more)] ▼                              │
│                                                               │
│                          [Cancel]  [Deploy Stack]             │
└─────────────────────────────────────────────────────────────┘
```

**Key Features:**
- ✅ Variables without defaults marked with `*` (required)
- ✅ Variables with defaults pre-filled (can be overridden)
- ✅ Collapsible section for less important variables
- ✅ Automatic password field detection (contains "PASSWORD", "SECRET", "TOKEN")

#### Benefits of Docker Compose Approach (v0.4)

1. ✅ **Standard Format:** Docker Compose is industry-standard, widely known
2. ✅ **Quick Start:** Deploy existing stacks immediately without conversion
3. ✅ **Stack-Specific:** Each stack can have different configuration requirements
4. ✅ **Environment-Specific:** Same stack can have different configs per environment
5. ✅ **No Global State:** No wizard step for connection strings
6. ✅ **Flexibility:** Easy to add new configuration parameters by editing compose file
7. ✅ **Portainer Compatibility:** Familiar workflow for existing Portainer users

#### Limitations of Docker Compose Approach

1. ⚠️ **No Type Validation:** All values are strings, no number/boolean validation
2. ⚠️ **No Regex Validation:** Can't enforce format patterns (e.g., URL format)
3. ⚠️ **Basic Required Check:** Only checks if value is non-empty
4. ⚠️ **No Sensitive Marking:** Must detect password fields by naming convention
5. ⚠️ **Limited Documentation:** No field descriptions or help text

**Solution:** These limitations will be addressed in Phase 2 (Custom Manifest Format in v0.5+).

---

### Phase 2: Custom Manifest Format (v0.5+ - Future Enhancement)

**Status:** 🚧 Planned for v0.5 or later

This phase adds an **optional enhanced format** for stacks that need advanced validation and documentation.

#### Custom Manifest Schema

**File:** `readystack-core.manifest.json`

```json
{
  "version": "1.0.0",
  "name": "ReadyStack Core",
  "description": "Core microservices for ReadyStack platform",
  "format": "manifest",
  "containers": [
    {
      "name": "api-gateway",
      "image": "readystack/api-gateway:1.0.0",
      "ports": ["8080:8080"],
      "environment": [
        "TRANSPORT_URL={{transport.url}}",
        "PERSISTENCE_CONNECTION={{persistence.connection}}",
        "EVENTSTORE_URL={{eventstore.url}}"
      ]
    },
    {
      "name": "order-service",
      "image": "readystack/order-service:1.0.0",
      "ports": ["8081:8080"],
      "environment": [
        "TRANSPORT_URL={{transport.url}}",
        "PERSISTENCE_CONNECTION={{persistence.connection}}",
        "EVENTSTORE_URL={{eventstore.url}}"
      ]
    }
  ],
  "configurationSchema": {
    "transport": {
      "url": {
        "type": "string",
        "displayName": "Message Transport URL",
        "description": "RabbitMQ connection string (AMQP protocol)",
        "default": "amqp://rabbitmq:5672",
        "required": true,
        "validation": "^amqps?://.*",
        "placeholder": "amqp://hostname:5672"
      }
    },
    "persistence": {
      "connection": {
        "type": "string",
        "displayName": "Database Connection String",
        "description": "PostgreSQL connection string",
        "default": "Host=postgres;Port=5432;Database=readystack;Username=admin",
        "required": true,
        "sensitive": true,
        "placeholder": "Host=hostname;Port=5432;Database=dbname;Username=user;Password=pwd"
      }
    },
    "eventstore": {
      "url": {
        "type": "string",
        "displayName": "Event Store URL",
        "description": "EventStoreDB connection string",
        "default": "esdb://eventstore:2113?tls=false",
        "required": true,
        "validation": "^esdb://.*",
        "placeholder": "esdb://hostname:2113"
      }
    }
  }
}
```

#### Enhanced Configuration Schema

**Configuration Value Types (v0.5+):**

```json
{
  "configurationSchema": {
    "example": {
      "stringField": {
        "type": "string",
        "displayName": "Example String",
        "description": "A string field with validation",
        "default": "default-value",
        "required": true,
        "validation": "^[a-z0-9-]+$",
        "placeholder": "enter-value-here"
      },
      "numberField": {
        "type": "number",
        "displayName": "Example Number",
        "description": "A number field with range",
        "default": 8080,
        "required": true,
        "min": 1024,
        "max": 65535
      },
      "booleanField": {
        "type": "boolean",
        "displayName": "Example Boolean",
        "description": "A boolean toggle",
        "default": true
      },
      "selectField": {
        "type": "select",
        "displayName": "Example Dropdown",
        "description": "A dropdown selection",
        "default": "option1",
        "options": ["option1", "option2", "option3"]
      },
      "secretField": {
        "type": "string",
        "displayName": "Example Secret",
        "description": "A sensitive field",
        "required": true,
        "sensitive": true
      }
    }
  }
}
```

#### Benefits of Custom Manifest Format (v0.5+)

1. ✅ **Type Validation:** Numbers, booleans, strings, selects
2. ✅ **Regex Validation:** Enforce URL formats, patterns, etc.
3. ✅ **Range Validation:** Min/max for numbers
4. ✅ **Required Fields:** Explicit required marking
5. ✅ **Sensitive Fields:** Explicit password/secret marking
6. ✅ **Documentation:** Display names, descriptions, placeholders
7. ✅ **Better UX:** More user-friendly configuration UI

#### Coexistence: Both Formats Supported

**v0.5+ will support BOTH formats:**

```
User uploads stack:
├─ docker-compose.yml → Parsed as Docker Compose (Phase 1 logic)
└─ *.manifest.json → Parsed as Custom Manifest (Phase 2 logic)
```

**No migration required:** Existing Docker Compose stacks continue to work. Users can optionally upgrade to custom manifests when they need advanced features.

### Migration from v0.3 Connection Strings

**v0.3 had:**
- `rsgo.contexts.json` with global connection strings (Simple mode)

**v0.4 Migration Strategy:**
1. Read existing `rsgo.contexts.json` on first startup
2. When user deploys first stack in v0.4:
   - Pre-fill deployment configuration UI with values from `rsgo.contexts.json`
   - Map v0.3 context names to v0.4 environment variables:
     - `Transport` → `TRANSPORT_URL`
     - `Persistence` → `PERSISTENCE_CONNECTION`
     - `EventStore` → `EVENTSTORE_URL`
   - User can confirm or modify values
3. Save configuration per stack deployment
4. Archive `rsgo.contexts.json` → `rsgo.contexts.json.v0.3.backup`
5. Remove wizard step for connection configuration

**Example Migration:**

```json
// v0.3: rsgo.contexts.json (OLD)
{
  "transport": "amqp://rabbitmq:5672",
  "persistence": "Host=postgres;Port=5432;Database=readystack",
  "eventStore": "esdb://eventstore:2113?tls=false"
}

// v0.4: Pre-filled deployment UI (NEW)
{
  "TRANSPORT_URL": "amqp://rabbitmq:5672",
  "PERSISTENCE_CONNECTION": "Host=postgres;Port=5432;Database=readystack",
  "EVENTSTORE_URL": "esdb://eventstore:2113?tls=false"
}
```

### API Endpoints

#### Upload and Parse Docker Compose (v0.4)

**Upload Compose File**
```http
POST /api/stacks/upload
Authorization: Bearer {token}
Content-Type: multipart/form-data

File: docker-compose.yml

Response 200 OK:
{
  "stackName": "readystack-core",
  "detectedVariables": [
    {
      "name": "TRANSPORT_URL",
      "displayName": "Transport URL",
      "required": true,
      "defaultValue": null
    },
    {
      "name": "EVENTSTORE_URL",
      "displayName": "Eventstore URL",
      "required": false,
      "defaultValue": "esdb://eventstore:2113"
    }
  ],
  "services": ["api-gateway", "order-service", "postgres", "rabbitmq", "eventstore"]
}
```

#### Deploy Stack with Configuration (v0.4)

**Deploy Docker Compose Stack**
```http
POST /api/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "environmentId": "production",
  "stackName": "readystack-core",
  "composeFile": "docker-compose.yml",
  "configuration": {
    "TRANSPORT_URL": "amqp://prod-rabbitmq:5672",
    "PERSISTENCE_CONNECTION": "Host=prod-db;Port=5432;Database=readystack_prod;...",
    "EVENTSTORE_URL": "esdb://prod-es:2113?tls=true",
    "POSTGRES_PASSWORD": "secure-password-here"
  }
}

Response 201 Created:
{
  "deploymentId": "deployment-12345",
  "environmentId": "production",
  "stackName": "readystack-core",
  "status": "deploying",
  "containers": [
    { "serviceName": "api-gateway", "status": "creating" },
    { "serviceName": "order-service", "status": "creating" },
    { "serviceName": "postgres", "status": "creating" },
    { "serviceName": "rabbitmq", "status": "creating" },
    { "serviceName": "eventstore", "status": "creating" }
  ]
}
```

#### Get Deployment Configuration

```http
GET /api/deployments/{deploymentId}/configuration
Authorization: Bearer {token}

Response 200 OK:
{
  "deploymentId": "deployment-12345",
  "environmentId": "production",
  "stackName": "readystack-core",
  "configuration": {
    "TRANSPORT_URL": "amqp://prod-rabbitmq:5672",
    "PERSISTENCE_CONNECTION": "Host=prod-db;Port=5432;Database=readystack_prod;...",
    "EVENTSTORE_URL": "esdb://prod-es:2113?tls=true",
    "POSTGRES_PASSWORD": "***" // Masked for security
  }
}
```

#### Future: Deploy Custom Manifest (v0.5+)

```http
POST /api/deployments
Authorization: Bearer {token}
Content-Type: application/json

{
  "environmentId": "production",
  "manifestFile": "readystack-core.manifest.json",
  "configuration": {
    "transport": {
      "url": "amqp://prod-rabbitmq:5672"
    },
    "persistence": {
      "connection": "Host=prod-db;..."
    }
  }
}
```

---

## Backend Services

### New: `IEnvironmentService`

```csharp
public interface IEnvironmentService
{
    Task<List<Environment>> GetAllEnvironmentsAsync();
    Task<Environment?> GetEnvironmentAsync(string environmentId);
    Task<Environment> CreateEnvironmentAsync(CreateEnvironmentRequest request);
    Task<Environment> UpdateEnvironmentAsync(string environmentId, UpdateEnvironmentRequest request);
    Task DeleteEnvironmentAsync(string environmentId);
    Task<Environment> GetDefaultEnvironmentAsync();
    Task SetDefaultEnvironmentAsync(string environmentId);
}

public class EnvironmentService : IEnvironmentService
{
    private readonly IConfigStore _configStore;
    private readonly ILogger<EnvironmentService> _logger;

    public async Task<List<Environment>> GetAllEnvironmentsAsync()
    {
        var systemConfig = await _configStore.GetSystemConfigAsync();
        return systemConfig.Environments;
    }

    public async Task<Environment> CreateEnvironmentAsync(CreateEnvironmentRequest request)
    {
        var systemConfig = await _configStore.GetSystemConfigAsync();

        // Validate unique environment ID
        if (systemConfig.Environments.Any(e => e.Id == request.Id))
        {
            throw new InvalidOperationException($"Environment with ID '{request.Id}' already exists.");
        }

        // Validate unique Docker host URL
        if (systemConfig.Environments.Any(e => e.DockerHost == request.DockerHost))
        {
            throw new InvalidOperationException(
                $"Docker host '{request.DockerHost}' is already used by another environment. " +
                "Each environment must have a unique Docker host.");
        }

        var newEnvironment = new Environment
        {
            Id = request.Id,
            Name = request.Name,
            DockerHost = request.DockerHost,
            IsDefault = systemConfig.Environments.Count == 0, // First environment is default
            CreatedAt = DateTime.UtcNow
        };

        systemConfig.Environments.Add(newEnvironment);
        await _configStore.SaveSystemConfigAsync(systemConfig);

        // Create empty contexts config for new environment
        var contextsConfig = new ContextsConfig
        {
            EnvironmentId = newEnvironment.Id,
            ConnectionMode = ConnectionMode.Simple,
            Simple = new SimpleConnectionConfig()
        };
        await _configStore.SaveContextsConfigAsync(newEnvironment.Id, contextsConfig);

        return newEnvironment;
    }

    // ... other methods
}
```

### Modified: `IConfigStore`

```csharp
public interface IConfigStore
{
    // Existing methods
    Task<SystemConfig> GetSystemConfigAsync();
    Task SaveSystemConfigAsync(SystemConfig config);
    Task<SecurityConfig> GetSecurityConfigAsync();
    Task SaveSecurityConfigAsync(SecurityConfig config);
    Task<TlsConfig> GetTlsConfigAsync();
    Task SaveTlsConfigAsync(TlsConfig config);

    // v0.3 - Single contexts file
    Task<ContextsConfig> GetContextsConfigAsync();
    Task SaveContextsConfigAsync(ContextsConfig config);

    // v0.4 - Per-environment contexts files (NEW)
    Task<ContextsConfig> GetContextsConfigAsync(string environmentId);
    Task SaveContextsConfigAsync(string environmentId, ContextsConfig config);
}
```

**Implementation:**
```csharp
public async Task<ContextsConfig> GetContextsConfigAsync(string environmentId)
{
    var fileName = $"rsgo.contexts.{environmentId}.json";
    var filePath = Path.Combine(_configDirectory, fileName);

    if (!File.Exists(filePath))
    {
        _logger.LogWarning("Contexts config not found for environment: {EnvironmentId}", environmentId);
        return new ContextsConfig { EnvironmentId = environmentId };
    }

    var json = await File.ReadAllTextAsync(filePath);
    var config = JsonSerializer.Deserialize<ContextsConfig>(json, _jsonOptions);
    return config ?? new ContextsConfig { EnvironmentId = environmentId };
}

public async Task SaveContextsConfigAsync(string environmentId, ContextsConfig config)
{
    var fileName = $"rsgo.contexts.{environmentId}.json";
    var filePath = Path.Combine(_configDirectory, fileName);

    config.EnvironmentId = environmentId; // Ensure consistency

    var json = JsonSerializer.Serialize(config, _jsonOptions);
    await File.WriteAllTextAsync(filePath, json);

    _logger.LogInformation("Saved contexts config for environment: {EnvironmentId}", environmentId);
}
```

### Modified: `IDockerService`

```csharp
// v0.3
public interface IDockerService
{
    Task<List<ContainerInfo>> ListContainersAsync();
    Task<ContainerInfo> GetContainerAsync(string containerId);
    Task StartContainerAsync(string containerId);
    Task StopContainerAsync(string containerId);
}

// v0.4 - Environment-aware
public interface IDockerService
{
    Task<List<ContainerInfo>> ListContainersAsync(string environmentId);
    Task<ContainerInfo> GetContainerAsync(string environmentId, string containerId);
    Task StartContainerAsync(string environmentId, string containerId);
    Task StopContainerAsync(string environmentId, string containerId);
    Task<bool> TestConnectionAsync(string dockerHostUrl); // NEW - Test Docker host connectivity
}
```

**Implementation:**
```csharp
public class DockerService : IDockerService
{
    private readonly IEnvironmentService _environmentService;
    private readonly ILogger<DockerService> _logger;

    public async Task<List<ContainerInfo>> ListContainersAsync(string environmentId)
    {
        var environment = await _environmentService.GetEnvironmentAsync(environmentId);
        if (environment == null)
        {
            throw new InvalidOperationException($"Environment '{environmentId}' not found.");
        }

        var client = CreateDockerClient(environment.DockerHost);

        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true
        });

        return containers.Select(c => new ContainerInfo
        {
            Id = c.ID,
            Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "unknown",
            Image = c.Image,
            State = c.State,
            Status = c.Status
        }).ToList();
    }

    private DockerClient CreateDockerClient(string dockerHostUrl)
    {
        var config = new DockerClientConfiguration(new Uri(dockerHostUrl));
        return config.CreateClient();
    }

    public async Task<bool> TestConnectionAsync(string dockerHostUrl)
    {
        try
        {
            var client = CreateDockerClient(dockerHostUrl);
            await client.System.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

---

## Frontend Changes

### New Component: `EnvironmentSelector`

**Location:** `src/components/EnvironmentSelector.tsx`

```tsx
import { useState, useEffect } from 'react';
import { useEnvironment } from '../hooks/useEnvironment';

export default function EnvironmentSelector() {
  const { environments, activeEnvironment, setActiveEnvironment } = useEnvironment();
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="relative">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 dark:bg-gray-800 dark:text-gray-200 dark:border-gray-600"
      >
        <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
        </svg>
        <span>{activeEnvironment?.name || 'Select Environment'}</span>
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {isOpen && (
        <div className="absolute right-0 z-10 mt-2 w-64 bg-white border border-gray-200 rounded-lg shadow-lg dark:bg-gray-800 dark:border-gray-700">
          <div className="p-2">
            {environments.map((env) => (
              <button
                key={env.id}
                onClick={() => {
                  setActiveEnvironment(env.id);
                  setIsOpen(false);
                }}
                className={`w-full px-4 py-2 text-left text-sm rounded-md transition-colors ${
                  activeEnvironment?.id === env.id
                    ? 'bg-brand-100 text-brand-700 dark:bg-brand-900 dark:text-brand-300'
                    : 'text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700'
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-medium">{env.name}</span>
                  {env.isDefault && (
                    <span className="text-xs text-gray-500 dark:text-gray-400">Default</span>
                  )}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  {env.dockerHost}
                </div>
              </button>
            ))}
          </div>
          <div className="border-t border-gray-200 dark:border-gray-700 p-2">
            <a
              href="/settings/environments"
              className="block px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-md dark:text-gray-300 dark:hover:bg-gray-700"
            >
              Manage Environments
            </a>
          </div>
        </div>
      )}
    </div>
  );
}
```

### New Hook: `useEnvironment`

**Location:** `src/hooks/useEnvironment.ts`

```typescript
import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { getEnvironments } from '../services/api';

interface Environment {
  id: string;
  name: string;
  dockerHost: string;
  isDefault: boolean;
  createdAt: string;
}

interface EnvironmentContextType {
  environments: Environment[];
  activeEnvironment: Environment | null;
  setActiveEnvironment: (environmentId: string) => void;
  refreshEnvironments: () => Promise<void>;
}

const EnvironmentContext = createContext<EnvironmentContextType | undefined>(undefined);

export function EnvironmentProvider({ children }: { children: ReactNode }) {
  const [environments, setEnvironments] = useState<Environment[]>([]);
  const [activeEnvironmentId, setActiveEnvironmentId] = useState<string | null>(
    localStorage.getItem('activeEnvironmentId')
  );

  const activeEnvironment = environments.find(e => e.id === activeEnvironmentId) || null;

  const refreshEnvironments = async () => {
    try {
      const data = await getEnvironments();
      setEnvironments(data.environments);

      // Set default environment if none active
      if (!activeEnvironmentId && data.environments.length > 0) {
        const defaultEnv = data.environments.find(e => e.isDefault) || data.environments[0];
        setActiveEnvironmentId(defaultEnv.id);
        localStorage.setItem('activeEnvironmentId', defaultEnv.id);
      }
    } catch (error) {
      console.error('Failed to load environments:', error);
    }
  };

  const setActiveEnvironment = (environmentId: string) => {
    setActiveEnvironmentId(environmentId);
    localStorage.setItem('activeEnvironmentId', environmentId);
  };

  useEffect(() => {
    refreshEnvironments();
  }, []);

  return (
    <EnvironmentContext.Provider
      value={{ environments, activeEnvironment, setActiveEnvironment, refreshEnvironments }}
    >
      {children}
    </EnvironmentContext.Provider>
  );
}

export function useEnvironment() {
  const context = useContext(EnvironmentContext);
  if (!context) {
    throw new Error('useEnvironment must be used within EnvironmentProvider');
  }
  return context;
}
```

### Modified: `DashboardLayout`

```tsx
import EnvironmentSelector from '../components/EnvironmentSelector';

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <div>
      <header className="flex items-center justify-between p-4 border-b">
        <h1>ReadyStackGo Admin</h1>
        <div className="flex items-center gap-4">
          <EnvironmentSelector /> {/* NEW */}
          <UserMenu />
        </div>
      </header>
      <main>{children}</main>
    </div>
  );
}
```

### Modified: `ContainersPage`

```tsx
import { useEnvironment } from '../hooks/useEnvironment';

export default function ContainersPage() {
  const { activeEnvironment } = useEnvironment();
  const [containers, setContainers] = useState([]);

  useEffect(() => {
    if (activeEnvironment) {
      loadContainers(activeEnvironment.id);
    }
  }, [activeEnvironment]);

  const loadContainers = async (environmentId: string) => {
    const data = await getContainers(environmentId);
    setContainers(data);
  };

  if (!activeEnvironment) {
    return <div>No environment selected</div>;
  }

  return (
    <div>
      <h2>Containers - {activeEnvironment.name}</h2>
      {/* Container list */}
    </div>
  );
}
```

---

## Migration Strategy (v0.3 → v0.4)

### Automatic Migration on Startup

**Detection Logic:**
```csharp
public async Task<bool> IsMigrationNeededAsync()
{
    var systemConfig = await _configStore.GetSystemConfigAsync();

    // Check if v0.3 format (no environments array)
    return systemConfig.Environments == null || systemConfig.Environments.Count == 0;
}

public async Task MigrateFromV03Async()
{
    _logger.LogInformation("Starting migration from v0.3 to v0.4");

    // 1. Load existing v0.3 system config
    var systemConfig = await _configStore.GetSystemConfigAsync();

    // 2. Create default environment
    var defaultEnvironment = new Environment
    {
        Id = "production",
        Name = "Production",
        DockerHost = "unix:///var/run/docker.sock", // Default to local Docker
        IsDefault = true,
        CreatedAt = DateTime.UtcNow
    };

    systemConfig.Environments = new List<Environment> { defaultEnvironment };
    systemConfig.InstalledVersion = "v0.4.0";
    systemConfig.UpdatedAt = DateTime.UtcNow;

    await _configStore.SaveSystemConfigAsync(systemConfig);

    // 3. Migrate rsgo.contexts.json → rsgo.contexts.production.json
    var oldContextsPath = Path.Combine(_configDirectory, "rsgo.contexts.json");
    var newContextsPath = Path.Combine(_configDirectory, "rsgo.contexts.production.json");

    if (File.Exists(oldContextsPath))
    {
        var contextsJson = await File.ReadAllTextAsync(oldContextsPath);
        var contextsConfig = JsonSerializer.Deserialize<ContextsConfig>(contextsJson);

        if (contextsConfig != null)
        {
            contextsConfig.EnvironmentId = "production";
            await _configStore.SaveContextsConfigAsync("production", contextsConfig);

            // Backup old file
            File.Move(oldContextsPath, oldContextsPath + ".v0.3.backup");
        }
    }

    _logger.LogInformation("Migration to v0.4 completed successfully");
}
```

**Startup Hook in `Program.cs`:**
```csharp
// After TLS bootstrap, before HTTP pipeline
await MigrateConfigurationAsync(app);

private static async Task MigrateConfigurationAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (await migrationService.IsMigrationNeededAsync())
        {
            logger.LogInformation("Detected v0.3 configuration. Starting migration to v0.4...");
            await migrationService.MigrateFromV03Async();
            logger.LogInformation("Configuration migration completed successfully.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Configuration migration failed. Manual intervention may be required.");
    }
}
```

---

## Testing Strategy

### Unit Tests
- `EnvironmentService` CRUD operations
- `ConfigStore` environment-specific file operations
- Migration logic from v0.3 to v0.4
- Environment validation (unique IDs, default environment constraints)

### Integration Tests
- `GET /api/environments` returns all environments
- `POST /api/environments` creates new environment
- `DELETE /api/environments/{id}` fails for default environment
- `GET /api/containers?environment=test` returns only test environment containers
- Migration from v0.3 config format to v0.4

### E2E Tests (Playwright)
- Environment selector dropdown interaction
- Switch between environments updates dashboard
- Create new environment via Settings page
- Deploy stack to specific environment
- Migration flow: upgrade from v0.3, verify default environment created

---

## Out of Scope for v0.4

The following features are explicitly **NOT included** in v0.4 and deferred to future releases:

### Multi-Node Support (Deferred to v2.0+)
- Multiple Docker hosts per environment
- Load balancing across nodes
- Node health monitoring
- Distributed container orchestration

### Advanced Features (Future)
- Environment templates
- Environment cloning
- Cross-environment promotion workflows
- Environment-specific RBAC
- Audit logs per environment

---

## Open Questions

1. **Docker Host Security:** Should we support TLS-secured Docker hosts in v0.4, or only plain TCP/Unix sockets?
   - **Recommendation:** Support basic auth (TCP/Unix) in v0.4, TLS in v0.5

2. **Environment Deletion:** What happens to deployed containers when environment is deleted?
   - **Recommendation:** Prevent deletion if containers exist; require manual cleanup first

3. **Environment Limits:** Should there be a max number of environments per organization?
   - **Recommendation:** No hard limit in v0.4; add if performance issues arise

4. **Wizard Defaults:** Should wizard auto-detect local Docker daemon, or require user input?
   - **Recommendation:** Auto-detect `unix:///var/run/docker.sock` if available, otherwise prompt

---

## Success Criteria

v0.4 is considered complete when:

- ✅ Users can create multiple environments via UI
- ✅ Users can switch between environments using dropdown selector
- ✅ Dashboard/containers/stacks are filtered by active environment
- ✅ Each environment has independent connection strings configuration
- ✅ Each environment connects to a different Docker host
- ✅ Stack deployments are scoped to active environment
- ✅ v0.3 installations automatically migrate to v0.4 with default environment
- ✅ All unit/integration/E2E tests pass
- ✅ Documentation updated (README, CHANGELOG, release notes)

---

## Timeline Estimate

| Phase | Duration | Tasks |
|-------|----------|-------|
| **Design** | 1 week | Finalize API contracts, UI mockups, database schema |
| **Backend** | 2 weeks | Implement EnvironmentService, ConfigStore changes, migration logic |
| **Frontend** | 1 week | Build EnvironmentSelector, Settings page, hook integration |
| **Testing** | 1 week | Unit, integration, E2E tests |
| **Documentation** | 3 days | Update all docs, write migration guide |
| **QA & Bug Fixes** | 1 week | User acceptance testing, polish |

**Total:** ~6 weeks (1.5 months)

---

## References

- [v0.3.0 Release Notes](../Release-Notes/v0.3.0.md)
- [CHANGELOG.md](../../CHANGELOG.md)
- [Docker Engine API](https://docs.docker.com/engine/api/)
- [Clean Architecture Principles](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
