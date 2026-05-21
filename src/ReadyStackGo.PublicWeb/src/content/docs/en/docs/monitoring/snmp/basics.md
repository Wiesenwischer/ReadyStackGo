---
title: SNMP — Basics & History
description: How SNMP works, why v1, v2c, and v3 exist — and what the differences mean for you.
---

The **Simple Network Management Protocol (SNMP)** is one of the oldest and most widely deployed protocols for monitoring devices in IT networks. If your monitoring tool shows switch utilisation, UPS temperature, or NAS status, chances are SNMP is doing the work behind the scenes.

This page gives you the background **before** you dive into the [SNMP setup guide](/en/docs/monitoring/snmp). It is optional — if you just want to monitor ReadyStackGo quickly, skip straight to the setup.

---

## What problem was SNMP designed for?

In the early '90s corporate networks were exploding in size. Every vendor (Cisco, IBM, Sun, HP) shipped their own management tools — which only worked for their own devices. There was no common standard.

In 1988 the first SNMP appeared as **[RFC 1067](https://datatracker.ietf.org/doc/html/rfc1067)**, followed three years later by the classic version 1 (**RFC 1157**). The design was deliberately minimalist:

- **UDP instead of TCP** — lightweight enough that even very small devices (printers, air conditioners, UPSs) can participate.
- **Pull-based** — the server (the "manager") asks periodically, the device (the "agent") answers. No persistent tunnel.
- **But also push-capable** — when something critical happens, the agent can send an unsolicited **trap** to the manager.
- **Tabular data model** — everything is an OID, and OIDs are organised in trees.

That mix turned SNMP into the lingua franca of infrastructure monitoring. Practically every enterprise monitoring tool speaks SNMP — open-source classics like Nagios and Zabbix, commercial products like PRTG, cloud SaaS offerings.

---

## The three components

| Component | Role | In ReadyStackGo |
|-----------|------|-----------------|
| **Manager** | Sends requests, receives responses and traps. Central dashboard. | Your monitoring tool (PRTG, Zabbix, …) |
| **Agent** | Runs on the monitored device, listens on UDP, sends traps. | The built-in SNMP listener in ReadyStackGo (port 1161). |
| **MIB** | File that describes which OIDs the agent knows and what they mean. | `READYSTACKGO-MIB.txt`, downloadable from the settings page. |

---

## What is an OID?

An **Object Identifier (OID)** is a unique dot-separated numeric path. Like a phone number — but for data points.

Example:

```
1.3.6.1.4.1.65846.1.1.1.0
└─┬──┘ └─┬─┘ └─┬─┘ └─┬─┘ └─┬─┘
  │     │     │     │     │
  │     │     │     │     └── Scalar (.0 = single value, no index)
  │     │     │     └──────── ReadyStackGo subtree (.1 = system scalars)
  │     │     └────────────── ReadyStackGo under PEN 65846
  │     └──────────────────── private.enterprises (4.1)
  └────────────────────────── iso.org.dod.internet (1.3.6.1)
```

Every vendor gets their own PEN (Private Enterprise Number) from IANA and can lay out arbitrary trees underneath. ReadyStackGo currently uses `65846` (IANA-assigned 2026-05-21) — the real PEN was applied for on 2026-05-19 and will land in a future release.

:::tip[Visualise the OID tree]
The [SNMP settings page](/en/docs/monitoring/snmp#step-6-browse-the-oid-reference) shows the full OID tree for your environment interactively and lets you copy any OID to your clipboard.
:::

---

## A history of versions

### SNMPv1 (1988 / 1991)

The original. Authentication was a single **community string** — essentially a shared cleartext "password". Conventional values were `public` (read-only) and `private` (read-write). That was fine in 1988: networks were small, trusted, often physically isolated.

**Problems from today's perspective:**

- The community string travels in **cleartext** — anyone with a sniffer has it.
- No integrity check. An attacker could modify a packet in transit.
- Limited data types (e.g. only 32-bit counters).

ReadyStackGo does **not** support v1. If you still use v1 somewhere, move to v2c or v3.

### SNMPv2c (1996)

The "c" stands for "**c**ommunity-based". v2c kept almost all of v1's security flaws **but** added many useful features:

- **64-bit counters** (`Counter64`) — important for high-bandwidth links that overflow a 32-bit counter in minutes.
- **`GETBULK` operation** — instead of polling every table row individually (`GETNEXT`), the manager can request multiple rows in one packet. Huge performance gain.
- **Better error codes** and new data types.

**When is v2c fine?**

- Trusted, segmented management network.
- No compliance requirements (HIPAA, PCI-DSS, ISO 27001).
- You only need read access (which is the case in ReadyStackGo — the agent is read-only).

**When is it not?**

- Traffic crosses untrusted networks (internet, third-party WAN).
- You need per-user audit ("Who polled this?").
- Compliance mandates encryption of *all* management traffic.

### SNMPv3 (2002, RFC 3411–3418)

v3 is a **completely re-thought security architecture** on top of the proven v2c protocol layer. Three core concepts:

1. **Authentication** (Auth) — the receiver can verify the packet really came from the named user and wasn't modified. HMAC-based.
2. **Privacy** (Priv) — the payload is encrypted so eavesdroppers can't read it.
3. **User-based Security Model (USM)** — authentication per **named user**, not per shared community string. Each user has their own auth and priv passphrases.

There is also the **View-based Access Control Model (VACM)** — defining per user which OID subtrees can be read/written. ReadyStackGo does not use VACM actively because the agent is read-only and limited to a single subtree anyway.

More detail: [security models (community vs. USM)](/en/docs/monitoring/snmp/security) and [auth and priv algorithms](/en/docs/monitoring/snmp/auth-priv).

---

## Operations you should know

| Operation | Who initiates? | Used for |
|-----------|----------------|----------|
| `GET` | Manager → Agent | Read a single value |
| `GETNEXT` | Manager → Agent | Read the next value in OID-tree order — the basis for walks |
| `GETBULK` | Manager → Agent | Multiple `GETNEXT`s in one packet (v2c+) |
| `WALK` | (convention) | Loop over `GETNEXT`/`GETBULK` — download a whole subtree |
| `SET` | Manager → Agent | Write a value — **not** implemented in ReadyStackGo (read-only) |
| `TRAP` / `NOTIFICATION` | Agent → Manager | Event push from agent |
| `INFORM` | Agent → Manager | Like trap but acknowledged |

In practice the most common tool you use is `snmpwalk` (CLI command that internally uses `GETNEXT`/`GETBULK`) and you configure your GUI tool to do periodic `GET`s.

---

## What does an SNMP packet look like?

A typical SNMPv2c `GET` contains:

| Field | Example value |
|-------|---------------|
| Version | `1` (for v2c — the packet uses 0=v1, 1=v2c, 3=v3) |
| Community | `readonly-demo` |
| PDU type | `GetRequest` |
| Request ID | `12345` (uniquely matches request to response) |
| Error status | `0` (zero in requests) |
| VarBind list | `[(1.3.6.1.4.1.65846.1.1.1.0, NULL)]` |

The agent's response is the same shape — except `NULL` is replaced by the real value.

In SNMPv3 there is an additional **SecurityModel header** (user, engine ID, auth params, priv params). With `authPriv` the VarBind data is encrypted.

:::note[What is an Engine ID?]
Every SNMPv3 agent has a unique **Engine ID** (RFC 3411). It is transmitted during the first handshake and folded into all auth and priv key derivations. So even if two agents had the same user with the same passphrase, an attacker could not take a packet from agent A and replay it against agent B — the engine ID is always unique per agent.

ReadyStackGo generates its engine ID **once**, on first activation (RFC 3411 vendor-specific format, `0x80` marker + IANA PEN + random) and persists it in the database.
:::

---

## Read on

- [Set up SNMP in ReadyStackGo](/en/docs/monitoring/snmp) — the practical guide
- [Security models (community vs. USM)](/en/docs/monitoring/snmp/security) — when v2c is enough, when you need v3
- [Auth and priv algorithms](/en/docs/monitoring/snmp/auth-priv) — which algorithm is still safe today
