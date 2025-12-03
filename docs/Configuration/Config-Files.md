# Config Files (`rsgo.*.json`)

This document describes all central config files managed by the admin container.

## Overview v0.6

With v0.6, the configuration was fundamentally revised:

| Storage | Data Type | Description |
|---------|-----------|-------------|
| **SQLite** | Dynamic data | Organizations, Users, Environments, Deployments |
| **JSON** | Static configuration | System settings, TLS, Features, Release info |

### Current JSON Files

| File | Description |
|------|-------------|
| `rsgo.system.json` | Wizard status, BaseUrl, Ports, Network |
| `rsgo.tls.json` | TLS certificates and mode |
| `rsgo.features.json` | Feature Flags |
| `rsgo.release.json` | Installed stack version |

### Removed Files (since v0.6)

| File | Replaced by |
|------|-------------|
| ~~`rsgo.security.json`~~ | SQLite: Users table |
| ~~`rsgo.organization.json`~~ | SQLite: Organizations table |
| ~~`rsgo.contexts.json`~~ | Removed (obsolete since v0.4) |
| ~~`rsgo.connections.json`~~ | Removed (obsolete since v0.4) |

---

## rsgo.system.json

Stores system settings and wizard status.

```json
{
  "baseUrl": "https://localhost:8443",
  "httpPort": 8080,
  "httpsPort": 8443,
  "networkName": "rsgo-net",
  "wizardState": "Completed",
  "deploymentMode": "SingleNode"
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `baseUrl` | string | Base URL for the admin UI |
| `httpPort` | int | HTTP port (default: 8080) |
| `httpsPort` | int | HTTPS port (default: 8443) |
| `networkName` | string | Docker network for containers |
| `wizardState` | enum | NotStarted, AdminCreated, OrganizationSet, EnvironmentCreated, Completed |
| `deploymentMode` | enum | SingleNode, MultiNode |

---

## rsgo.tls.json

Defines TLS mode and certificate paths.

```json
{
  "mode": "SelfSigned",
  "certificatePath": "/app/certs/rsgo.pfx",
  "certificatePassword": "***",
  "customCertificateThumbprint": null
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `mode` | enum | SelfSigned, Custom, None |
| `certificatePath` | string | Path to PFX certificate |
| `certificatePassword` | string | Password for PFX (encrypted) |
| `customCertificateThumbprint` | string? | Thumbprint of a custom certificate |

---

## rsgo.features.json

Global feature flags passed as environment variables to containers.

```json
{
  "features": {
    "AUDIT_LOGGING": true,
    "ADVANCED_SEARCH": false,
    "BETA_FEATURES": false
  }
}
```

### Usage

Feature flags are passed as `RSGO_FEATURE_<NAME>` environment variables to deployed containers:

```bash
RSGO_FEATURE_AUDIT_LOGGING=true
RSGO_FEATURE_ADVANCED_SEARCH=false
```

---

## rsgo.release.json

Contains information about the installed stack version.

```json
{
  "installedStackVersion": "1.0.0",
  "installDate": "2024-01-15T10:30:00Z",
  "installedContexts": {
    "api": "1.0.0",
    "web": "1.0.0",
    "db": "15.0"
  }
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `installedStackVersion` | string? | Installed stack version |
| `installDate` | DateTime? | Installation timestamp |
| `installedContexts` | Dictionary | Service name → Version |

---

## Storage Location

All JSON files are located in the config volume:

```
/app/config/
├── rsgo.system.json
├── rsgo.tls.json
├── rsgo.features.json
└── rsgo.release.json
```

In the Docker container, this directory is typically mounted as a volume:

```yaml
volumes:
  - rsgo-config:/app/config
```

---

## SQLite Database (v0.6)

Dynamic data is stored in a SQLite database:

```
/app/data/readystackgo.db
```

### Tables

| Table | Description |
|-------|-------------|
| `Organizations` | Organizations with name, description, status |
| `Users` | Users with username, email, password hash, enablement |
| `UserRoleAssignments` | Role assignments with scope |
| `Environments` | Docker environments (Socket/API) |
| `Deployments` | Deployment history |
| `DeployedServices` | Services per deployment |

---

## ConfigStore Interface

Access to JSON configurations is through `IConfigStore`:

```csharp
public interface IConfigStore
{
    // System Config
    Task<SystemConfig> GetSystemConfigAsync();
    Task SaveSystemConfigAsync(SystemConfig config);

    // TLS Config
    Task<TlsConfig> GetTlsConfigAsync();
    Task SaveTlsConfigAsync(TlsConfig config);

    // Features Config
    Task<FeaturesConfig> GetFeaturesConfigAsync();
    Task SaveFeaturesConfigAsync(FeaturesConfig config);

    // Release Config
    Task<ReleaseConfig> GetReleaseConfigAsync();
    Task SaveReleaseConfigAsync(ReleaseConfig config);
}
```

---

## Migration from Older Versions

### From v0.5 to v0.6

1. `rsgo.security.json` → SQLite Users table
2. `rsgo.organization.json` → SQLite Organizations table
3. `rsgo.contexts.json` → Removed (was already obsolete)

Migration happens automatically on first start of v0.6.
