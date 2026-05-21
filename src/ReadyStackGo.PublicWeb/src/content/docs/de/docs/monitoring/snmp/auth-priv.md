---
title: SNMP — Auth- und Priv-Algorithmen
description: Welcher Auth- und Priv-Algorithmus heute noch akzeptabel ist, welche du vermeiden solltest und warum.
---

Wenn du in ReadyStackGo einen [SNMPv3-User anlegst](/de/docs/monitoring/snmp#schritt-7-snmpv3-benutzer-anlegen-empfohlen), musst du zwei Algorithmen wählen:

- **Auth-Algorithmus** — sichert die Echtheit und Integrität des Pakets (HMAC).
- **Priv-Algorithmus** — verschlüsselt den Inhalt des Pakets (Cipher).

Diese Seite hilft dir bei der Wahl. Kurzfassung am Anfang, Details darunter.

---

## TL;DR — Empfehlungen

| Wahl | Empfehlung | Begründung |
|------|------------|------------|
| **Auth** | **SHA-256** | Standard, breit unterstützt, sicher. SHA-384/512 nur, wenn Compliance es vorschreibt. |
| **Priv** | **AES-128** | Standard, schnell, sicher. AES-192/256 nur, wenn Compliance es vorschreibt. |
| **Niemals** | MD5, SHA-1, DES | Gebrochen oder so geschwächt, dass sie nicht mehr als "sicher" gelten. |
| **Beide Passphrases** | mindestens 16 Zeichen, zufällig | RFC 3414 schreibt min. 8 vor — das war 1998. Heute sind 16+ Pflicht. |

---

## Auth-Algorithmen

Die "Auth"-Funktion in SNMPv3 ist ein **HMAC** (Hash-based Message Authentication Code). Aus deiner Passphrase wird ein **lokalisierter Schlüssel** abgeleitet (RFC 3414 Section 2.6) — verrechnet mit der Engine-ID des Agents — und damit wird jedes Paket signiert. Der Empfänger berechnet die Signatur erneut und prüft, ob sie übereinstimmt.

### MD5 — ❌ vermeiden

- Veröffentlicht 1991 von Ron Rivest.
- Seit 2004 (Wang et al.) sind praktische Kollisionsangriffe bekannt.
- Für HMAC-Konstruktionen war es lange "noch ok", weil Kollisionen anders gewichtet werden als Pre-Image-Resistenz. Aber:
- Heute hat MD5 in **keinem** modernen Sicherheitsstandard mehr Platz. NIST hat es 2008 deprecated. BSI (Deutschland) verbietet es in TR-02102 für neue Verfahren.

**Wann es trotzdem auftaucht:** ältere SNMP-Geräte (z. B. Switches von vor 2010) konnten oft nur MD5. ReadyStackGo unterstützt es **nur** aus Kompatibilität — schalte um, sobald deine Manager-Tools moderne Algorithmen unterstützen.

### SHA-1 — ❌ vermeiden

- 1995 vom NIST veröffentlicht.
- Praktische Kollisionen seit 2017 (Shattered-Angriff, Google + CWI).
- HMAC-SHA1 ist noch nicht in dem Sinne "gebrochen", aber gilt als deprecated.
- ReadyStackGo unterstützt es **nur** aus Kompatibilität.

### SHA-256 — ✅ empfohlen

- Aus der SHA-2-Familie. Seit 2001.
- Bisher **keine** praktischen Angriffe bekannt.
- NIST-zugelassen, FIPS-140-konform, im BSI TR-02102 als sicher gelistet.
- In Net-SNMP, Wireshark, allen Standard-Tools breit unterstützt.

**Standardempfehlung für 95 % aller Fälle.**

### SHA-384 / SHA-512 — ✅ ok, aber Overkill

- Größere Variante derselben SHA-2-Familie.
- Mehr Rechenaufwand, kein praxisrelevanter Sicherheitsgewinn gegenüber SHA-256.
- Nur sinnvoll, wenn dein Compliance-Regelwerk es explizit fordert (z. B. NIST-Suite-B).

---

## Priv-Algorithmen

Die "Priv"-Funktion in SNMPv3 verschlüsselt den **Payload** (die VarBinds) — der Header bleibt im Klartext, damit der Agent die Engine-ID und den User noch entschlüsseln kann, *bevor* er die richtigen Schlüssel ausgewählt hat.

### DES — ❌ vermeiden

- Data Encryption Standard, 1976. **56-Bit-Schlüssel.**
- Erste praktische Brute-Force-Angriffe ab 1998 (EFF DES Cracker, 22 Stunden).
- Heute knackbar in **Minuten** mit moderner GPU-Hardware.
- War bis SNMPv3 USM (RFC 3414) der **einzige** vorgesehene Priv-Algorithmus — historischer Ballast.
- ReadyStackGo unterstützt es **nur** aus Kompatibilität.

### AES-128 — ✅ empfohlen

- Advanced Encryption Standard, 2001 (RFC 3826 für SNMP).
- 128-Bit-Schlüssel, **128-Bit-Blockgröße**.
- Brute-Force ist astronomisch unrealistisch (2¹²⁸ Operationen).
- Hardware-Beschleunigung in jeder modernen CPU (AES-NI).
- Breit unterstützt von Net-SNMP, Wireshark, allen Standard-Tools.

**Standardempfehlung für 95 % aller Fälle.**

### AES-192 / AES-256 — ✅ ok, aber selten praktikabel

- Größere Schlüssellängen.
- Kein praxisrelevanter Sicherheitsgewinn gegenüber AES-128 (auch in 30 Jahren wahrscheinlich nicht).
- **Tooling-Problem:** Einige SNMP-Tools (vor allem ältere Net-SNMP-Versionen) unterstützen AES-192/256 *nicht*, weil sie nie standardisiert wurden — Cisco hatte eine eigene Variante, andere Implementierungen folgten verschiedenen RFC-Drafts.
- Nur nehmen, wenn Compliance es fordert **und** dein Manager-Tool es unterstützt.

---

## Passphrase-Anforderungen

RFC 3414 schreibt eine Mindestlänge von **8 Zeichen** vor. Das war 1998 — heute viel zu wenig. Die Schlüsselableitung iteriert die Passphrase mehrfach durch den Hash, aber das schützt nicht vor schwachen Geheimnissen.

**Empfehlungen:**

| Anforderung | Wert |
|-------------|------|
| **Mindestlänge** | 8 Zeichen (technisch) → **16 Zeichen (praktisch)** |
| **Empfohlene Länge** | 20+ Zeichen, gemischt aus Buchstaben/Zahlen/Sonderzeichen |
| **Auth- ≠ Priv-Passphrase** | Immer unterschiedliche Werte verwenden |
| **Rotation** | Bei Personalwechsel, sonst nicht zwanghaft — anders als Passwörter wirken Algorithmus-Wechsel viel stärker |
| **Aufbewahrung** | Im Password-Manager. Nicht in Slack, nicht im Wiki. |

### Beispiel-Generierung

```bash
# Linux/Mac — 20 zufällige Zeichen aus dem druckbaren ASCII-Bereich
tr -dc 'A-Za-z0-9!@#$%^&*' </dev/urandom | head -c 20 ; echo

# Oder mit pwgen
pwgen -s -y 20 1
```

---

## Wie ReadyStackGo die Passphrases schützt

| Schritt | Was passiert |
|---------|--------------|
| Du tippst die Passphrase in die UI | Wird per HTTPS an die API geschickt |
| API empfängt sie | Wird sofort **verschlüsselt** mit einem master-Key (Container-Secret) gespeichert |
| Agent braucht sie für den USM-Schlüssel | Wird beim Start (oder Reload) **entschlüsselt** und im RAM gehalten |
| Agent verarbeitet ein v3-Paket | Lokalisierten Schlüssel berechnen → HMAC bzw. AES |
| Du löschst den User | Auch der verschlüsselte DB-Eintrag wird gelöscht |

**Was wir bewusst nicht tun:** Die Klartext-Passphrase irgendwo loggen, in der UI zurückspielen oder per API zurückgeben. Wenn sie verloren geht, muss der User neu angelegt werden.

---

## Häufige Probleme

### "Authentication failure" — was prüfen?

In Reihenfolge der Wahrscheinlichkeit:

1. **Falscher Auth-Algorithmus** im Client. UI sagt `Sha256`, du startest mit `-a SHA1` → fail.
2. **Falscher Priv-Algorithmus** im Client. Selbiges Spiel.
3. **Tippfehler in der Passphrase.** Häufig — gerade bei manuell eingetippten 20-Zeichen-Strings.
4. **Falsche Engine-ID-Wahrnehmung.** Falls du die Engine-ID irgendwo cached hast und der Agent eine neue erzeugt hat (DB-Reset).
5. **Zeit-Drift.** SNMPv3 prüft `EngineBoots` und `EngineTime`. Wenn der Client zu lange ein Paket "halt" und es spät schickt, wird es als Replay verworfen.

### Net-SNMP-Flag-Cheatsheet

```bash
# SHA-256 + AES-128 (Standard)
snmpwalk -v 3 -u <user> -l authPriv -a SHA-256 -A '<auth-pass>' \
  -x AES -X '<priv-pass>' <host> <oid>

# Nur Auth, keine Verschlüsselung (selten sinnvoll)
snmpwalk -v 3 -u <user> -l authNoPriv -a SHA-256 -A '<auth-pass>' <host> <oid>

# Mit MD5 für legacy-Tools (nicht empfohlen!)
snmpwalk -v 3 -u <user> -l authPriv -a MD5 -A '<auth-pass>' \
  -x DES -X '<priv-pass>' <host> <oid>
```

Bei Net-SNMP heißt SHA-256 in den Flags `SHA-256`, in der MIB-Datei `usmHMAC192SHA256AuthProtocol` — das ist dieselbe Sache, nur andere Schreibweise.

---

## Weiterlesen

- [SNMP einrichten in ReadyStackGo](/de/docs/monitoring/snmp) — die praktische Anleitung
- [SNMP — Grundlagen & Geschichte](/de/docs/monitoring/snmp/grundlagen) — wie SNMP funktioniert
- [Sicherheitsmodelle (Community vs. USM)](/de/docs/monitoring/snmp/sicherheit) — wann reicht v2c, wann brauchst du v3
