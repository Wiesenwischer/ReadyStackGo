---
title: Stop & Restart Containers
description: Stop, restart, or restore containers of a product deployment — with a confirmation screen and real-time progress.
---

**Container Control** lets you stop or restart the Docker containers of a product deployment without removing it. All actions go through a confirmation screen and show real-time progress.

## Overview

| Action | Description |
|--------|-------------|
| **Stop Containers** | Stop all containers of the product (deployment is retained) |
| **Restart Containers** | Restart stopped containers (restore function) |

---

## Step by Step: Stop Containers

### Step 1: Product Deployment Detail

Open the detail page of a running product deployment. When the status is **Running** or **PartiallyRunning**, the **Stop Containers** and **Restart Containers** links are visible.

![Product deployment detail with Stop/Restart buttons](/images/docs/container-control-01-buttons.png)

---

### Step 2: Stop Confirmation

Clicking **Stop Containers** opens the confirmation screen:

- **Product Details** — name, version, environment
- **Stacks to stop** — list of all stacks with service count
- **Cancel** — returns to the deployment detail page
- **Stop All Containers** — starts the stop operation

![Stop containers confirmation screen](/images/docs/container-control-02-stop-confirm.png)

:::note[No data loss]
Stopping only halts the containers. The deployment and all configuration are retained. Containers can be started again at any time via **Restart Containers**.
:::

---

### Step 3: Stop Progress

While stopping, the page shows a progress spinner.

![Container stop in progress](/images/docs/container-control-03-stop-loading.png)

---

### Step 4: Stop Result

When complete, the result screen shows the status of each stack.

![Container stop result](/images/docs/container-control-04-stop-result.png)

**Successfully stopped:**
- Heading: "Containers Stopped Successfully!"
- List of all stacks with result

**With errors:**
- Heading: "Stop Completed with Errors"
- Error messages per stack

---

### Step 5: Stopped Status in Deployment

After stopping, the deployment detail page shows **Stopped** status.

![Product deployment in Stopped status](/images/docs/container-control-05-stopped-status.png)

From here the product can be started again via **Restart Containers**.

---

## Step by Step: Restart Containers

### Step 1: Restart Confirmation

Via **Restart Containers** (on the deployment detail page or directly at `/restart-product/:id`) the restart confirmation screen opens:

- **Product Details**
- **Stacks to restart**
- **Restart All Containers** — restarts the containers

![Restart containers confirmation screen](/images/docs/container-control-06-restart-confirm.png)

---

### Step 2: Restart Result

After the restart the page shows the result.

![Container restart result](/images/docs/container-control-07-restart-result.png)

**Successfully restarted:** Heading "Containers Restarted Successfully!" — the product is back in **Running** status.

---

## When to Use Which Action

| Scenario | Recommended action |
|----------|--------------------|
| Maintenance window | **Stop** → maintenance → **Restart** |
| Containers are unresponsive | **Restart Containers** |
| Free up resources | **Stop Containers** |
| Permanently remove product | [Remove Product](/en/docs/product-remove/) |

---

## API Endpoints

| Method | Endpoint | Description | Permission |
|--------|----------|-------------|------------|
| `POST` | `/api/environments/{envId}/product-deployments/{id}/stop-containers` | Stop containers | `Deployments.Update` |
| `POST` | `/api/environments/{envId}/product-deployments/{id}/restart-containers` | Restart containers | `Deployments.Update` |

---

## Error Handling

| Situation | Behavior |
|-----------|----------|
| Product cannot be stopped | Error screen instead of confirmation screen |
| Individual stack fails | Other stacks continue to be stopped/started |
| Already stopped | `canRestart: false` → error screen when opening `/restart-product/:id` |
