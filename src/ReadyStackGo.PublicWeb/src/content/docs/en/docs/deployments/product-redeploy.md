---
title: Product Redeploy
description: Guide to redeploying a product with real-time per-stack progress tracking
---

A redeploy restarts all stacks of a product — with a fresh image pull using the same version and configuration. The split-view interface shows the progress of each stack in real time with detailed phase, service, and init container information.

## When to Use Redeploy

| Use Case | Description |
|----------|-------------|
| **Images updated** | Container images were rebuilt (e.g. `:latest` tag) without a version change |
| **Re-apply configuration** | Environment variables or mounts should be reloaded without a version upgrade |
| **Fix container errors** | Containers in *Crashed* or *Unhealthy* state after an issue |
| **Init containers** | Re-run database migrations or setup containers |

:::note[Redeploy vs. Upgrade]
A redeploy uses the **same version** — no new variables are read from the manifest.
For a version change, use the [Upgrade feature](/en/docs/deployments/stack-upgrade/) instead.
:::

---

## Step by Step: Product Redeploy

### Step 1: Open the Product Deployment

Navigate to the **Deployments** page and select your product. On the product deployment detail page, you'll find the **Redeploy** button for products in *Running* status.

![Deployments page with product deployment](/images/docs/deployments/product-redeploy-01-deployments.png)

---

### Step 2: Confirm the Redeploy

After clicking *Redeploy*, the confirmation screen shows all relevant information:

![Redeploy confirmation with product details and stack list](/images/docs/deployments/product-redeploy-03-confirm.png)

The confirmation screen displays:

- **Product name** and current version
- **Target environment**
- **Number of stacks**
- **List of all stacks** to be redeployed, with service count

Click **Redeploy All Stacks** to start the redeploy, or **Cancel** to return to the product detail page.

:::caution[Image Pull]
During a redeploy, all container images are pulled fresh from the registry.
Make sure the registry is reachable and the images are available.
:::

---

### Step 3: Real-Time Progress View

Once the redeploy starts, the view switches to the **split-view progress display**:

![Redeploy in progress with split-view and per-stack details](/images/docs/deployments/product-redeploy-04-progress.png)

#### Left Column — Stack Overview

All stacks of the product are displayed as clickable entries. Each entry shows:

- **Status indicator** (spinner or icon)
- **Stack name** in the corresponding color
- **Status badge** with current state

| Status Badge | Meaning |
|-------------|---------|
| ⚪ **Pending** | Waiting to be processed |
| 🟠 **Removing** | Old stack is being removed (docker compose down) |
| 🔵 **Deploying** | Stack is being deployed (docker compose up) |
| 🟢 **Running** | Stack redeployed successfully |
| 🔴 **Failed** | Stack redeploy failed |

#### Right Column — Detail Panel

Depending on the selected stack's status, the detail panel shows different information:

| Status | Detail Panel Content |
|--------|----------------------|
| Pending | Waiting message |
| Removing | Orange spinner with "Removing existing stack before redeploy..." |
| Deploying | `DeploymentProgressPanel` with phase, progress bar, service counter, init container logs |
| Running | Green success message + last known progress |
| Failed | Red error message + last known progress |

#### Overall Progress Bar

At the top, a blue bar shows the overall progress: how many stacks have completed out of the total count.

:::tip[Selecting a Stack]
Click any stack in the left list to view its details in the right panel.
The system automatically selects the currently active stack — you can switch manually at any time.
:::

---

### Step 4: Completion

After a successful redeploy, the success screen shows an overview of all redeployed stacks:

![Redeploy completed successfully](/images/docs/deployments/product-redeploy-05-success.png)

From here you can:

- **View Deployment** — Return to the product deployment
- **All Deployments** — Navigate to the deployments overview

---

## DeploymentProgressPanel

The right detail panel in *Deploying* status shows the same progress display as during the initial deployment:

| Element | Description |
|---------|-------------|
| **Phase** | Current deployment phase (e.g. *Pulling Images*, *Starting Services*) |
| **Progress Bar** | Percentage progress of the current phase |
| **Service Counter** | Number of processed vs. total services |
| **Current Service** | Name of the service currently being processed |
| **Init Container Logs** | Terminal output from init containers (e.g. database migrations) |

All data is transmitted in real time via **SignalR** — no page reload required.

---

## API Endpoint

```http
POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/redeploy
Content-Type: application/json

{
  "sessionId": "optional-signalr-session-id"
}
```

**Response:**

```json
{
  "success": true,
  "message": "Product redeployed successfully",
  "stackResults": [
    {
      "stackName": "e2e-platform-backend",
      "stackDisplayName": "Backend",
      "serviceCount": 2,
      "success": true
    }
  ]
}
```

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sessionId` | string | No | SignalR session ID for real-time progress updates |

The `sessionId` is optional but recommended for real-time progress tracking in the UI. The client must subscribe to the SignalR Deployment Hub with this session ID before calling the endpoint.

---

## Further Documentation

- [Product Deployment](/en/docs/deployments/product-deployment/) — Deploy products for the first time
- [Stack Upgrade](/en/docs/deployments/stack-upgrade/) — Upgrade stacks to new versions
- [Stack Deployment](/en/docs/deployments/stack-deployment/) — Deploy individual stacks
