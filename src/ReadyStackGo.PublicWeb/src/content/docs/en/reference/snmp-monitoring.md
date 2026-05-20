---
title: SNMP Monitoring
description: Polling ReadyStackGo deployments via the built-in SNMP agent
---

ReadyStackGo ships with a read-only SNMP agent so external monitoring systems
(Nagios, Zabbix, PRTG, LibreNMS, Icinga, ...) can poll the health and status of
ProductDeployments, individual stacks, container services, and the RSGO server
itself via standard SNMP GET / GETNEXT / WALK.

> **Status (v0.64):** read-only, SNMPv2c, with a placeholder Private
> Enterprise Number (`1.3.6.1.4.1.99999.1`). The IANA PEN application is in
> review; the OID will switch to the assigned PEN before v1.0. SNMPv3 user
> credentials can be configured but RSGO only answers via v2c in this milestone;
> full v3 responses ship in v0.65.

## Enabling the agent

The agent is disabled by default. Enable it via `appsettings.json` (or
environment variables in your `docker-compose.override.yml`):

```json
{
  "Snmp": {
    "Enabled": true,
    "Port": 1161,
    "ListenAddress": "0.0.0.0",
    "RootOid": "1.3.6.1.4.1.99999.1",
    "Community": "your-community-string",
    "V3Users": []
  }
}
```

Restart the container after changing settings — the agent reads them once at
startup.

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

## Limits (v0.64)

- **Read-only.** No SET, no Traps. Both come in later phases.
- **SNMPv2c responses only.** v3 credentials decode incoming requests but
  responses are still v2c; full v3 ships in v0.65.
- **Settings via config only.** The WebUI shows the current configuration but
  does not edit it yet. Changes require a container restart.
