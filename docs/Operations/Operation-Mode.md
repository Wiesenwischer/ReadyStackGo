# Operation Mode

ReadyStackGo uses Operation Modes to manage the lifecycle state of deployments. This allows you to put stacks into maintenance, track migrations, and handle failures gracefully.

## Overview

Operation Mode is distinct from health status:

- **Health Status** (Healthy/Degraded/Unhealthy) = Technical state reported by containers
- **Operation Mode** (Normal/Maintenance/Migrating/etc.) = Operational state set by RSGO

RSGO is the source of truth for operation mode, while containers report their technical state.

## Available Modes

| Mode | Icon | Description |
|------|------|-------------|
| **Normal** | 🟢 | Standard operation, all services should be running |
| **Maintenance** | 🔧 | Planned maintenance, services may be stopped |
| **Migrating** | 🔄 | Upgrade/migration in progress |
| **Failed** | 🔴 | Migration or operation failed, requires intervention |
| **Stopped** | ⏹️ | Stack intentionally stopped |

## Maintenance Mode

Maintenance mode allows you to temporarily stop a stack for updates, backups, or configuration changes.

### Entering Maintenance Mode

When entering maintenance mode:

1. Operation mode changes to `Maintenance`
2. All stack containers are **automatically stopped**
3. Health status reflects the expected down state
4. UI shows maintenance indicator

### Container Lifecycle

```
Normal → Maintenance
   │
   └── All containers stopped (via Docker API)

Maintenance → Normal
   │
   └── All containers started (via Docker API)
```

### Excluding Containers

Some containers (like databases) should continue running during maintenance. Use the `rsgo.maintenance` label:

```yaml
services:
  postgres:
    image: postgres:16
    labels:
      rsgo.stack: my-app
      rsgo.maintenance: ignore    # ← Won't be stopped during maintenance

  api:
    image: myapp/api:latest
    labels:
      rsgo.stack: my-app
      # No rsgo.maintenance label = will be stopped
```

Containers with `rsgo.maintenance: ignore` are:
- **Not stopped** when entering maintenance mode
- **Not started** when exiting maintenance mode (already running)

### Use Cases

- **Database migrations**: Stop app containers, run migrations, restart
- **Backup windows**: Stop services during backup
- **Configuration updates**: Safe config changes without traffic

## Migrating Mode

Migration mode tracks upgrades and version transitions.

### Starting a Migration

When deploying a new version:

1. Operation mode changes to `Migrating`
2. `TargetVersion` is set (e.g., "2.0.0")
3. Current version is preserved
4. Health shows "Migrating" status

### Migration Flow

```
Normal (v1.0)
    │
    ▼
Migrating (target: v2.0)
    │
    ├── Success ──► Normal (v2.0)
    │
    └── Failure ──► Failed (rollback possible)
```

### Completing Migration

On successful migration:
- Operation mode returns to `Normal`
- Stack version updated to target version
- Health monitoring resumes normal checks

## Failed Mode

Failed mode indicates a problem requiring manual intervention.

### Causes

- Migration failed during execution
- Deployment error
- Manual override

### Recovery

To recover from failed state:

1. Investigate the failure cause (logs, health history)
2. Fix the underlying issue
3. Use "Recover" action in UI
4. Or re-deploy with corrected configuration

## Stopped Mode

Stopped mode indicates an intentionally stopped stack.

### Characteristics

- Different from failure (planned state)
- All containers should be stopped
- No automatic restart attempts

## API

### Change Operation Mode

```http
PUT /api/deployments/{deploymentId}/operation-mode
Content-Type: application/json

{
  "newMode": "Maintenance",
  "reason": "Scheduled maintenance window"
}
```

### Mode Transitions

Not all transitions are allowed:

| From | To | Allowed |
|------|-----|---------|
| Normal | Maintenance | ✅ |
| Normal | Migrating | ✅ (via deploy) |
| Maintenance | Normal | ✅ |
| Migrating | Normal | ✅ (on success) |
| Migrating | Failed | ✅ (on failure) |
| Failed | Normal | ✅ (via recover) |
| Stopped | Normal | ✅ (via start) |

## UI Integration

### Deployment List

Operation mode is shown alongside health:

```
┌────────────────────────────────────────────┐
│ Stack          │ Health  │ Mode           │
├────────────────────────────────────────────┤
│ Production     │ 🟢      │ Normal         │
│ Staging        │ 🟡      │ 🔧 Maintenance │
│ Development    │ ⚪      │ 🔄 Migrating   │
└────────────────────────────────────────────┘
```

### Deployment Detail

The detail view shows:

- Current operation mode with visual indicator
- Mode history (when mode changed, by whom)
- Available actions based on current mode

### Actions

Available actions depend on current mode:

| Mode | Available Actions |
|------|-------------------|
| Normal | Enter Maintenance, Stop |
| Maintenance | Exit Maintenance |
| Migrating | (Wait for completion) |
| Failed | Recover, Redeploy |
| Stopped | Start |

## SignalR Notifications

Operation mode changes trigger immediate SignalR notifications:

- UI updates without refresh
- All connected clients see the change
- Health widget reflects new state

## Maintenance Observers

Maintenance Observers allow external systems to trigger maintenance mode automatically. Instead of manually switching modes, RSGO monitors external state and synchronizes the operation mode accordingly.

### Observer Pattern

```
External System          RSGO Observer           Stack
     │                        │                    │
     │  State changes         │                    │
     │  (DB property=1)       │                    │
     │ ─────────────────────► │                    │
     │                        │  Enter Maintenance │
     │                        │ ──────────────────►│
     │                        │                    │ Containers stopped
     │                        │                    │
     │  State restored        │                    │
     │  (DB property=0)       │                    │
     │ ─────────────────────► │                    │
     │                        │  Exit Maintenance  │
     │                        │ ──────────────────►│
     │                        │                    │ Containers started
```

### Observer Types

#### SQL Extended Property Observer

Monitors a SQL Server Extended Property and triggers maintenance mode based on its value.

**Use Case**: Legacy applications or backend systems set a database property during their own maintenance. RSGO automatically puts the dependent stack into maintenance mode.

**Manifest Configuration:**

```yaml
metadata:
  name: Legacy Integration Stack
  productVersion: "1.0.0"

variables:
  DB_CONNECTION:
    type: SqlServerConnectionString
    label: Database Connection
    required: true

maintenanceObserver:
  type: sqlExtendedProperty
  connectionString: ${DB_CONNECTION}      # Direct variable reference
  # OR: connectionName: DB_CONNECTION     # Reference by variable name
  propertyName: app.MaintenanceMode
  maintenanceValue: "1"      # When property = 1 → enter maintenance
  normalValue: "0"           # When property = 0 → exit maintenance
  pollingInterval: 30s       # Check every 30 seconds

services:
  api:
    image: myapp/api:latest
    # Will be stopped automatically when app.MaintenanceMode = 1
```

**Connection Options:**

| Property | Description |
|----------|-------------|
| `connectionString` | Direct connection string or variable substitution (`${VAR}`) |
| `connectionName` | Name of a defined variable (e.g., `DB_CONNECTION`, `LEGACY_DB`) |

Using `connectionName` is recommended when you already have a connection string variable defined in your manifest - it avoids duplication and ensures consistency.

**Extended Property Example:**

```sql
-- Set maintenance mode ON
EXEC sp_addextendedproperty
  @name = N'app.MaintenanceMode',
  @value = N'1';

-- Set maintenance mode OFF
EXEC sp_updateextendedproperty
  @name = N'app.MaintenanceMode',
  @value = N'0';

-- Query current value
SELECT value
FROM sys.extended_properties
WHERE name = 'app.MaintenanceMode';
```

#### SQL Query Observer

Executes a custom SQL query and triggers maintenance based on the result.

**Use Case**: More complex conditions, multiple tables, or non-Extended Property sources.

**Manifest Configuration:**

```yaml
maintenanceObserver:
  type: sqlQuery
  connectionName: PERSISTENCE_DB           # Reference existing variable
  # OR: connectionString: ${PERSISTENCE_DB}
  query: |
    SELECT CASE
      WHEN EXISTS (SELECT 1 FROM SystemStatus WHERE Status = 'Maintenance')
      THEN 'maintenance'
      ELSE 'normal'
    END AS Mode
  maintenanceValue: "maintenance"
  normalValue: "normal"
  pollingInterval: 60s
```

#### HTTP Endpoint Observer

Monitors an HTTP endpoint and triggers maintenance based on the response.

**Use Case**: External maintenance APIs, status pages, or service health endpoints.

**Manifest Configuration:**

```yaml
maintenanceObserver:
  type: http
  url: https://status.example.com/api/maintenance
  method: GET
  headers:
    Authorization: Bearer ${STATUS_TOKEN}
  jsonPath: "$.maintenanceMode"     # Extract value from JSON response
  maintenanceValue: "true"
  normalValue: "false"
  pollingInterval: 30s
  timeout: 10s
```

#### File Observer

Monitors a file for existence or content changes.

**Use Case**: Legacy systems that create marker files during maintenance.

**Manifest Configuration:**

```yaml
maintenanceObserver:
  type: file
  path: /var/maintenance/maintenance.flag
  mode: exists                    # 'exists' or 'content'
  # mode: exists → file exists = maintenance, file absent = normal
  # mode: content → compare file content with maintenanceValue/normalValue
  pollingInterval: 10s
```

### Observer Configuration Reference

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `type` | string | **Yes** | Observer type: `sqlExtendedProperty`, `sqlQuery`, `http`, `file` |
| `pollingInterval` | string | No | How often to check (default: `30s`) |
| `timeout` | string | No | Timeout for each check (default: `10s`) |
| `maintenanceValue` | string | **Yes** | Value that triggers maintenance mode |
| `normalValue` | string | **Yes** | Value that exits maintenance mode |
| `enabled` | boolean | No | Enable/disable observer (default: `true`) |

**Type-specific properties:**

| Type | Property | Description |
|------|----------|-------------|
| `sqlExtendedProperty` | `connectionString` | SQL Server connection string (supports `${VAR}` syntax) |
| `sqlExtendedProperty` | `connectionName` | Name of a defined variable (alternative to connectionString) |
| `sqlExtendedProperty` | `propertyName` | Name of the Extended Property |
| `sqlQuery` | `connectionString` | SQL Server connection string (supports `${VAR}` syntax) |
| `sqlQuery` | `connectionName` | Name of a defined variable (alternative to connectionString) |
| `sqlQuery` | `query` | SQL query returning single value |
| `http` | `url` | HTTP endpoint URL |
| `http` | `method` | HTTP method (GET, POST) |
| `http` | `headers` | Request headers |
| `http` | `jsonPath` | JSONPath to extract value from response |
| `file` | `path` | File path to monitor |
| `file` | `mode` | `exists` or `content` |

> **Note:** For SQL observers, use either `connectionString` OR `connectionName`, not both. `connectionName` is recommended when you already have a connection string variable defined in your manifest.

### Observer Behavior

#### State Transitions

```
Observer detects maintenanceValue
    │
    ▼
┌─────────────────────────────────────────┐
│ RSGO logs: "Observer triggered:         │
│ External maintenance detected"          │
└─────────────────────────────────────────┘
    │
    ▼
Enter Maintenance Mode (automatic)
    │
    ▼
Containers stopped (respecting rsgo.maintenance labels)
```

#### Conflict Resolution

If a user manually changes the mode while an observer is active:

| Scenario | Behavior |
|----------|----------|
| Observer says "maintenance", user sets "normal" | Observer wins on next poll |
| Observer says "normal", user sets "maintenance" | User wins (observer doesn't override manual) |
| Observer disabled | Manual control only |

To override the observer temporarily, disable it via API:

```http
PUT /api/deployments/{id}/maintenance-observer
{ "enabled": false }
```

#### Failure Handling

| Failure | Behavior |
|---------|----------|
| Connection timeout | Log warning, retry on next poll |
| Query error | Log error, retain current state |
| Invalid response | Log error, retain current state |
| 3 consecutive failures | Log critical, send notification |

### Dashboard Integration

When an observer is configured, the UI shows:

```
┌─────────────────────────────────────────────────────┐
│ Operation Mode: 🔧 Maintenance                      │
│ ─────────────────────────────────────────────────── │
│ Triggered by: SQL Extended Property Observer        │
│ Property: ams.MaintenanceMode = 1                   │
│ Last checked: 15 seconds ago                        │
│ ─────────────────────────────────────────────────── │
│ [Disable Observer] [View History]                   │
└─────────────────────────────────────────────────────┘
```

### Example: Legacy System Integration

Complete manifest for integrating with a legacy system's maintenance mode:

```yaml
metadata:
  name: Legacy Portal Integration
  description: Stack synced with backend maintenance mode
  productVersion: "2.0.0"
  category: Enterprise

variables:
  BACKEND_DB:
    label: Backend Database Connection
    type: SqlServerConnectionString
    required: true
    description: Connection to the backend database

  API_PORT:
    label: API Port
    type: Port
    default: "5000"

maintenanceObserver:
  type: sqlExtendedProperty
  connectionName: BACKEND_DB              # Reference the variable by name
  propertyName: app.MaintenanceMode
  maintenanceValue: "1"
  normalValue: "0"
  pollingInterval: 30s

services:
  portal-api:
    image: mycompany/portal-api:latest
    ports:
      - "${API_PORT}:5000"
    environment:
      ConnectionStrings__Backend: ${BACKEND_DB}
    # Stopped when app.MaintenanceMode = 1

  portal-worker:
    image: mycompany/portal-worker:latest
    environment:
      ConnectionStrings__Backend: ${BACKEND_DB}
    # Stopped when app.MaintenanceMode = 1

  redis:
    image: redis:7
    labels:
      rsgo.maintenance: ignore    # Keeps running during maintenance
```

**Workflow:**

1. Admin starts maintenance in the backend system
2. Backend sets `app.MaintenanceMode = 1` on database
3. RSGO Observer detects the change within 30 seconds
4. RSGO enters maintenance mode, stops `portal-api` and `portal-worker`
5. Redis keeps running (has `rsgo.maintenance: ignore`)
6. Admin completes maintenance, sets `app.MaintenanceMode = 0`
7. RSGO Observer detects the change
8. RSGO exits maintenance mode, starts containers
9. Stack is fully operational again

## Best Practices

1. **Plan maintenance windows**: Communicate with users before entering maintenance
2. **Use labels wisely**: Mark critical infrastructure containers with `rsgo.maintenance: ignore`
3. **Monitor migrations**: Watch migration progress in real-time
4. **Document failures**: Record failure reasons for post-mortem analysis
5. **Test recovery**: Verify your recovery procedures before production issues
6. **Configure observers for legacy systems**: Let external systems control maintenance automatically
7. **Set appropriate polling intervals**: Balance responsiveness vs. resource usage

## Example Workflow

### Scheduled Maintenance

```bash
# 1. Notify users
# 2. Enter maintenance mode
PUT /api/deployments/{id}/operation-mode
{ "newMode": "Maintenance", "reason": "Weekly backup" }

# 3. Perform maintenance tasks
# (Databases still running if labeled with rsgo.maintenance: ignore)

# 4. Exit maintenance mode
PUT /api/deployments/{id}/operation-mode
{ "newMode": "Normal" }

# 5. Verify health
GET /api/deployments/{id}/health
```

## See Also

- [Health Monitoring](./Health-Monitoring.md) - Understanding health status
- [Deployment Engine](../Architecture/Deployment-Engine.md) - How deployments work
- [Container Lifecycle](../Architecture/Container-Lifecycle.md) - Container management
