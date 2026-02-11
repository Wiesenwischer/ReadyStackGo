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

## Creating an API Key

Pipeline access uses **API Keys** instead of JWT tokens.

1. Navigate to **Settings → CI/CD Integration**
2. Click **Create API Key**
3. Choose a descriptive name (e.g., "GitHub Actions Deploy")
4. Select the required permissions:
   - **Redeploy** – Restart the stack with fresh images
   - **Upgrade** – Upgrade the stack to a new catalog version
   - **Sync Sources** – Synchronize catalog sources
5. Optional: Set an expiration date and environment scope
6. **Copy the key immediately** – it will only be shown once!

### Environment Scope

When an API Key is linked to a specific environment, the `environmentId` does not need to be included in every request – it is automatically resolved from the key.

---

## Webhook Endpoints

All endpoints are located under `/api/hooks/` and require API Key authentication via the `X-Api-Key` header.

### Redeploy

`POST /api/hooks/redeploy` – Triggers a redeployment of a running stack with fresh images.

**Request:**
```json
{
  "stackName": "ams-project",
  "environmentId": "optional-if-key-is-environment-scoped"
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

**Permission:** `Hooks.Redeploy`

### Upgrade

`POST /api/hooks/upgrade` – Upgrades a stack to a specific catalog version.

**Request:**
```json
{
  "stackName": "ams-project",
  "targetVersion": "6.5.0",
  "environmentId": "optional",
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

**Permission:** `Hooks.Upgrade`

### Sync Catalog Sources

`POST /api/hooks/sync-sources` – Synchronizes all stack catalog sources.

**Request:** No body required.

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
| 400 | Invalid request (stack not found, invalid version, etc.) |
| 401 | Not authenticated (missing or invalid API key) |
| 403 | Not authorized (API key lacks the required permission) |
| 500 | Server error |

---

## Pipeline Examples

### curl

```bash
# Redeploy (fresh images)
curl -X POST https://rsgo.example.com/api/hooks/redeploy \
  -H "X-Api-Key: rsgo_a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6" \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project"}'

# Sync catalog sources
curl -X POST https://rsgo.example.com/api/hooks/sync-sources \
  -H "X-Api-Key: rsgo_..."

# Upgrade
curl -X POST https://rsgo.example.com/api/hooks/upgrade \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"stackName": "ams-project", "targetVersion": "6.5.0"}'
```

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

```yaml
- script: |
    curl -sf -X POST "$(RSGO_URL)/api/hooks/redeploy" \
      -H "X-Api-Key: $(RSGO_API_KEY)" \
      -H "Content-Type: application/json" \
      -d '{"stackName": "$(STACK_NAME)"}'
  displayName: Trigger Redeploy on ReadyStackGo
```

---

## Security

- API Keys are stored as **SHA-256 hashes** in the database
- Keys use the format `rsgo_` + 32 alphanumeric characters (~190 bits of entropy)
- Keys can be revoked at any time (Settings → CI/CD → Revoke)
- Optional: Environment scope restricts the key to a specific environment
- Expiration date can be configured per key
