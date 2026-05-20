---
title: SNMP — Security Models (Community vs. USM)
description: When is the community string enough? When do you need SNMPv3 USM? A decision guide with pros and cons.
---

ReadyStackGo supports two security worlds:

- **SNMPv2c** — the classic community string (cleartext "password").
- **SNMPv3** — the User-based Security Model (USM) with named users, auth, and priv algorithms.

This page helps you decide: which is right for your environment?

---

## TL;DR — Which should I pick?

| Scenario | Recommendation |
|----------|----------------|
| Home lab, isolated network, just you | **v2c** is fine — least effort |
| Small shop, one management VLAN, trusted admins | **v2c** ok, **v3 nice-to-have** |
| Larger shop, multiple admins, compliance (PCI-DSS, ISO 27001, HIPAA) | **v3 with `authPriv`** |
| Monitoring traffic crosses untrusted networks (VPN tunnels, cloud paths) | **v3 with `authPriv`**, always |
| You must prove *who* triggered which poll (audit) | **v3** with per-user logging in the monitoring tool |

When in doubt: **v3 with `authPriv`**. The effort/value ratio is small.

---

## Community strings (v1/v2c)

### How it works

The manager (monitoring tool) puts a community string into every SNMP packet — a simple ASCII word like `readonly-demo`. The agent compares it against its configuration. If they match, it answers; otherwise it drops the packet silently.

```
┌─────────┐                                ┌─────────┐
│ Manager │ ──GetRequest(community=…) ───> │  Agent  │
│         │ <─── Response(varbinds=…) ──── │         │
└─────────┘                                └─────────┘
```

### What you do **not** get

- **No encryption.** Anyone who can capture the network traffic (switch SPAN, compromised host, cloud LB logs) sees the community string and the polled values in cleartext.
- **No integrity check.** A man-in-the-middle could alter the response — you would see "all green" while the building burns.
- **No replay protection.** Captured packets can be replayed later.
- **No user identity.** Who triggered this poll? — Only "someone who knows the community string".

### What you **do** get

- **Simplicity.** One config line, one string, done.
- **Very broad tool support.** Every monitoring tool can speak v2c.
- **Low CPU overhead.** No HMAC computation, no encryption. Relevant for 1-second polling intervals across 10,000 devices.

### When is v2c "good enough"?

If **all** of the following hold:

- Management traffic never leaves a trusted network.
- You or your team controls *every* device on that network.
- No external compliance requirement mandates encryption.
- You do not need user identification for audits.

A private home lab or a dedicated management VLAN realistically meets these. Most modern corporate setups **do not**.

---

## SNMPv3 with USM

### What USM solves

USM replaces the shared community string with **named users with separate secrets**:

| Aspect | v2c community | v3 USM |
|--------|---------------|--------|
| Identity | "Whoever knows the string" | Named user (`docs-monitor`) |
| Authentication | Cleartext compare | HMAC-based auth passphrase |
| Encryption | No | Optional via priv passphrase |
| Replay protection | No | Yes (timeliness checks via Engine Boots/Time) |
| Rotation | "Roll new password everywhere" | Per-user, independent |

### The three security levels

When you poll with v3 you pick the security level. ReadyStackGo accepts:

| Level | Auth | Priv | When useful |
|-------|------|------|-------------|
| `noAuthNoPriv` | – | – | (disabled in ReadyStackGo) |
| `authNoPriv` | ✓ | – | Tightly-controlled internal network where you need integrity but not confidentiality |
| `authPriv` | ✓ | ✓ | **Default recommendation** — anywhere traffic crosses untrusted paths |

### What the agent supports in ReadyStackGo

When you add a v3 user in the UI, you can choose:

- **Auth algorithms**: MD5, SHA-1, SHA-256, SHA-384, SHA-512
- **Priv algorithms**: DES, AES-128, AES-192, AES-256

Which to pick is covered in [auth and priv algorithms](/en/docs/monitoring/snmp/auth-priv).

### Engine ID — why it matters

Every SNMPv3 agent has a unique **Engine ID**. It is folded into every auth/priv key derivation, so the same username + same passphrase on **two different agents** yields **different** keys.

Practical consequence: you cannot just move a v3 user entry from agent A to agent B — you must create it again. ReadyStackGo generates its engine ID once on activation and persists it in the database.

:::tip[Inspect the engine ID]
The engine ID is exposed in the MIB as `rsgoSnmpEngineId` and can be read via `snmpget`:

```bash
snmpget -v 2c -c readonly-demo rsgo.local 1.3.6.1.4.1.99999.1.1.<n>.0
```
(The exact column number is in the MIB.)
:::

---

## Migration path: v2c → v3

You don't have to flip "everything at once". Approach:

1. **Run in parallel.** Set a community string *and* add a v3 user. Both methods work simultaneously.
2. **In the monitoring tool, run the new v3 sensor alongside the v2c sensor.** Compare results for a week.
3. **When everything is stable, empty the community string.** This disables v2c — only v3 will be accepted.

In practice this takes 1–2 maintenance windows and breaks nothing.

---

## What ReadyStackGo deliberately does **not** do

- **Write access.** The agent is read-only. There are no `SET` requests. So the attack surface is minimal — even if someone gets the community string, they cannot change anything.
- **VACM (View-based Access Control).** Since the whole subtree is read-only and only contains ReadyStackGo data, we don't need per-user views. Whoever has access has access to everything.
- **Plaintext passphrase storage.** v3 passphrases are encrypted symmetrically in the database; they are **not** shown back in the UI after creation.
- **External auth backends.** No LDAP/RADIUS integration for SNMP users today — all auth data lives in the ReadyStackGo database.

---

## Read on

- [Set up SNMP in ReadyStackGo](/en/docs/monitoring/snmp) — the practical guide
- [SNMP basics & history](/en/docs/monitoring/snmp/basics) — how SNMP works
- [Auth and priv algorithms](/en/docs/monitoring/snmp/auth-priv) — which algorithm is still safe today
