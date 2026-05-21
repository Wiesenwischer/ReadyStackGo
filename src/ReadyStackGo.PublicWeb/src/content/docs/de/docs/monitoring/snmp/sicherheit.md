---
title: SNMP — Sicherheitsmodelle (Community vs. USM)
description: Wann reicht der Community-String, wann brauchst du SNMPv3 mit USM? Entscheidungshilfe mit Vor- und Nachteilen.
---

ReadyStackGo unterstützt zwei Sicherheits-Welten:

- **SNMPv2c** — der klassische Community-String (Klartext-"Passwort").
- **SNMPv3** — das User-based Security Model (USM) mit benannten Usern, Auth- und Priv-Algorithmen.

Diese Seite hilft dir bei der Entscheidung: Welches ist für deine Umgebung das Richtige?

---

## TL;DR — Welches soll ich nehmen?

| Szenario | Empfehlung |
|----------|------------|
| Home-Lab, abgeschottetes Netz, nur du selbst | **v2c** reicht — minimaler Aufwand |
| Kleiner Betrieb, ein Management-VLAN, vertrauenswürdige Admins | **v2c** ok, **v3 nice-to-have** |
| Größerer Betrieb, mehrere Admins, Compliance (PCI-DSS, BSI Grundschutz, ISO 27001) | **v3 mit `authPriv`** |
| Monitoring-Traffic läuft durch unsichere Netze (VPN-Tunnel, Cloud-Strecken) | **v3 mit `authPriv`**, immer |
| Du musst nachweisen, *wer* welche Polls gemacht hat (Audit) | **v3** mit pro-User-Logging im Monitoring-Tool |

Bei Unsicherheit: **v3 mit `authPriv`**. Der Aufwand-Mehrwert-Faktor ist klein.

---

## Community-String (v1/v2c)

### Wie es funktioniert

Der Manager (Monitoring-Tool) schickt im SNMP-Paket einen Community-String mit — ein simples ASCII-Wort wie `readonly-demo`. Der Agent vergleicht das mit seiner Konfiguration. Stimmt es überein, antwortet er; sonst verwirft er das Paket wortlos.

```
┌─────────┐                                ┌─────────┐
│ Manager │ ──GetRequest(community=…) ───> │  Agent  │
│         │ <─── Response(varbinds=…) ──── │         │
└─────────┘                                └─────────┘
```

### Was du **nicht** bekommst

- **Keine Verschlüsselung.** Wer den Netzverkehr mitschneiden kann (Switch-SPAN, kompromittierter Host, Cloud-Provider-LB-Logs) sieht den Community-String und alle abgefragten Werte im Klartext.
- **Keine Integritätsprüfung.** Eine Man-in-the-Middle-Attacke könnte die Antwort manipulieren — du würdest "alles grün" sehen, während die Hütte brennt.
- **Keine Replay-Sicherheit.** Aufgezeichnete Pakete können später erneut eingespielt werden.
- **Keine User-Identität.** Wer hat das Polling getriggert? — Es gibt nur "wer den Community-String kennt".

### Was du **doch** bekommst

- **Einfachheit.** Eine Konfigurationszeile, ein String, fertig.
- **Sehr breite Tool-Unterstützung.** Jedes Monitoring-Tool kann v2c.
- **Niedriger CPU-Aufwand.** Keine HMAC-Berechnung, keine Verschlüsselung. Für 1-Sekunden-Intervalle auf 10.000 Geräten relevant.

### Wann ist v2c "gut genug"?

Wenn **alle** der folgenden Punkte gegeben sind:

- Der Management-Traffic verlässt nie ein vertrauenswürdiges Netz.
- Du oder dein Team kontrolliert *alle* Geräte, die in diesem Netz sind.
- Es gibt keine externen Compliance-Anforderungen, die Verschlüsselung verlangen.
- Du brauchst keine User-Identifikation für Audits.

Das ist bei einem privaten Home-Lab oder in einem dedizierten Management-VLAN realistisch. In den meisten Firmen-Setups inzwischen **nicht** mehr.

---

## SNMPv3 mit USM

### Was USM löst

USM (User-based Security Model) ersetzt den geteilten Community-String durch **benannte User mit getrennten Geheimnissen**:

| Aspekt | v2c-Community | v3-USM |
|--------|---------------|--------|
| Identität | "Wer den String kennt" | Benannter User (`docs-monitor`) |
| Authentifizierung | Klartext-Vergleich | HMAC-basierte Auth-Passphrase |
| Verschlüsselung | Nein | Optional via Priv-Passphrase |
| Replay-Schutz | Nein | Ja (timeliness checks via Engine-Boots/Time) |
| Rotation | "Neues Passwort überall ausrollen" | Pro User unabhängig |

### Die drei Security Levels

Beim v3-Polling gibst du an, welches Level du verwenden willst. ReadyStackGo akzeptiert:

| Level | Auth | Priv | Wann sinnvoll |
|-------|------|------|---------------|
| `noAuthNoPriv` | – | – | (in ReadyStackGo deaktiviert) |
| `authNoPriv` | ✓ | – | Tightly-controlled internes Netz, in dem nur die Integrität, aber nicht die Vertraulichkeit zählt |
| `authPriv` | ✓ | ✓ | **Standard-Empfehlung** — überall, wo Traffic über unsichere Strecken läuft |

### Was der Agent in ReadyStackGo unterstützt

Wenn du in der UI einen V3-User anlegst, kannst du wählen aus:

- **Auth-Algorithmen**: MD5, SHA-1, SHA-256, SHA-384, SHA-512
- **Priv-Algorithmen**: DES, AES-128, AES-192, AES-256

Welche du nehmen solltest, steht in [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv).

### Die Engine-ID — warum sie wichtig ist

Jeder SNMPv3-Agent hat eine eindeutige **Engine-ID**. Die wird mit jedem Auth-/Priv-Schlüssel verrechnet, sodass derselbe Username + dieselbe Passphrase auf **zwei verschiedenen Agents** trotzdem unterschiedliche Schlüssel ergeben.

Praktische Folge: Du kannst nicht einen V3-User-Eintrag von Agent A einfach zu Agent B umziehen — er muss neu angelegt werden. ReadyStackGo erzeugt seine Engine-ID einmalig bei der Aktivierung und persistiert sie in der Datenbank.

:::tip[Engine-ID prüfen]
Die Engine-ID steht in der MIB als `rsgoSnmpEngineId`-Scalar und kann mit `snmpget` ausgelesen werden:

```bash
snmpget -v 2c -c readonly-demo rsgo.local 1.3.6.1.4.1.65846.1.1.<n>.0
```
(`<n>` siehst du in der MIB.)
:::

---

## Migrations-Pfad: von v2c zu v3

Du musst nicht "alles auf einmal" umstellen. Vorgehen:

1. **Parallel-Betrieb starten.** Setze einen Community-String *und* lege einen V3-User an. Beide Wege funktionieren gleichzeitig.
2. **Im Monitoring-Tool den neuen v3-Sensor parallel zum v2c-Sensor laufen lassen.** Vergleiche eine Woche lang die Ergebnisse.
3. **Wenn alles stabil ist: Community-String leeren.** Damit ist v2c deaktiviert, nur noch v3 möglich.

Das dauert in der Praxis 1-2 Wartungsfenster und reißt nichts ab.

---

## Was ReadyStackGo bewusst **nicht** macht

- **Schreibender Zugriff.** Der Agent ist read-only. SET-Requests gibt es nicht. Damit ist die Angriffsfläche minimal — selbst wenn jemand den Community-String hat, kann er nichts ändern.
- **VACM (View-based Access Control).** Da der gesamte Sub-Baum read-only ist und nur ReadyStackGo-Daten enthält, brauchen wir kein pro-User-Sichten-Modell. Wer Zugriff hat, hat Zugriff auf alles.
- **Plaintext-Passwort-Persistenz.** Die V3-Passphrases werden symmetrisch verschlüsselt in der DB abgelegt; sie sind **nicht** in der UI sichtbar nach dem Anlegen.
- **Externe Auth-Backends.** Aktuell keine LDAP/RADIUS-Integration für SNMP-User — alle Auth-Daten liegen in der ReadyStackGo-DB.

---

## Weiterlesen

- [SNMP einrichten in ReadyStackGo](/de/docs/monitoring/snmp) — die praktische Anleitung
- [SNMP — Grundlagen & Geschichte](/de/docs/monitoring/snmp/grundlagen) — wie SNMP überhaupt funktioniert
- [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv) — welcher Algorithmus heute noch akzeptabel ist
