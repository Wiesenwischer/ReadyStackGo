---
title: PRTG inline registration — ad-hoc credentials per deployment
description: Like Variant 3 (saved PrtgConnection), but URL and API token live directly on the ProductDeployment instead of in a reusable connection — useful for multi-tenant / customer-hosted PRTG.
---

**Variant 2** is the lightweight sibling of [Variant 3 (PrtgConnection)](/en/docs/monitoring/snmp/prtg-connection): RSGO registers itself as a PRTG device on `Running` and cleans up on remove, but URL + token live **on the deployment** rather than in a central connection.

:::tip[When to pick V2 over V3?]
- **Multi-tenant**: each ProductDeployment goes to a *different* PRTG (e.g. each customer's own).
- **One-off setup**: you don't want the credentials parked in a Settings page because they're only relevant for this deployment.
- **Quick hack**: try it without round-tripping through Settings → PRTG Connections.

Everywhere else, **Variant 3** (saved connection) is the better pick: a single point for credential rotation and reuse.
:::

## How to use it in the UI

On the **Deployment Detail page** (`/deployments/<id>`) the **"PRTG monitoring"** card has two tabs:

- **Saved connection** — picks from Variant 3 connections ([docs](/en/docs/monitoring/snmp/prtg-connection))
- **Inline (ad-hoc)** — this variant

The inline tab on the deployment detail page — fields identical to the [saved-connection variant](/en/docs/monitoring/snmp/prtg-connection), only persisted per deployment instead of centrally:

![Inline tab of the PRTG monitoring card with URL/token/Template-Device-ID/Verify-TLS fields and Save/Clear buttons](/images/docs/prtg-detail-inline-tab.png)

In the inline tab you fill in:

| Field | Value |
|-------|-------|
| **URL** | `https://prtg.example.local` |
| **API token / passhash** | PRTG passhash or API token |
| **Template Device ID** | PRTG device id RSGO clones (empty = auto-register off) |
| **Verify TLS certificate** | Uncheck for self-signed certs |

**Save inline** stores the credentials encrypted on the ProductDeployment. Any saved-connection link is **cleared** at the same time — only **one** PRTG target can be active.

## API

```bash
# Set inline registration
curl -X PUT https://rsgo.local/api/deployments/<deployment-id>/prtg-inline \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{
    "id": "<deployment-id>",
    "url": "https://prtg.example.local",
    "apiToken": "PRTG_TOKEN_123",
    "templateDeviceId": 4221,
    "verifyTls": false
  }'

# Clear (url: null)
curl -X PUT https://rsgo.local/api/deployments/<deployment-id>/prtg-inline \
  -H "X-Api-Key: rsgo_..." \
  -H "Content-Type: application/json" \
  -d '{"id":"<deployment-id>","url":null,"verifyTls":true}'
```

## What exactly happens

Same flow as Variant 3 — only the credential source differs:

| Event | RSGO calls | Source |
|-------|------------|--------|
| `Running` | `duplicateobject.htm` + `pause.htm?action=1` | Inline URL + decrypted token + inline template id |
| `Removed` / `Superseded` | `deleteobject.htm` | Same inline data |

The resolver ([PrtgRegisterDeviceOnCompletedHandler.ResolvePrtgTarget](src/ReadyStackGo.Application/Integrations/Prtg/V3/PrtgLifecycleHandlers.cs)) prefers a saved connection when both are set — in practice that never happens because "Save inline" clears the connection and vice versa.

## Security

- **Token encrypted at rest** with the same `ICredentialEncryptionService` as every other credential in RSGO.
- **Token never in read DTOs** — `HasInlinePrtgApiToken: bool` is the only indicator that one is set.
- **TLS verify per deployment** (handy for customer-PRTG with self-signed certs).
- **Best-effort**: a PRTG outage doesn't break the RSGO deploy, it logs a warning.

## Switching between V2 and V3

You can flip back and forth any time:

- **V2 → V3**: create a connection in Settings → detail page → tab "Saved connection" → pick from dropdown → Save. Clears the inline data.
- **V3 → V2**: detail page → tab "Inline" → fill in URL/token/template → Save inline. Clears the connection link.

Switching does **not** delete the existing PRTG device — that happens on the next `Removed`/`Superseded` event. Delete it manually in PRTG if you want it gone immediately.

## Related

- [Variant 3 — saved PrtgConnection](/en/docs/monitoring/snmp/prtg-connection) — the "proper" path for multiple deployments
- [Variant 4 — HTTP Data Advanced sensor](/en/docs/monitoring/snmp/prtg-http-sensor) — passive: PRTG polls RSGO
- [Variant 1 — Device Template Bundle](/en/docs/monitoring/snmp/prtg) — deep per-stack integration
