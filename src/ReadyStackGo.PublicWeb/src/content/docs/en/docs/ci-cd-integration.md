---
title: CI/CD Integration
description: Automated deployments via API keys and webhooks in CI/CD pipelines
---

ReadyStackGo integrates with automated build and release processes. Using **API Keys** and **Webhook Endpoints**, CI/CD pipelines can trigger deployments, perform upgrades, and synchronize the stack catalog.

## Overview

| Use Case | Webhook | Description |
|----------|---------|-------------|
| **Dev/Test** | `/api/hooks/redeploy` | Automatically redeploy the stack after every build (fresh images) |
| **Release** | `/api/hooks/upgrade` | Upgrade to a new catalog version |
| **Catalog Sync** | `/api/hooks/sync-sources` | Update the catalog after manifest changes |

---

## Step by Step: Creating an API Key

Pipeline access uses **API Keys** instead of JWT tokens. Here's how to create one:

### Step 1: Open Settings

Navigate to **Settings** in the main menu. You'll see the **CI/CD Integration** entry.

![Settings page with CI/CD Integration](/images/docs/cicd-01-settings-nav.png)

---

### Step 2: CI/CD Integration

Click on **CI/CD Integration**. If no keys have been created yet, you'll see an empty page with the **Create API Key** button.

![CI/CD page with no API keys](/images/docs/cicd-02-empty-state.png)

---

### Step 3: Configure Key

Click **Create API Key** and fill in the form:

- **Name** – A descriptive name (e.g., "Azure DevOps Deploy")
- **Permissions** – Select the required permissions:
  - **Redeploy** – Restart the stack with fresh images
  - **Upgrade** – Upgrade the stack to a new catalog version
  - **Sync Sources** – Synchronize catalog sources
- **Environment** (optional) – Restrict the key to a specific environment
- **Expiry** (optional) – Set an expiration date

![Create API Key modal with filled form](/images/docs/cicd-03-create-modal.png)

:::tip[Environment Scope]
When an API Key is linked to a specific environment, the `environmentId` does not need to be included in every webhook request – it is automatically resolved from the key.
:::

---

### Step 4: Copy the Key

After creation, the full API key is displayed **once**. **Copy it immediately** – it will not be shown again!

![API key created – copy the key immediately](/images/docs/cicd-04-key-created.png)

Store the key as a secret in your CI/CD system (e.g., Azure DevOps Variable Group, GitHub Secret).

---

### Step 5: Key in the List

The created key appears in the overview with name, permissions, environment, expiration date, and status.

![API key list with the new key](/images/docs/cicd-05-key-list.png)

---

## Webhook Endpoints

All endpoints are located under `/api/hooks/` and require API Key authentication via the `X-Api-Key` header.

### POST /api/hooks/redeploy

Triggers a redeployment of a running stack. Stops existing containers, pulls fresh images, and restarts – using the same variables and settings.

**Parameters:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `stackName` | string | Yes | Name of the deployed stack (as shown in the Deployments overview) |
| `environmentId` | string | No* | Environment ID. *Not required when using an environment-scoped API key. |

**Request:**
```json
{
  "stackName": "ams-project",
  "environmentId": "abc123-def4-..."
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Successfully triggered redeploy of 'ams-project'.",
  "deploymentId": "d4f8b2...",
  "stackName": "ams-project",
  "stackVersion": "6.4.0"
}
```

**Error Responses:**
```json
// 400 – Stack not found
{ "success": false, "message": "No deployment found for stack 'xyz' in environment '...'" }

// 400 – Stack not in Running status
{ "success": false, "message": "Deployment is in status 'Failed', only running deployments can be redeployed." }
```

**Permission:** `Hooks.Redeploy`

---

### POST /api/hooks/upgrade

Upgrades a stack to a specific catalog version. Validates the version in the catalog, merges optional new variables, and delegates to the existing upgrade flow.

**Parameters:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `stackName` | string | Yes | Name of the deployed stack |
| `targetVersion` | string | Yes | Target version from the catalog (e.g., `"6.5.0"`) |
| `environmentId` | string | No* | Environment ID. *Not required when using an environment-scoped API key. |
| `variables` | object | No | Additional or changed variables as key-value pairs |

**Request:**
```json
{
  "stackName": "ams-project",
  "targetVersion": "6.5.0",
  "environmentId": "abc123-def4-...",
  "variables": {
    "NEW_SETTING": "value"
  }
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Successfully upgraded 'ams-project' from 6.4.0 to 6.5.0.",
  "deploymentId": "d4f8b2...",
  "previousVersion": "6.4.0",
  "newVersion": "6.5.0"
}
```

**Error Responses:**
```json
// 400 – Version not in catalog
{ "success": false, "message": "Version '9.9.9' not found in catalog. Available versions: 6.4.0, 6.5.0" }

// 400 – Stack not in Running status
{ "success": false, "message": "Deployment is in status 'Failed', only running deployments can be upgraded." }
```

**Permission:** `Hooks.Upgrade`

---

### POST /api/hooks/sync-sources

Synchronizes all stack catalog sources (local directories and Git repositories). Useful after a Git push with updated manifests.

**Parameters:** No request body required.

**Response (200):**
```json
{
  "success": true,
  "stacksLoaded": 12,
  "sourcesSynced": 3,
  "message": "Synced 3 source(s), loaded 12 stack(s)."
}
```

**Permission:** `Hooks.SyncSources`

---

## Error Handling

| HTTP Status | Meaning |
|-------------|---------|
| 200 | Success |
| 400 | Invalid request (stack not found, invalid version, stack not Running) |
| 401 | Not authenticated (missing or invalid API key) |
| 403 | Not authorized (API key lacks the required permission) |
| 500 | Server error |

---

## Pipeline Examples

### curl

```bash
# Redeploy (fresh images)
curl -sf -X POST https://rsgo.example.com/api/hooks/redeploy \
  -H "X-Api-Key: rsgo_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6" \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project"}'

# Sync catalog sources
curl -sf -X POST https://rsgo.example.com/api/hooks/sync-sources \
  -H "X-Api-Key: rsgo_..."

# Upgrade to new version
curl -sf -X POST https://rsgo.example.com/api/hooks/upgrade \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project", "targetVersion": "6.5.0"}'
```

The `-sf` flag causes curl to exit with an error code on HTTP failures (401, 403, 500) – important so the pipeline stops on errors.

### GitHub Actions

```yaml
- name: Trigger Redeploy
  run: |
    curl -sf -X POST "${{ secrets.RSGO_URL }}/api/hooks/redeploy" \
      -H "X-Api-Key: ${{ secrets.RSGO_API_KEY }}" \
      -H "Content-Type: application/json" \
      -d '{"stackName": "${{ vars.STACK_NAME }}"}'
```

### Azure DevOps

#### Inline Call

```yaml
- script: |
    curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
      -H "X-Api-Key: $(RSGO_API_KEY)" \
      -H "Content-Type: application/json" \
      -d '{"stackName": "$(STACK_NAME)"}'
  displayName: Trigger Redeploy on ReadyStackGo
```

#### YAML Templates (Reusable)

Create reusable templates in your repository:

**`templates/rsgo-redeploy.yml`:**
```yaml
parameters:
  - name: stackName
    type: string

steps:
  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
        -H "X-Api-Key: $(RSGO_API_KEY)" \
        -H "Content-Type: application/json" \
        -d '{"stackName": "${{ parameters.stackName }}"}'
    displayName: Redeploy ${{ parameters.stackName }}
```

**`templates/rsgo-sync-upgrade.yml`:**
```yaml
parameters:
  - name: stackName
    type: string
  - name: targetVersion
    type: string

steps:
  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/sync-sources" \
        -H "X-Api-Key: $(RSGO_API_KEY)"
    displayName: Sync Catalog Sources

  - script: |
      curl -sf -X POST "$(RSGO_URL)/api/hooks/upgrade" \
        -H "X-Api-Key: $(RSGO_API_KEY)" \
        -H "Content-Type: application/json" \
        -d '{"stackName": "${{ parameters.stackName }}", "targetVersion": "${{ parameters.targetVersion }}"}'
    displayName: Upgrade ${{ parameters.stackName }} to ${{ parameters.targetVersion }}
```

**Usage in your pipeline:**
```yaml
steps:
  - template: templates/rsgo-redeploy.yml
    parameters:
      stackName: ams-project
```

```yaml
steps:
  - template: templates/rsgo-sync-upgrade.yml
    parameters:
      stackName: ams-project
      targetVersion: '6.5.0'
```

**Required Pipeline Variables:**

| Variable | Type | Description |
|----------|------|-------------|
| `RSGO_URL` | Secret | ReadyStackGo server URL (e.g., `https://rsgo.example.com`) |
| `RSGO_API_KEY` | Secret | API key with the required permissions |
| `STACK_NAME` | Normal | Name of the stack to deploy (only for inline calls) |

---

## Security

- API Keys are stored as **SHA-256 hashes** in the database
- Keys use the format `rsgo_` + 32 alphanumeric characters (~190 bits of entropy)
- Keys can be revoked at any time (Settings → CI/CD → Revoke)
- Optional: Environment scope restricts the key to a specific environment
- Expiration date can be configured per key
