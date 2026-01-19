# Health Monitoring

ReadyStackGo provides comprehensive health monitoring for all deployed stacks, giving you real-time visibility into the status of your containers, services, and infrastructure.

## Overview

The Health Monitoring system provides:

- **Real-time status updates** via SignalR
- **Container health tracking** (running, stopped, restarting)
- **Service health checks** via HTTP endpoints
- **Aggregated health views** at environment and organization levels
- **Health history** for trend analysis

## Health Status Levels

Each stack reports one of four health statuses:

| Status | Icon | Description |
|--------|------|-------------|
| **Healthy** | 🟢 | All services running normally |
| **Degraded** | 🟡 | Some services experiencing issues |
| **Unhealthy** | 🔴 | Critical services down or failing |
| **Unknown** | ⚪ | Unable to determine status |

## Dashboard Widget

The main dashboard shows a compact summary of all deployments with their current health:

```
┌─────────────────────────────────────────┐
│ Health Overview                         │
├─────────────────────────────────────────┤
│ 🟢 Production   │ 3/3 services healthy  │
│ 🟡 Staging      │ 2/3 services healthy  │
│ 🔴 Development  │ 1/3 services healthy  │
└─────────────────────────────────────────┘
```

## Health Dashboard Page

For a more comprehensive view, navigate to `/health` to access the dedicated Health Dashboard. This full-screen view provides:

### Summary Cards
At the top, four cards show the count of stacks by status:
- **Healthy** - All services running normally
- **Degraded** - Some services experiencing issues
- **Unhealthy** - Critical services down or failing
- **Total** - Total number of monitored stacks

### Filters
- **Status Filter** - Show only stacks with a specific status (All/Healthy/Degraded/Unhealthy)
- **Search** - Find stacks by name or version

### Expandable Stack Cards
Each stack is displayed as a card showing:
- Stack name and version
- Last health check timestamp
- Service count (healthy/total)
- Operation mode badge (if not Normal)
- Overall health status badge

Click on a card to expand it and see:
- Individual service status with container details
- Restart counts per service
- Link to the full deployment detail page

### Real-time Updates
The Health Dashboard receives live updates via SignalR:
- Status changes appear immediately without refresh
- A "Live" indicator shows the connection status
- Automatic reconnection if connection is lost

## Deployment Detail View

The deployment detail page (`/deployments/{stackName}`) shows:

- Overall stack health status badge
- Operation mode (Normal, Maintenance, Migrating, etc.)
- Health summary card with service counts
- **Health History Chart** - Visual timeline of health status over time
- Individual service status with container details
- Restart counts per service

### Health History Chart

The deployment detail page includes a chart showing health trends:
- Displays the last 100 health check results
- X-axis shows time, Y-axis shows health percentage (0-100%)
- Color-coded based on current status (green/yellow/red)
- Hover over data points to see exact values
- Helps identify patterns and recurring issues

## Health Checks

### Container Health

ReadyStackGo monitors container status via the Docker API:

- Container state (running, stopped, restarting, exited)
- Restart count (high restart counts indicate issues)
- Exit codes for stopped containers

### HTTP Health Checks

For services exposing health endpoints, configure checks in the manifest:

```yaml
services:
  api:
    image: myapp/api:latest
    health:
      type: http
      url: http://api:8080/health
      interval: 30s
      timeout: 5s
      retries: 3
```

### Health Check Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| `type` | - | Check type: `http`, `tcp`, `command` |
| `url` | - | URL for HTTP checks |
| `interval` | `30s` | Time between checks |
| `timeout` | `5s` | Maximum wait time |
| `retries` | `3` | Failures before unhealthy |

## Real-time Updates

Health status updates are pushed to the UI via SignalR:

- Immediate notification when services start/stop
- Operation mode changes reflected instantly
- No manual refresh required

## Health History

ReadyStackGo stores health snapshots for trend analysis:

- View health over time (last hour, day, week)
- Identify patterns (e.g., nightly restarts)
- Correlate issues with deployments

## API Endpoints

### Get Stack Health

```http
GET /api/deployments/{deploymentId}/health
```

Response:
```json
{
  "deploymentId": "abc123",
  "stackName": "my-app",
  "overallStatus": "Healthy",
  "operationMode": "Normal",
  "services": [
    {
      "name": "api",
      "status": "Healthy",
      "containerId": "a1b2c3",
      "restartCount": 0
    }
  ],
  "capturedAtUtc": "2024-01-15T10:30:00Z"
}
```

### Get Environment Health Summary

```http
GET /api/environments/{environmentId}/health-summary
```

Returns aggregated health for all stacks in an environment.

### Get Health History

```http
GET /api/deployments/{deploymentId}/health/history?hours=24
```

Returns health snapshots for the specified time period.

## Best Practices

1. **Configure health checks** for all production services
2. **Set appropriate intervals** - too frequent checks add load
3. **Use meaningful endpoints** - health endpoints should test real dependencies
4. **Monitor restart counts** - frequent restarts indicate problems
5. **Review health history** before deployments

## Troubleshooting

### Stack shows "Unknown" status

- Verify the environment is connected
- Check if containers exist
- Ensure Docker API is accessible

### Health checks failing

- Verify the health endpoint URL is correct
- Check network connectivity between containers
- Review container logs for errors

### High restart counts

- Check container logs for crash causes
- Verify resource limits (memory, CPU)
- Review dependency availability (databases, APIs)

## See Also

- [Operation Mode](./Operation-Mode.md) - Managing maintenance and migration modes
- [Troubleshooting](./Troubleshooting.md) - General troubleshooting guide
- [API Reference](../Reference/API-Reference.md) - Full API documentation
