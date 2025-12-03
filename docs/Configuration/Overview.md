# Configuration Overview

ReadyStackGo uses a structured configuration system based on JSON files, stored in the persistent volume `rsgo-config`.

## Configuration Principles

### 1. Declarative
All configuration is declaratively described in JSON files.

### 2. Versioned
Configuration files are versioned together with the stack.

### 3. Validated
Each configuration is validated against a schema.

### 4. Persistent
All configurations are persisted in the Docker volume `rsgo-config`.

## Configuration Files

ReadyStackGo uses several specialized configuration files, all with the prefix `rsgo.`:

### [`rsgo.system.json`](Config-Files.md#rsgo-system-json)
System and organization configuration
- Organization (ID, Name)
- Network configuration
- Ports (HTTP, HTTPS)
- Base URL
- Wizard status
- Deployment mode

### [`rsgo.security.json`](Config-Files.md#rsgo-security-json)
Security and authentication configuration
- Local admin users
- Password hashes
- JWT configuration
- OIDC providers (future)
- Roles and permissions

### [`rsgo.tls.json`](Config-Files.md#rsgo-tls-json)
TLS configuration
- TLS mode (SelfSigned/Custom)
- Certificate path
- Ports
- HTTP redirect settings

### [`rsgo.contexts.json`](Config-Files.md#rsgo-contexts-json)
Context and connection configuration
- Connection mode (Simple/Advanced)
- Global connection strings
- Context-specific connections
- Context metadata

### [`rsgo.features.json`](Config-Files.md#rsgo-features-json)
Feature flags
- Global feature switches
- True/False values
- Passed as `RSGO_FEATURE_*` environment variables

### [`rsgo.release.json`](Config-Files.md#rsgo-release-json)
Release status
- Installed stack version
- Context versions
- Installation date
- Deployment history

### [`rsgo.nodes.json`](Config-Files.md#rsgo-nodes-json) (Future)
Multi-node configuration
- Node definitions
- Node roles
- Remote Docker hosts

## Configuration Modes

### Simple Mode (Default)
All contexts use the same global connections:
```json
{
  "mode": "Simple",
  "globalConnections": {
    "transport": "amqp://rabbitmq:5672",
    "persistence": "Server=sql;Database=ams",
    "eventStore": "esdb://eventstore:2113"
  }
}
```

### Advanced Mode
Each context can have individual connections:
```json
{
  "mode": "Advanced",
  "contexts": {
    "project": {
      "connections": {
        "transport": "amqp://rabbitmq-project:5672",
        "persistence": "Server=sql-project;Database=project"
      }
    }
  }
}
```

## Configuration Location

All configuration files are located in the Docker volume:
```
/app/config/
├── rsgo.system.json
├── rsgo.security.json
├── rsgo.tls.json
├── rsgo.contexts.json
├── rsgo.features.json
├── rsgo.release.json
└── tls/
    ├── certificate.pfx
    └── self-signed.pfx
```

## Runtime Configuration

### Reading
```csharp
var systemConfig = await configStore.LoadSystemConfigAsync();
```

### Writing
```csharp
await configStore.SaveSystemConfigAsync(systemConfig);
```

### Validation
All configurations are automatically validated:
- JSON schema validation
- Business rule validation
- Dependency validation

## Environment Variables

Environment variables are automatically generated from configuration files for containers:

### System Variables
```bash
RSGO_ORG_ID=customer-a
RSGO_ORG_NAME=Customer A Inc.
RSGO_STACK_VERSION=4.3.0
```

### Feature Variables
```bash
RSGO_FEATURE_newColorTheme=true
RSGO_FEATURE_discussionV2=false
```

### Connection Variables
```bash
RSGO_CONNECTION_transport=amqp://rabbitmq:5672
RSGO_CONNECTION_persistence=Server=sql;Database=ams
RSGO_CONNECTION_eventStore=esdb://eventstore:2113
```

## Best Practices

1. **Do not edit manually**: Use the Admin UI or API
2. **Create backups**: Regularly backup the `rsgo-config` volume
3. **Prefer Simple Mode**: Only switch to Advanced when needed
4. **External secrets**: Store passwords in secrets management systems
5. **Versioning**: Document configuration changes

## Next Steps

- [Config Files in Detail](Config-Files.md)
- [Manifest Specification](Manifest-Specification.md)
- [Feature Flags](Feature-Flags.md)
