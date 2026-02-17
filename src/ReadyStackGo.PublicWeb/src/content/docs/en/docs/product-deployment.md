---
title: Product Deployment
description: Deploy entire products with all stacks in a single operation
---

With multi-stack products like microservices architectures with 10+ stacks, each stack previously had to be deployed individually. **Product Deployment** rolls out the entire product in one operation — with shared variables, progress tracking, and coordinated lifecycle management.

## Overview

| Aspect | Single Stack Deployment | Product Deployment |
|--------|------------------------|-------------------|
| **Deploy** | One stack per operation | All stacks of a product at once |
| **Variables** | Configure per stack individually | Shared Variables once, then per stack |
| **Progress** | One stack with its own session | One SessionId, N stacks with overall progress |
| **Status** | No aggregated status | `ProductDeploymentStatus` with state machine |
| **Error Handling** | Stack-individual | `ContinueOnError` — continue on partial failures |

---

## Concept: Two-Level Architecture

Product Deployment operates on two levels:

1. **ProductDeployment** (Aggregate Root) — coordinates all stacks, tracks overall status
2. **Deployment** (per stack) — existing stack deployment logic with container operations

```
┌─────────────────────────────────────────────┐
│ ProductDeployment                            │
│ Status: Running │ Version: 3.1.0             │
│                                              │
│ ┌─ Stack: infrastructure (Order: 0) ───┐     │
│ │ Status: Running                      │──→  Deployment (infra-stack)
│ └──────────────────────────────────────┘     │
│                                              │
│ ┌─ Stack: identity-access (Order: 1) ──┐     │
│ │ Status: Running                      │──→  Deployment (identity-stack)
│ └──────────────────────────────────────┘     │
│                                              │
│ ┌─ Stack: business (Order: 2) ─────────┐     │
│ │ Status: Running                      │──→  Deployment (business-stack)
│ └──────────────────────────────────────┘     │
└─────────────────────────────────────────────┘
```

Stacks are deployed **sequentially** in manifest order — this respects dependencies between stacks (e.g., database before application server).

---

## Status Lifecycle

Product Deployments transition through these statuses:

```
Deploying ──→ Running              (all stacks successful)
          ──→ PartiallyRunning     (some stacks failed)
          ──→ Failed               (all stacks failed)

Running ──→ Upgrading ──→ Running / PartiallyRunning / Failed
        ──→ Removing  ──→ Removed (terminal)

Failed ──→ Upgrading (retry with new version)
       ──→ Removing  (cleanup)
```

| Status | Meaning |
|--------|---------|
| `Deploying` | Deployment in progress, stacks being rolled out sequentially |
| `Running` | All stacks successfully deployed and active |
| `PartiallyRunning` | Some stacks running, others failed |
| `Failed` | Deployment completely failed |
| `Upgrading` | Upgrade to a new version in progress |
| `Removing` | All stacks being removed |
| `Removed` | All stacks removed (terminal state) |

---

## Variable Configuration

Product Deployment supports a three-tier variable system:

1. **Stack Defaults** — default values defined in the stack definition
2. **Shared Variables** — product-wide variables (e.g., database host)
3. **Per-Stack Overrides** — stack-specific overrides

Priority (ascending): Stack Defaults → Shared Variables → Per-Stack Overrides.

:::tip[Shared Variables]
Set commonly used values like database connections or API URLs as Shared Variables. These are automatically passed to all stacks and can be overridden per stack when needed.
:::

---

## API Endpoints

### POST /api/environments/{environmentId}/product-deployments

Starts a new Product Deployment. All stacks of the product are deployed sequentially.

**Permission:** `Deployments.Create`

**Request:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `productId` | string | Yes | Product ID from the catalog (e.g., `stacks:myproduct:1.0.0`) |
| `stackConfigs` | array | Yes | Configuration for each stack |
| `stackConfigs[].stackId` | string | Yes | Stack ID from the catalog |
| `stackConfigs[].deploymentStackName` | string | Yes | Name for the deployment |
| `stackConfigs[].variables` | object | No | Stack-specific variables |
| `sharedVariables` | object | No | Product-wide shared variables |
| `sessionId` | string | No | Client-generated session ID for SignalR tracking |
| `continueOnError` | boolean | No | Continue on error (default: `true`) |

```json
{
  "productId": "stacks:ams.project:3.1.0",
  "stackConfigs": [
    {
      "stackId": "stacks:ams.project:infrastructure:3.1.0",
      "deploymentStackName": "ams-infra",
      "variables": {
        "DB_PASSWORD": "secret123"
      }
    },
    {
      "stackId": "stacks:ams.project:identity:3.1.0",
      "deploymentStackName": "ams-identity",
      "variables": {}
    },
    {
      "stackId": "stacks:ams.project:business:3.1.0",
      "deploymentStackName": "ams-business",
      "variables": {}
    }
  ],
  "sharedVariables": {
    "DB_HOST": "postgres.local",
    "REDIS_URL": "redis://cache:6379"
  },
  "continueOnError": true
}
```

**Response (200):**

```json
{
  "success": true,
  "productDeploymentId": "a1b2c3d4-...",
  "productName": "ams.project",
  "productVersion": "3.1.0",
  "status": "Running",
  "sessionId": "product-ams.project-20260217120000000",
  "stackResults": [
    {
      "stackName": "infrastructure",
      "stackDisplayName": "Infrastructure",
      "success": true,
      "deploymentId": "d1e2f3...",
      "deploymentStackName": "ams-infra",
      "serviceCount": 3
    },
    {
      "stackName": "identity",
      "stackDisplayName": "Identity Access",
      "success": true,
      "deploymentId": "g4h5i6...",
      "deploymentStackName": "ams-identity",
      "serviceCount": 2
    }
  ]
}
```

**Error Response (400) — Product not found:**

```json
{
  "success": false,
  "message": "Product 'nonexistent:product:1.0.0' not found in catalog."
}
```

---

### GET /api/environments/{environmentId}/product-deployments

Lists all Product Deployments in an environment (excluding `Removed`).

**Permission:** `Deployments.Read`

**Response (200):**

```json
{
  "success": true,
  "productDeployments": [
    {
      "productDeploymentId": "a1b2c3d4-...",
      "productGroupId": "stacks:ams.project",
      "productName": "ams.project",
      "productDisplayName": "AMS Project",
      "productVersion": "3.1.0",
      "status": "Running",
      "createdAt": "2026-02-17T12:00:00Z",
      "completedAt": "2026-02-17T12:05:30Z",
      "totalStacks": 3,
      "completedStacks": 3,
      "failedStacks": 0,
      "canUpgrade": true,
      "canRemove": true
    }
  ]
}
```

---

### GET /api/environments/{environmentId}/product-deployments/{id}

Returns a specific Product Deployment with full stack details.

**Permission:** `Deployments.Read`

**Response (200):**

```json
{
  "productDeploymentId": "a1b2c3d4-...",
  "environmentId": "env-123",
  "productGroupId": "stacks:ams.project",
  "productId": "stacks:ams.project:3.1.0",
  "productName": "ams.project",
  "productDisplayName": "AMS Project",
  "productVersion": "3.1.0",
  "status": "Running",
  "createdAt": "2026-02-17T12:00:00Z",
  "completedAt": "2026-02-17T12:05:30Z",
  "continueOnError": true,
  "totalStacks": 3,
  "completedStacks": 3,
  "failedStacks": 0,
  "upgradeCount": 0,
  "canUpgrade": true,
  "canRemove": true,
  "durationSeconds": 330.5,
  "stacks": [
    {
      "stackName": "infrastructure",
      "stackDisplayName": "Infrastructure",
      "stackId": "stacks:ams.project:infrastructure:3.1.0",
      "deploymentId": "d1e2f3...",
      "deploymentStackName": "ams-infra",
      "status": "Running",
      "startedAt": "2026-02-17T12:00:01Z",
      "completedAt": "2026-02-17T12:02:15Z",
      "order": 0,
      "serviceCount": 3,
      "isNewInUpgrade": false
    }
  ],
  "sharedVariables": {
    "DB_HOST": "postgres.local",
    "REDIS_URL": "redis://cache:6379"
  }
}
```

---

### GET /api/environments/{environmentId}/product-deployments/by-product/{groupId}

Returns the active Product Deployment for a specific Product Group.

**Permission:** `Deployments.Read`

The response has the same format as `GET .../{id}`.

:::note
`groupId` is the logical product identifier without version, e.g., `stacks:ams.project`. The most recent non-removed deployment is always returned.
:::

---

## Real-Time Progress via SignalR

During deployment, ReadyStackGo sends real-time updates via SignalR:

1. **Before each stack**: Progress message with stack index and total count
2. **During each stack**: Service-level progress (from existing Stack Deployment)
3. **On completion**: Overall result with status message

Connect via the `DeploymentHub` and subscribe to the session ID:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/deploymentHub")
  .build();

connection.on("DeploymentProgress", (data) => {
  console.log(`${data.phase}: ${data.message} (${data.percentComplete}%)`);
});

await connection.start();
await connection.invoke("SubscribeToDeployment", sessionId);
```

---

## Error Handling

| HTTP Status | Meaning |
|-------------|---------|
| 200 | Success |
| 400 | Invalid request (product not found, active deployment exists, empty stack configuration) |
| 401 | Not authenticated |
| 403 | Not authorized (missing permission) |
| 404 | Product Deployment not found (for GET requests) |

### ContinueOnError Behavior

| `continueOnError` | On Stack Failure |
|-------------------|-----------------|
| `true` (default) | Next stack is deployed anyway. Final status: `PartiallyRunning` |
| `false` | Deployment is aborted. Remaining stacks stay on `Pending`. Final status: `Failed` |
