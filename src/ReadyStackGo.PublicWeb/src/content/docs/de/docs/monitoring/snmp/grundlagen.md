---
title: SNMP — Grundlagen & Geschichte
description: Wie SNMP funktioniert, warum es v1, v2c und v3 gibt — und was die Unterschiede für dich bedeuten.
---

Das **Simple Network Management Protocol (SNMP)** ist eines der ältesten und am weitesten verbreiteten Protokolle zur Überwachung von Geräten in IT-Netzwerken. Wenn dein Monitoring-Tool die Auslastung deines Switches, die Temperatur einer USV oder den Status deines NAS anzeigt — die Wahrscheinlichkeit ist hoch, dass im Hintergrund SNMP läuft.

Diese Seite gibt dir den Hintergrund, **bevor** du dich in die [SNMP-Einrichtung](/de/docs/monitoring/snmp) stürzt. Sie ist optional — wenn du nur schnell ReadyStackGo überwachen möchtest, kannst du direkt zur Anleitung springen.

---

## Wofür wurde SNMP erfunden?

Anfang der 90er-Jahre wuchsen Firmennetze rasend schnell. Jeder Hersteller (Cisco, IBM, Sun, HP) brachte eigene Verwaltungstools mit — die nur die eigenen Geräte konnten. Ein gemeinsamer Standard fehlte.

1988 entstand mit **[RFC 1067](https://datatracker.ietf.org/doc/html/rfc1067)** das erste SNMP. Drei Jahre später folgte die heute klassische Version 1 (**RFC 1157**). Die Designentscheidungen waren bewusst minimalistisch:

- **UDP statt TCP** — leichtgewichtig, sodass selbst sehr kleine Geräte (Drucker, Klimaanlagen, USVs) mitmachen können.
- **Pull-basiert** — der Server (= "Manager") fragt regelmäßig nach, das Gerät (= "Agent") antwortet. Kein dauerhaft offener Tunnel.
- **Trotzdem Push-fähig** — wenn etwas Wichtiges passiert, kann der Agent eine **Trap** ungefragt ans Management-System schicken.
- **Tabellenartige Datenstruktur** — alles ist eine OID, und OIDs lassen sich in Bäumen organisieren.

Diese Mischung macht SNMP bis heute zur Lingua Franca für Infrastruktur-Monitoring. Praktisch jedes Enterprise-Monitoring-Tool kann SNMP — von Open-Source-Klassikern wie Nagios und Zabbix über kommerzielle Lösungen wie PRTG bis zu Cloud-Anbietern.

---

## Die drei Komponenten

| Komponente | Rolle | In ReadyStackGo |
|------------|-------|-----------------|
| **Manager** | Stellt Anfragen, empfängt Antworten und Traps. Zentrales Dashboard. | Dein Monitoring-Tool (PRTG, Zabbix, …) |
| **Agent** | Läuft auf dem überwachten Gerät, lauscht auf UDP-Anfragen, sendet Traps. | Der eingebaute SNMP-Listener in ReadyStackGo (Port 1161). |
| **MIB** | Datei, die beschreibt, welche OIDs der Agent kennt und was sie bedeuten. | `READYSTACKGO-MIB.txt`, herunterladbar über die Settings-Seite. |

---

## Was ist eine OID?

Eine **Object Identifier (OID)** ist ein eindeutiger Punkt-getrennter Zahlenpfad. Wie eine Telefonnummer — aber für Datenpunkte statt Anschlüsse.

Beispiel:

```
1.3.6.1.4.1.99999.1.1.1.0
└─┬──┘ └─┬─┘ └─┬─┘ └─┬─┘ └─┬─┘
  │     │     │     │     │
  │     │     │     │     └── Skalar (.0 = einzelner Wert, kein Index)
  │     │     │     └──────── ReadyStackGo Sub-Baum (.1 = System-Scalars)
  │     │     └────────────── ReadyStackGo unter PEN 99999
  │     └──────────────────── private.enterprises (4.1)
  └────────────────────────── iso.org.dod.internet (1.3.6.1)
```

Jeder Hersteller bekommt von der **IANA** eine eigene PEN (Private Enterprise Number) und kann darunter beliebige Bäume aufspannen. ReadyStackGo nutzt aktuell `99999` als Platzhalter — die richtige PEN wurde am 2026-05-19 beantragt und kommt in einer der nächsten Versionen.

:::tip[OID-Baum visualisieren]
Auf der [SNMP-Settings-Seite](/de/docs/monitoring/snmp#schritt-6-oid-reference-im-blick) zeigt ReadyStackGo den gesamten OID-Baum deiner Umgebung interaktiv an — du kannst direkt einzelne OIDs in die Zwischenablage kopieren.
:::

---

## Geschichte der Versionen

### SNMPv1 (1988 / 1991)

Die Ur-Version. Authentifizierung erfolgte über einen **Community-String** — quasi ein gemeinsames Passwort im Klartext. Üblich waren `public` (read-only) und `private` (read-write). Das war 1988 völlig akzeptabel: Netze waren klein, vertrauenswürdig, oft physisch isoliert.

**Probleme aus heutiger Sicht:**

- Community-String läuft **unverschlüsselt** über die Leitung — jeder mit einem Sniffer hat ihn.
- Keine Integritätsprüfung. Ein Angreifer könnte ein Paket modifizieren.
- Schwacher Datentyp-Umfang (z. B. nur 32-Bit-Counter).

ReadyStackGo unterstützt v1 **nicht**. Wer noch v1 nutzt, sollte auf v2c oder v3 wechseln.

### SNMPv2c (1996)

"c" steht für "**c**ommunity-based". v2c hat fast alle v1-Sicherheitsschwächen **behalten**, dafür aber viele nützliche Features draufgepackt:

- **64-Bit-Counter** (`Counter64`) — wichtig für hohe Bandbreiten, die einen 32-Bit-Counter binnen Minuten überlaufen lassen würden.
- **`GETBULK`-Operation** — statt jede Zeile einer Tabelle einzeln abzufragen (`GETNEXT`), kann der Manager in einem Paket mehrere Zeilen auf einmal anfordern. Massiver Performance-Gewinn.
- **Bessere Fehlercodes** und neue Datentypen.

**Wann reicht v2c?**

- Vertrauenswürdiges, segmentiertes Management-Netz.
- Keine Compliance-Anforderungen (HIPAA, PCI-DSS, BSI Grundschutz).
- Du brauchst nur Lese-Zugriff (was bei ReadyStackGo der Fall ist — der Agent ist read-only).

**Wann nicht?**

- Verkehr läuft durch unsichere Netze (Internet, fremde WAN-Strecken).
- Du brauchst pro-User-Audit ("Wer hat dieses Polling getriggert?").
- Compliance verlangt Verschlüsselung *aller* Management-Traffic.

### SNMPv3 (2002, RFC 3411–3418)

v3 ist eine **komplett neu gedachte Sicherheitsarchitektur** auf der bewährten v2c-Protokollebene. Drei Kernkonzepte:

1. **Authentication** (Auth) — der Empfänger kann prüfen, dass das Paket wirklich vom angegebenen User stammt und nicht modifiziert wurde. HMAC-basiert.
2. **Privacy** (Priv) — der Inhalt wird verschlüsselt, sodass Mitlauscher nichts mitlesen können.
3. **User-based Security Model (USM)** — Authentifizierung pro **benanntem User**, nicht mehr pro Community-String. Jeder User hat eigene Auth- und Priv-Passphrases.

Daneben gibt es das **View-based Access Control Model (VACM)** — pro User kann definiert werden, welche OID-Teilbäume er lesen/schreiben darf. ReadyStackGo nutzt VACM nicht aktiv, weil der Agent ohnehin read-only und auf einen einzigen Sub-Baum beschränkt ist.

Mehr Details: [Sicherheitsmodelle (Community vs. USM)](/de/docs/monitoring/snmp/sicherheit) und [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv).

---

## Operationen, die du kennen solltest

| Operation | Wer initiiert? | Wofür |
|-----------|----------------|-------|
| `GET` | Manager → Agent | Einen einzelnen Wert abfragen |
| `GETNEXT` | Manager → Agent | Den nächsten Wert in der OID-Baum-Reihenfolge — Basis für Walks |
| `GETBULK` | Manager → Agent | Mehrere `GETNEXT`s in einem Paket (v2c+) |
| `WALK` | (Konvention) | Loop aus `GETNEXT`/`GETBULK` — komplette Teilbäume herunterladen |
| `SET` | Manager → Agent | Einen Wert schreiben — bei ReadyStackGo **nicht** implementiert (read-only) |
| `TRAP` / `NOTIFICATION` | Agent → Manager | Event-Push vom Agent |
| `INFORM` | Agent → Manager | Wie Trap, aber mit Bestätigung |

In der Praxis verwendest du am häufigsten `snmpwalk` (CLI-Befehl, der intern `GETNEXT`/`GETBULK` benutzt) und konfigurierst deine GUI-Tools so, dass sie regelmäßig `GET`-Anfragen schicken.

---

## Wie sieht ein SNMP-Paket aus?

Ein typischer SNMPv2c `GET` enthält:

| Feld | Wert (Beispiel) |
|------|-----------------|
| Version | `1` (für v2c — die Zahlen im Paket sind 0=v1, 1=v2c, 3=v3) |
| Community | `readonly-demo` |
| PDU-Typ | `GetRequest` |
| Request-ID | `12345` (eindeutige Zuordnung Frage/Antwort) |
| Error-Status | `0` (im Request immer 0) |
| VarBind-Liste | `[(1.3.6.1.4.1.99999.1.1.1.0, NULL)]` |

Der Agent antwortet mit denselben Feldern, nur dass `NULL` durch den realen Wert ersetzt ist.

In SNMPv3 kommt ein zusätzlicher **SecurityModel-Header** hinzu (User, Engine-ID, Auth-Parameter, Priv-Parameter). Die VarBind-Daten werden bei `authPriv` verschlüsselt.

:::note[Was ist eine Engine-ID?]
Jeder SNMPv3-Agent hat eine eindeutige **Engine-ID** (RFC 3411). Sie wird beim ersten Handshake an den Manager übertragen und in alle Auth-Berechnungen mit eingerechnet. So kann ein Angreifer auch dann keine v3-Pakete von Agent A nehmen und an Agent B umleiten, wenn beide denselben User-Namen hätten — die Engine-ID ist immer pro Agent eindeutig.

ReadyStackGo erzeugt seine Engine-ID **einmal** bei der ersten Aktivierung (RFC 3411 vendor-specific format, `0x80` Marker + IANA-PEN + Random) und persistiert sie in der Datenbank.
:::

---

## Weiterlesen

- [SNMP einrichten in ReadyStackGo](/de/docs/monitoring/snmp) — die praktische Anleitung
- [Sicherheitsmodelle (Community vs. USM)](/de/docs/monitoring/snmp/sicherheit) — Entscheidungshilfe v2c vs. v3
- [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv) — welcher Algorithmus heute noch akzeptabel ist
