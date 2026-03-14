---
title: Maintenance Mode
description: Put products into maintenance mode and release them in a controlled manner
---

**Maintenance Mode** allows you to put a Product Deployment into a dedicated maintenance state. All containers are stopped, enabling safe execution of maintenance tasks such as database migrations, hardware updates, or planned downtimes. The trigger system ensures that manually activated maintenance cannot be accidentally overridden by the observer.

## Overview

| Aspect | Normal Mode | Maintenance Mode |
|--------|-------------|-----------------|
| **Containers** | Running normally | Stopped |
| **Trigger** | — | Manual or Observer |
| **Exit** | — | Only by the trigger that activated maintenance |
| **Observer** | Can activate maintenance | Cannot override manual maintenance |

### Trigger Ownership

The core principle: **Whoever activated maintenance also controls when it ends.**

- **Manual Trigger**: Maintenance was activated by a user via the UI or API. Only the user can end maintenance — the observer has no effect.
- **Observer Trigger**: Maintenance was automatically activated by the Maintenance Observer (e.g., an external health check source reports maintenance). Maintenance is only lifted when the observer reports Normal again.

---

## Step by Step: Activating Maintenance Mode

### Step 1: Open Product Deployment

Navigate to the **Product Deployment Detail** page. In normal state, you'll see **Operation Mode: Normal** in the overview cards and the **Enter Maintenance** button in the action bar.

![Product deployment in Normal mode with Enter Maintenance button](/images/docs/maintenance-01-normal-mode.png)

---

### Step 2: Activate Maintenance Mode

Click the **Enter Maintenance** button. ReadyStackGo puts the product into maintenance mode and stops all containers of the associated stacks.

After activation, the view changes:

- A **Maintenance badge** appears next to the status
- The **Maintenance info panel** shows the trigger type (Manual)
- The **Operation Mode** switches to "Maintenance"
- The button changes to **Exit Maintenance**

![Product deployment in Maintenance Mode with info panel](/images/docs/maintenance-02-in-maintenance.png)

:::tip[Maintenance Reason]
When activating via the API, you can optionally provide a reason (e.g., "Scheduled database migration"). This is displayed in the info panel.
:::

---

### Step 3: Check Status

The overview cards show the current state at a glance. During maintenance, the **Operation Mode** is set to "Maintenance" and the info panel displays details about the active maintenance.

![Overview cards during Maintenance Mode](/images/docs/maintenance-03-overview-cards.png)

---

### Step 4: Stacks During Maintenance

The **Stacks table** remains visible even during maintenance mode. It shows all stacks belonging to the product along with their respective status.

![Stacks table during maintenance with reason](/images/docs/maintenance-05-stacks-during.png)

---

### Step 5: Exit Maintenance Mode

Click **Exit Maintenance** to restore normal operation. ReadyStackGo restarts all containers of the associated stacks.

- The **Operation Mode** switches back to "Normal"
- The **Maintenance info panel** disappears
- The button changes back to **Enter Maintenance**

![Product deployment after exiting Maintenance Mode](/images/docs/maintenance-04-exited.png)

:::caution[Observer Maintenance]
If maintenance was activated by the observer, it **cannot** be ended manually via the UI. The exit button is not visible in this case. Maintenance is only lifted when the external source reports Normal again.
:::

---

## API Endpoint

Maintenance mode can also be controlled via the REST API:

```
PUT /api/environments/{environmentId}/product-deployments/{productDeploymentId}/operation-mode
```

### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `mode` | string | Yes | `"Maintenance"` or `"Normal"` |
| `reason` | string | No | Optional reason for the maintenance |

### Examples

**Activate maintenance:**
```json
{
  "mode": "Maintenance",
  "reason": "Scheduled database migration"
}
```

**Exit maintenance:**
```json
{
  "mode": "Normal"
}
```

### HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Mode changed successfully |
| 404 | Product deployment not found |
| 409 | Transition blocked — trigger ownership violated (e.g., manually exiting observer maintenance) |

---

## Error Handling

| Situation | Behavior |
|-----------|----------|
| Manual exit during observer maintenance | Blocked with HTTP 409 — observer controls the exit |
| Product already in the desired mode | No action, successful response (no-op) |
| Observer reports Normal during manual maintenance | No action — manual trigger takes precedence |
