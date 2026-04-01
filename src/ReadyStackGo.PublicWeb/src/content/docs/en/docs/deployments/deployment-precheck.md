---
title: Deployment Precheck
description: Automatic infrastructure validation before deployment – images, ports, networks and more
---

Before deploying a stack, ReadyStackGo automatically runs a **Deployment Precheck**. This validates all prerequisites to detect problems before containers are removed or recreated.

## Overview

| Check | Description | Severity |
|-------|------------|----------|
| **Variable Validation** | Are all required variables set and valid? | Error |
| **Existing Deployment** | Is there already a deployment with this stack name? | Warning / Error |
| **Image Availability** | Are all Docker images available locally or remotely? | Error |
| **Port Conflicts** | Are host ports already in use by other containers? | Error |
| **Network Availability** | Do external networks required by the stack exist? | Error |
| **Volume Status** | Do volumes already exist for this stack? | Warning |

---

## Step by Step: Precheck During Deployment

### Step 1: Select a Stack from the Catalog

Open the **Stack Catalog** and select a product. Click **View Details** to see the available stacks.

![Stack Catalog with available products](/images/docs/precheck-01-catalog.png)

---

### Step 2: Configure the Stack

On the deploy page, configure the **Stack Name** and **Environment Variables**. The precheck starts automatically once a valid stack name is entered.

![Deploy page with stack configuration](/images/docs/precheck-02-configure.png)

---

### Step 3: Review Precheck Results

The **Deployment Precheck** panel appears below the configuration and shows the results of all checks:

- ✓ **Green** – Check passed
- ⚠ **Yellow** – Warning (deployment possible, but review the note)
- ✗ **Red** – Error (deployment blocked)

![Precheck panel with results – all checks passed](/images/docs/precheck-03-results.png)

:::tip[Service Badges]
For service-specific checks (e.g. Image Availability), the affected service name is shown as a badge. This helps you quickly identify which service is causing the issue.
:::

---

### Step 4: Run Re-Check

Use the **Re-Check** button to re-run all checks at any time – for example, after changing a variable or pulling a missing Docker image.

![Precheck panel with Re-Check button](/images/docs/precheck-04-recheck-button.png)

---

### Step 5: Start Deployment

When all checks pass (no errors), the **Deploy** button is enabled. If there are errors, the button is disabled and you must fix the issues first.

![Deploy button enabled after successful precheck](/images/docs/precheck-05-deploy-button.png)

:::caution[Errors Block Deployment]
When the precheck reports errors, the deployment cannot be started. Fix the reported issues and click **Re-Check** to re-run the checks.
:::

---

## Checks in Detail

### Variable Validation

Checks whether all variables marked as **Required** have a value and whether values match defined patterns. Each missing or invalid variable is reported as a separate error.

### Existing Deployment

Detects whether a deployment with the same stack name already exists:

| Existing Deployment Status | Result |
|---|---|
| **Running** | Warning – upgrade scenario |
| **Installing / Upgrading** | Error – deployment blocked |
| **Failed** | Warning – retry scenario |
| **Removed** | OK – fresh install |

### Image Availability

Checks for each service whether the Docker image is available locally or can be pulled remotely. For private registries, authentication is also validated.

### Port Conflicts

Detects whether host ports are already in use by running containers. Containers belonging to the same stack (upgrade scenario) are excluded.

### Network Availability

Checks whether external networks required by the stack exist. Stack-managed networks are automatically created during deployment.

### Volume Status

Reports whether named volumes already exist:
- **New volume** → OK (will be created during deployment)
- **Existing volume during upgrade** → OK (data preserved)
- **Existing volume during fresh install** → Warning (existing data will be reused)

---

## Hooks API: Dry Run

The precheck can also be executed via the **Hooks API** without starting an actual deployment:

```bash
curl -X POST http://rsgo:8080/api/hooks/deploy \
  -H "X-API-Key: rsgo_your_key" \
  -H "Content-Type: application/json" \
  -d '{
    "stackName": "my-stack",
    "stackId": "source:stack:version",
    "dryRun": true
  }'
```

With `dryRun: true`, only the precheck is executed and the result is returned. If there are errors, the API responds with `422 Unprocessable Entity`.

---

## Timeout and Error Handling

- **Timeout**: The entire precheck has a 30-second timeout
- **Individual rule failures**: If a single check fails, it is reported as a warning – other checks continue running
- **Docker connection errors**: Reported immediately as an error
- **Precheck failures**: If the precheck itself fails (e.g. network issues), deployment can still proceed
