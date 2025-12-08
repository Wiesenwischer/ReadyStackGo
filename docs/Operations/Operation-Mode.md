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

## Best Practices

1. **Plan maintenance windows**: Communicate with users before entering maintenance
2. **Use labels wisely**: Mark critical infrastructure containers with `rsgo.maintenance: ignore`
3. **Monitor migrations**: Watch migration progress in real-time
4. **Document failures**: Record failure reasons for post-mortem analysis
5. **Test recovery**: Verify your recovery procedures before production issues

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
