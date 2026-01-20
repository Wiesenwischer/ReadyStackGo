---
title: Stack Upgrade & Rollback
description: How to upgrade stacks to new versions and rollback on failures
---

This guide explains how to upgrade existing deployments to newer versions and how the automatic rollback mechanism protects your deployments.

## Overview

ReadyStackGo provides built-in support for:

1. **Version Detection** - Automatically checks if newer versions are available
2. **Stack Upgrade** - Upgrades a running deployment to a new version
3. **Automatic Rollback** - Restores the previous state if an upgrade fails

---

## Upgrade Process

### How Upgrades Work

When you upgrade a deployment, ReadyStackGo follows these steps:

1. **Create Snapshot** - Captures the current state (version, variables, services)
2. **Pull New Images** - Downloads container images for the new version
3. **Start New Containers** - Starts the upgraded services
4. **Point of No Return** - Once containers are running, the upgrade is committed
5. **Clear Snapshot** - Removes the snapshot after successful upgrade

### Checking for Updates

On the deployment detail page, ReadyStackGo automatically checks if a newer version is available in the catalog. If an upgrade is available, you'll see a blue banner:

**"Upgrade Available: 1.0.0 → 2.0.0"**

The banner also shows:
- **New Variables** - Variables added in the new version
- **Removed Variables** - Variables that no longer exist

### Performing an Upgrade

1. Navigate to **Deployments** and select your deployment
2. If an upgrade is available, click the **Upgrade** button
3. Confirm the upgrade in the dialog
4. Monitor the progress in real-time via the live progress indicator

:::caution[Prerequisites]
A deployment must be in **Running** status to be upgraded. Stopped, failed, or pending deployments cannot be upgraded directly.
:::

### Real-Time Progress Tracking

ReadyStackGo provides live progress updates during upgrades via SignalR:

- **Progress Bar** - Shows overall completion percentage
- **Current Phase** - Displays the current operation (Pulling Images, Starting Services, etc.)
- **Service Counter** - Shows how many services have been processed
- **Live Status** - Green indicator confirms real-time connection

The progress updates automatically without page refresh. If the connection is temporarily lost, the UI will show "Reconnecting..." and resume updates when restored.

### Variable Handling During Upgrade

When upgrading to a new version:

- **Existing variables** are preserved and merged with the new configuration
- **New required variables** must be configured before the upgrade starts
- **Removed variables** are no longer used but the values are kept in the snapshot

---

## Rollback Mechanism

### Point of No Return Semantics

ReadyStackGo uses a "Point of No Return" model for upgrades:

| Phase | Can Rollback? | Description |
|-------|---------------|-------------|
| Snapshot Created | No | Upgrade hasn't started yet |
| Pulling Images | Yes | If image pull fails, rollback is possible |
| Starting Containers | Yes | If container start fails, rollback is possible |
| **Containers Running** | **No** | Point of No Return passed |
| Upgrade Complete | No | Snapshot is cleared |

### When Rollback is Available

A rollback is available when:
- The upgrade **failed before** containers started running
- A **snapshot exists** from the failed upgrade attempt

If you see an amber "Rollback Available" banner on the deployment detail page, you can restore the previous state.

### Performing a Rollback

1. Navigate to the failed deployment
2. Click the **Rollback** button in the amber banner
3. Confirm the rollback on the dedicated rollback page
4. Monitor the rollback progress in real-time
5. The deployment is restored to its previous version

:::note[What Gets Restored]
A rollback restores:
- **Stack Version** - Returns to the previous version
- **Variables** - Restores the original variable values
- **Services** - Redeploys using the previous container images
:::

### Rollback Progress Tracking

Just like upgrades, rollbacks also provide real-time progress updates:

- **Progress Bar** - Shows rollback completion percentage
- **Current Phase** - Displays operations like "Removing Old Containers", "Pulling Images", "Starting Services"
- **Live Updates** - Automatic updates via SignalR connection

The rollback page shows a confirmation screen before starting, allowing you to review what version you're rolling back to.

---

## API Endpoints

### Check for Upgrade

```http
GET /api/environments/{environmentId}/deployments/{deploymentId}/upgrade/check
```

**Response:**
```json
{
  "success": true,
  "upgradeAvailable": true,
  "currentVersion": "1.0.0",
  "latestVersion": "2.0.0",
  "latestStackId": "source:product:stack",
  "newVariables": ["NEW_VAR"],
  "removedVariables": ["OLD_VAR"],
  "canUpgrade": true,
  "cannotUpgradeReason": null
}
```

### Perform Upgrade

```http
POST /api/environments/{environmentId}/deployments/{deploymentId}/upgrade
Content-Type: application/json

{
  "stackId": "source:product:stack",
  "variables": {
    "NEW_VAR": "value"
  },
  "sessionId": "signalr-session-id"
}
```

### Get Rollback Info

```http
GET /api/environments/{environmentId}/deployments/{deploymentId}/rollback
```

**Response:**
```json
{
  "canRollback": true,
  "rollbackTargetVersion": "1.0.0",
  "snapshotDescription": "Before upgrade to v2.0.0",
  "snapshotCreatedAt": "2024-01-15T10:30:00Z"
}
```

### Perform Rollback

```http
POST /api/environments/{environmentId}/deployments/{deploymentId}/rollback
Content-Type: application/json

{
  "sessionId": "signalr-session-id"
}
```

The `sessionId` is optional but recommended for real-time progress tracking. If provided, the client should subscribe to the SignalR deployment hub with this session ID before calling the endpoint.

---

## Best Practices

### Before Upgrading

1. **Check Release Notes** - Review what changed in the new version
2. **Backup Data** - If your stack uses persistent volumes, consider backing up data
3. **Test in Development** - Try the upgrade in a non-production environment first
4. **Schedule Maintenance** - Use Maintenance Mode to prevent alerts during upgrade

### Handling Failed Upgrades

If an upgrade fails:

1. **Check the Error Message** - Understand why the upgrade failed
2. **Review Logs** - Check container logs for detailed error information
3. **Rollback if Available** - Use the rollback feature to restore the previous state
4. **Fix the Issue** - Address the root cause before retrying the upgrade

### Version Strategy

- **Stay Current** - Regular updates include security fixes and improvements
- **Test First** - Always test upgrades in non-production environments
- **Document Changes** - Keep track of what versions are deployed where

---

## Troubleshooting

### "Cannot Upgrade" Message

| Reason | Solution |
|--------|----------|
| "Deployment must be running" | Start the deployment first, then upgrade |
| "No newer version available" | The current version is already the latest |
| "Product not found in catalog" | Ensure the stack source is still configured |

### Rollback Not Available

Rollback is only available if:
- The deployment is in **Failed** status
- A **snapshot exists** from the failed upgrade

If the upgrade passed the Point of No Return (containers started), the snapshot is already cleared and rollback is not possible.

---

## Further Documentation

- [Stack Deployment](/en/docs/stack-deployment/)
- [RSGo Manifest Format](/en/reference/manifest-format/)
- [Variable Types](/en/reference/variable-types/)
