---
title: PRTG auto-register — reusable PrtgConnection
description: Configure a PRTG connection once in RSGO, link ProductDeployments to it, and RSGO auto-registers them as PRTG devices on deploy and auto-deregisters on remove.
---

A **PrtgConnection** lets RSGO **call PRTG's API** when a ProductDeployment goes live and when it is removed. Unlike [Variant 4 (HTTP Data Advanced sensor)](/en/docs/monitoring/snmp/prtg-http-sensor) and [Variant 1 (Device Template Bundle)](/en/docs/monitoring/snmp/prtg), which are configured on the PRTG side, this variant is **bidirectional**: RSGO knows about PRTG and drives the integration.

:::tip[When is this the right choice?]
- You have many deployments and don't want to add a PRTG sensor for each by hand.
- PRTG is your central monitoring platform and you want every RSGO product to appear there as a device.
- You want a removed product to disappear from PRTG automatically (no sensor graveyard).
:::

## How it works

1. You create a PrtgConnection **once** in `/settings/prtg-connections`: URL + PRTG API token + optionally a **template device id** that RSGO clones per deployment.
2. You link a ProductDeployment to the connection (via API or, soon, on the detail page).
3. When the deployment turns **Running**, RSGO calls the PRTG API:
   - `duplicateobject.htm` → clones the template device, names it `RSGO: <product> (<version>)`, sets the host
   - `pause.htm?action=1` → unpauses the new device so PRTG starts polling
   - The new PRTG device id is stored on the ProductDeployment.
4. On `Removed` or `Superseded`, RSGO deletes the device via `deleteobject.htm`.

All calls are **best-effort**: a PRTG outage does not break the RSGO deploy — the sync operation logs a warning.

## Creating a connection in RSGO

### Step 1: prepare a template device in PRTG

Create a device in PRTG once with all the sensors you want RSGO products to have. Set the sensors to paused so the template itself doesn't poll. Note the device's **Object ID** (visible in the URL: `device.htm?id=4221` → id = `4221`).

Optional: start from the [Variant 1 device-template bundle](/en/docs/monitoring/snmp/prtg) — it ships a ready-made sensor set.

### Step 2: create a PRTG API token

In PRTG: **Setup → System Administration → User Accounts** → your user → reveal passhash (or, from PRTG 23.x onwards, a real API token).

### Step 3: store the connection in RSGO

Settings → **PRTG Connections** → **+ Add connection**:

| Field | Value |
|-------|-------|
| **Name** | `prod-prtg` (free-form, appears in the deploy wizard) |
| **URL** | `https://prtg.example.local` |
| **API token / passhash** | From step 2 |
| **Template Device ID** | `4221` from step 1 — leave empty to disable auto-register |
| **Verify TLS certificate** | Uncheck if PRTG uses a self-signed cert (very common) |

Click **Create**. The token is encrypted in the RSGO database — you will **never** see it in the UI again.

## Linking a ProductDeployment to a connection

The link is currently set via REST (UI on the deployment detail page is coming in a follow-up):

```bash
curl -X PUT https://rsgo.local/api/deployments/<deployment-id>/prtg-connection \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"id":"<deployment-id>","prtgConnectionId":"<connection-id>"}'
```

`prtgConnectionId: null` clears the link.

On the next lifecycle event (`Running` → register, `Removed`/`Superseded` → deregister) RSGO runs the PRTG calls automatically.

## Security

- **API token encrypted** with the same `ICredentialEncryptionService` used for Docker registry credentials and SNMPv3 passphrases.
- **Token leak impact**: read+write access to PRTG. Treat the connection like a registry credential.
- **TLS**: `Verify TLS` is on by default. Uncheck explicitly for self-signed certs — RSGO logs the choice per connection.
- **PRTG permissions**: the PRTG user only needs read+write on the group RSGO writes to. No PRTG admin required.

## Which PRTG endpoints does RSGO call?

| Trigger | RSGO calls | Purpose |
|---------|-----------|---------|
| ProductDeployment → `Running` | `POST /api/duplicateobject.htm?id=<template>&name=<n>&host=<h>` | Clone device from template |
| (right after) | `GET /api/pause.htm?id=<new-id>&action=1` | Unpause; polling starts |
| ProductDeployment → `Removed` / `Superseded` | `POST /api/deleteobject.htm?id=<device-id>&approve=1` | Delete the device |
| (connection liveness probe) | `GET /api/getstatus.json` | Liveness + token validation |

Full PRTG API reference: https://www.paessler.com/manuals/prtg/application_programming_interface_api_definition

## Compared to the other variants

| | Variant 4 (HTTP Sensor) | Variant 1 (Bundle) | **Variant 3 (Connection)** |
|---|---|---|---|
| Setup time | 5 min in PRTG | 10–15 min in PRTG (admin) | 5 min in PRTG + RSGO connection |
| Auto-register new deployments | — | ✓ (auto-discovery, every 60 min) | **✓ instantly on deploy** |
| Auto-deregister on remove | — | — | **✓** |
| RSGO calls PRTG's API | — | — | **✓** |
| Sensor granularity | aggregated channels | per-stack / per-service | from the template device |

Variant 3 is the right pick when auto-cleanup matters or when a CI/CD flow stands up and tears down many deployments.

## Related pages

- [Set up SNMP in ReadyStackGo](/en/docs/monitoring/snmp) — prerequisite
- [Variant 1 — Device Template Bundle](/en/docs/monitoring/snmp/prtg) — manual setup, deep granularity
- [Variant 4 — HTTP Data Advanced sensor](/en/docs/monitoring/snmp/prtg-http-sensor) — fastest, manual setup
