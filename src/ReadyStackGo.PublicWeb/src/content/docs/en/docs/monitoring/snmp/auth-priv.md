---
title: SNMP — Auth and Priv Algorithms
description: Which auth and priv algorithms are still acceptable today, which to avoid and why.
---

When you create an [SNMPv3 user in ReadyStackGo](/en/docs/monitoring/snmp#step-7-add-an-snmpv3-user-recommended) you have to pick two algorithms:

- **Auth algorithm** — protects packet authenticity and integrity (HMAC).
- **Priv algorithm** — encrypts the packet payload (cipher).

This page helps you choose. TL;DR first, then details.

---

## TL;DR — Recommendations

| Choice | Recommendation | Why |
|--------|----------------|-----|
| **Auth** | **SHA-256** | Standard, broadly supported, secure. SHA-384/512 only if compliance demands it. |
| **Priv** | **AES-128** | Standard, fast, secure. AES-192/256 only if compliance demands it. |
| **Never** | MD5, SHA-1, DES | Broken or so weakened that they no longer count as secure. |
| **Both passphrases** | at least 16 characters, random | RFC 3414 mandates 8 — that was 1998. Today 16+ is the floor. |

---

## Auth algorithms

The "auth" function in SNMPv3 is an **HMAC** (Hash-based Message Authentication Code). Your passphrase is turned into a **localised key** (RFC 3414 §2.6) — folded together with the agent's engine ID — and that key signs every packet. The receiver recomputes the signature and verifies.

### MD5 — ❌ avoid

- Published 1991 by Ron Rivest.
- Practical collision attacks since 2004 (Wang et al.).
- For HMAC constructions it stayed "good enough" longer because collision resistance is treated differently from pre-image resistance. But:
- Today MD5 is **not** part of any modern security standard. NIST deprecated it in 2008. BSI (Germany) bans it in TR-02102 for new systems.

**Why it still appears:** older SNMP gear (e.g. switches from before 2010) often only supports MD5. ReadyStackGo supports it **only for compatibility** — switch as soon as your manager tools support modern algorithms.

### SHA-1 — ❌ avoid

- Published by NIST in 1995.
- Practical collisions since 2017 (the Shattered attack, Google + CWI).
- HMAC-SHA1 is not yet "broken" in the same sense, but is widely deprecated.
- ReadyStackGo supports it **only for compatibility**.

### SHA-256 — ✅ recommended

- From the SHA-2 family. Standardised in 2001.
- **No** practical attacks known.
- NIST-approved, FIPS-140 compliant, listed as secure in BSI TR-02102.
- Broadly supported in Net-SNMP, Wireshark, all standard tooling.

**Default recommendation for 95 % of cases.**

### SHA-384 / SHA-512 — ✅ ok but overkill

- Larger variant of the same SHA-2 family.
- More CPU, no practically relevant security gain over SHA-256.
- Only useful if your compliance regime explicitly requires it (e.g. NIST Suite B).

---

## Priv algorithms

The "priv" function in SNMPv3 encrypts the **payload** (the VarBinds) — the header stays cleartext so the agent can decrypt the engine ID and user *before* selecting the right keys.

### DES — ❌ avoid

- Data Encryption Standard, 1976. **56-bit key.**
- First practical brute-force attacks in 1998 (EFF DES Cracker, 22 hours).
- Today crackable in **minutes** with modern GPU hardware.
- Was the **only** priv algorithm in the original RFC 3414 — historical baggage.
- ReadyStackGo supports it **only for compatibility**.

### AES-128 — ✅ recommended

- Advanced Encryption Standard, 2001 (RFC 3826 for SNMP).
- 128-bit key, **128-bit block size**.
- Brute force is astronomically infeasible (2¹²⁸ operations).
- Hardware accelerated on every modern CPU (AES-NI).
- Broadly supported in Net-SNMP, Wireshark, all standard tools.

**Default recommendation for 95 % of cases.**

### AES-192 / AES-256 — ✅ ok but rarely practical

- Larger key sizes.
- No practically relevant security gain over AES-128 (probably also not in 30 years).
- **Tooling problem:** some SNMP tools (especially older Net-SNMP versions) do *not* support AES-192/256 because they were never properly standardised — Cisco had its own variant, other implementations followed different RFC drafts.
- Only pick if compliance requires it **and** your manager tool supports it.

---

## Passphrase requirements

RFC 3414 mandates a minimum length of **8 characters**. That was 1998 — far too little today. Key derivation iterates the passphrase through the hash multiple times, but that doesn't protect against weak secrets.

**Recommendations:**

| Requirement | Value |
|-------------|-------|
| **Minimum length** | 8 chars (technical) → **16 chars (practical)** |
| **Recommended length** | 20+ chars, mix of letters/digits/symbols |
| **Auth ≠ Priv passphrase** | Always use different values |
| **Rotation** | On staff changes, otherwise not aggressively — algorithm upgrades matter more than password churn |
| **Storage** | In a password manager. Not in Slack, not in the wiki. |

### Example generation

```bash
# Linux/Mac — 20 random printable-ASCII characters
tr -dc 'A-Za-z0-9!@#$%^&*' </dev/urandom | head -c 20 ; echo

# Or with pwgen
pwgen -s -y 20 1
```

---

## How ReadyStackGo protects passphrases

| Step | What happens |
|------|--------------|
| You type the passphrase in the UI | Sent to the API over HTTPS |
| API receives it | Immediately **encrypted** with a master key (container secret) and stored |
| Agent needs it for the USM key | **Decrypted** on start (or reload) and held in RAM |
| Agent processes a v3 packet | Compute localised key → HMAC / AES |
| You delete the user | The encrypted DB entry is removed |

**What we deliberately do not do:** log the cleartext passphrase, show it back in the UI, or return it via the API. If you lose it, you must recreate the user.

---

## Common problems

### "Authentication failure" — what to check

In order of likelihood:

1. **Wrong auth algorithm** on the client. UI says `Sha256`, you started with `-a SHA1` → fail.
2. **Wrong priv algorithm** on the client. Same story.
3. **Typo in the passphrase.** Common — especially when 20-character strings are typed by hand.
4. **Stale engine ID assumption.** If you cached the engine ID and the agent generated a new one (DB reset).
5. **Time drift.** SNMPv3 checks `EngineBoots` and `EngineTime`. If a client "holds" a packet for too long, the agent treats it as a replay and drops it.

### Net-SNMP flag cheatsheet

```bash
# SHA-256 + AES-128 (default)
snmpwalk -v 3 -u <user> -l authPriv -a SHA-256 -A '<auth-pass>' \
  -x AES -X '<priv-pass>' <host> <oid>

# Auth only, no encryption (rarely useful)
snmpwalk -v 3 -u <user> -l authNoPriv -a SHA-256 -A '<auth-pass>' <host> <oid>

# MD5 for legacy tooling (not recommended!)
snmpwalk -v 3 -u <user> -l authPriv -a MD5 -A '<auth-pass>' \
  -x DES -X '<priv-pass>' <host> <oid>
```

In Net-SNMP, `SHA-256` in the command line corresponds to `usmHMAC192SHA256AuthProtocol` in the MIB — same thing, different spelling.

---

## Read on

- [Set up SNMP in ReadyStackGo](/en/docs/monitoring/snmp) — the practical guide
- [SNMP basics & history](/en/docs/monitoring/snmp/basics) — how SNMP works
- [Security models (community vs. USM)](/en/docs/monitoring/snmp/security) — when v2c is enough, when you need v3
