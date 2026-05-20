---
title: SNMP Monitoring
description: Polling ReadyStackGo deployments via the built-in SNMP agent
---

ReadyStackGo ships with a read-only SNMP agent so external monitoring systems
(Nagios, Zabbix, PRTG, LibreNMS, Icinga, ...) can poll the health and status of
ProductDeployments, individual stacks, container services, and the RSGO server
itself via standard SNMP GET / GETNEXT / WALK.

> **Status (v0.65):** SNMPv2c read-only polling is fully supported and
> configurable via the WebUI without container restarts. SNMPv3 user
> credentials can be configured; v3 polling decodes correctly but RSGO
> currently answers only via v2c — full v3 responses ship in a follow-up.
> SNMP Traps are scaffolded (the receiver list is editable) but actual
> trap emission also comes in a follow-up. The OID root is the placeholder
> `1.3.6.1.4.1.99999.1`; the IANA PEN application is in review and the OID
> will switch to the assigned PEN before v1.0.

## Enabling the agent

The agent is disabled by default. Enable it in the WebUI:

1. Open **Settings → SNMP Monitoring**
2. Toggle **Enabled** on
3. Set a v2c community string (or leave empty and add SNMPv3 users)
4. Save

Changes apply immediately — the listener rebinds without a container restart.
The configuration is persisted in the RSGO database, so it survives upgrades.

### Port mapping

The container listens internally on **UDP/1161** (non-privileged so the
container can run as a non-root user under OpenShift, K8s `runAsNonRoot`,
rootless Docker, etc.). The default `docker-compose.yml` maps it 1:1 to the
host:

```yaml
ports:
  - "1161:1161/udp"
```

If you want monitoring tools to hit the classic SNMP port `161` instead, add a
`docker-compose.override.yml`:

```yaml
services:
  readystackgo:
    ports:
      - "161:1161/udp"
```

The Docker daemon binds host:161 (it has root); the container process stays
non-privileged.

## Finding the OIDs for your environment

Each environment / product / stack / service is addressed by a stable
hash-derived index. You can either walk the agent to discover them, or open
**Settings → SNMP Monitoring** in the WebUI. The page lists every concrete OID
for your instance with copy-to-clipboard buttons.

The page also links to the MIB file download (`/api/snmp/mib`) — import it
into your monitoring tool so the numeric OIDs resolve to symbolic names like
`RSGO-MIB::rsgoProductStatus`.

## Example snmpwalk

```bash
# Walk the entire RSGO subtree
snmpwalk -v2c -c your-community-string rsgo-host:1161 1.3.6.1.4.1.99999.1

# Get the status of a specific ProductDeployment
# (envIdx and prodIdx come from the Settings → SNMP Monitoring page)
snmpget -v2c -c your-community-string rsgo-host:1161 \
   1.3.6.1.4.1.99999.1.3.1.6.<envIdx>.<prodIdx>
```

## OID layout

| OID | Purpose |
| --- | --- |
| `<root>.1.*.0` | `rsgoSystem` scalars — version, uptime, env count, source count, DB health, build timestamp |
| `<root>.2.1.<col>.<envIdx>` | `rsgoEnvironmentTable` — id, name, type |
| `<root>.3.1.<col>.<envIdx>.<prodIdx>` | `rsgoProductTable` — id, name, version, status, total/running/failed stacks, last deployed at, error message |
| `<root>.4.1.<col>.<envIdx>.<prodIdx>.<stackIdx>` | `rsgoStackTable` — name, status, service count, order, error |
| `<root>.5.1.<col>.<envIdx>.<prodIdx>.<stackIdx>.<svcIdx>` | `rsgoServiceTable` — name, container name, running, health status, restart count, last health check |

See the [MIB file](/en/reference/snmp-monitoring/#download-the-mib-file)
for the exact column numbers and enum mappings.

## Download the MIB file

While the agent is running, the MIB file is reachable at
`/api/snmp/mib` from the WebUI host (also linked from the
**Settings → SNMP Monitoring** page). Import it into your monitoring tool's
MIB browser so symbolic names like `RSGO-MIB::rsgoProductStatus` work.

## Index stability

The indices in the OIDs are deterministic 31-bit positive `Int32` hashes of
the underlying domain IDs (Environment GUID, ProductGroupId, ...). They stay
the same across container restarts and across add/delete operations on other
entities — so once you store an OID in your monitoring config, it keeps
pointing at the same logical object.

## SNMP Traps (push notifications)

RSGO sends v2c traps to every receiver listed in **Trap receivers** on the
Settings page (comma- or newline-separated `host[:port]`, default port `162`)
whenever a notable domain event occurs:

| Trap | Domain event | Carries |
| --- | --- | --- |
| `rsgoProductDeploymentFailedTrap` | A product deployment ended in `Failed`. | Product name, error message |
| `rsgoProductDeploymentAutoFinalizedTrap` | A stuck deployment was auto-finalized by RSGO. | Product name, resulting status, reason |
| `rsgoProductMaintenanceModeChangedTrap` | A product entered or left maintenance mode. | New operation mode, reason |

Traps are only sent when the SNMP agent is enabled and a v2c community is
configured (the community is reused as the trap community).

## Limits (v0.65)

- **Read-only polling.** No SET.
- **SNMPv2c responses only.** SNMPv3 credentials authenticate incoming
  requests correctly, but RSGO currently constructs its responses as v2c —
  full v3 responses come in a follow-up.
