---
title: SNMP Monitoring
description: ReadyStackGo per SNMP überwachen — Schritt-für-Schritt-Anleitung für Einsteiger, vom ersten Polling bis zur Trap-Auswertung in PRTG.
---

ReadyStackGo bringt einen eingebauten **SNMP-Agenten** mit. Damit kannst du den Zustand all deiner Deployments mit denselben Monitoring-Werkzeugen abfragen, die du wahrscheinlich schon für Switches, Server und USVs nutzt — z. B. **PRTG**, **Zabbix**, **LibreNMS**, **Nagios**, **Checkmk** oder ein klassisches `snmpwalk` an der Kommandozeile.

:::tip[Du bist neu bei SNMP?]
Du brauchst keine SNMP-Erfahrung, um dieser Anleitung zu folgen — die Begriffe werden im Glossar am Ende erklärt. Wenn du dich tiefer einlesen möchtest:

- [SNMP-Grundlagen & Geschichte](/de/docs/monitoring/snmp/grundlagen) — wie SNMP funktioniert, warum es v1/v2c/v3 gibt
- [Sicherheitsmodelle (Community vs. USM)](/de/docs/monitoring/snmp/sicherheit) — wann reicht v2c, wann brauchst du v3
- [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv) — welcher Algorithmus heute noch akzeptabel ist
- [PRTG-Integration mit einem Klick](/de/docs/monitoring/snmp/prtg) — fertiges Device-Template + MIB + Lookups herunterladen
- [PRTG HTTP Sensor (5-Min-Setup)](/de/docs/monitoring/snmp/prtg-http-sensor) — ein PRTG-Sensor, eine URL, keine Server-Installation
:::

## Was kann der ReadyStackGo SNMP-Agent?

| Funktion | Beschreibung |
|----------|--------------|
| **Polling per SNMPv2c und SNMPv3** | Liest System-, Environment-, Product-, Stack- und Service-Werte über klassische SNMP `GET`/`WALK`-Operationen aus |
| **MIB-Datei** | Hand-gepflegte `READYSTACKGO-MIB.txt` — direkt auf der Settings-Seite herunterladbar |
| **OID-Reference-Browser** | Interaktiver Baum in der UI zeigt für die aktuelle Umgebung alle konkreten OIDs (mit Kopier-Button) |
| **SNMPv3 mit USM** | Nutzer-basierte Authentifizierung und Verschlüsselung — MD5/SHA1/SHA-2 plus DES/AES-128/192/256 |
| **Traps** | Drei Notification-Typen werden an konfigurierte Trap-Empfänger geschickt: fehlgeschlagene Deployments, Auto-Finalize, Maintenance-Mode-Wechsel |
| **Hot-Reload** | Konfigurationsänderungen werden ohne Container-Restart wirksam |

---

## Was du am Ende dieser Anleitung erreicht hast

1. SNMP-Agent ist aktiviert und lauscht auf einem UDP-Port deiner Wahl.
2. Du hast einen SNMPv2c-Community-String **oder** einen SNMPv3-Benutzer (oder beides).
3. Du kannst per `snmpwalk` einen Wert aus ReadyStackGo abfragen.
4. Du weißt, wie du die MIB in PRTG (oder ein anderes Tool) einbindest.
5. Du verstehst, was die drei Trap-Typen bedeuten und wie sie aussehen.

---

## Schritt 1: SNMP-Seite öffnen

Logge dich als Admin ein und öffne **Settings** in der Sidebar. Auf der Settings-Übersicht findest du die Kachel **SNMP Monitoring**.

![Settings-Übersicht mit SNMP-Kachel](/images/docs/snmp-01-settings-index.png)

Klick darauf — du landest auf `/settings/snmp`.

---

## Schritt 2: Agent-Konfiguration verstehen

Direkt am Anfang der Seite siehst du den Konfigurations-Block. Das sind die wichtigsten Felder:

![Agent-Konfiguration: Toggle, Adresse, Port, OID, Community, Trap-Empfänger](/images/docs/snmp-02-agent-config.png)

| Feld | Bedeutung | Standard |
|------|-----------|----------|
| **Enabled** | An/Aus-Schalter für den Agenten. Ohne Häkchen läuft kein UDP-Listener. | aus |
| **Listen address** | Auf welcher IP der Agent lauscht. `0.0.0.0` heißt: alle Netzwerk-Interfaces im Container. | `0.0.0.0` |
| **Port** | UDP-Port. Der Agent läuft **container-intern auf 1161** (non-privileged). Wer von außen den Standard-Port 161 möchte, mappt im `docker-compose.override.yml` `161:1161`. | `1161` |
| **Root OID** | Der Präfix unter dem alle ReadyStackGo-Werte hängen. Aktuell mit Platzhalter-PEN `99999` — wird auf die echte IANA-Nummer migriert, sobald sie zugewiesen ist. | `1.3.6.1.4.1.99999.1` |
| **SNMPv2c community** | Passwort-ähnlicher String für v2c-Zugriff. **Leer lassen, um v2c komplett zu deaktivieren** und nur v3 zuzulassen. | leer |
| **Trap receivers** | Komma-, Semikolon- oder Zeilenumbruch-getrennte Liste von Empfängern im Format `host` oder `host:port`. Default-Port ist 162. | leer |

:::note[Warum Port 1161 und nicht 161?]
Port 161 ist der SNMP-Standard, aber er liegt unter 1024 und braucht in Linux/Docker Root-Rechte. Damit der ReadyStackGo-Container ohne erhöhte Rechte starten kann, lauscht der Agent intern auf **1161**. Das ist nur die *interne* Sicht — was *außen* an deinem Host anliegt, entscheidest du im Compose-File.
:::

---

## Schritt 3: Agent aktivieren und Community setzen

Für den ersten Test reicht **SNMPv2c**. Das ist nur lesend, nicht verschlüsselt — also gut für ein internes Management-Netz, aber **nicht** für den freien Internet-Zugriff geeignet.

1. **Enabled**-Toggle anklicken (wird brand-farbig).
2. **SNMPv2c community** ausfüllen — wähle einen langen, zufälligen String. Beispiel: `readonly-demo` (für die Anleitung, in Produktion bitte etwas Generiertes).
3. Auf **Save** klicken.

![Agent aktiviert, Community-String gesetzt](/images/docs/snmp-03-enable-and-community.png)

Du bekommst eine grüne Erfolgsmeldung. Der Agent lädt seine Konfiguration automatisch neu — **kein Container-Restart nötig**.

![Bestätigung "Saved. Agent reloads automatically."](/images/docs/snmp-04-saved-confirmation.png)

---

## Schritt 4: Ersten Wert abfragen

Auf einem Linux/Mac-Host mit installiertem `net-snmp` (Paket `snmp` unter Debian/Ubuntu):

```bash
# Annahme: ReadyStackGo läuft auf rsgo.local und du hast 161:1161 gemappt.
snmpwalk -v 2c -c readonly-demo rsgo.local 1.3.6.1.4.1.99999.1.1
```

Du solltest eine Antwort sehen, die ungefähr so aussieht:

```
SNMPv2-SMI::enterprises.99999.1.1.1.0 = STRING: "0.65.3"
SNMPv2-SMI::enterprises.99999.1.1.2.0 = Counter32: 3214
SNMPv2-SMI::enterprises.99999.1.1.3.0 = INTEGER: 2
SNMPv2-SMI::enterprises.99999.1.1.4.0 = INTEGER: 5
SNMPv2-SMI::enterprises.99999.1.1.5.0 = INTEGER: 1
SNMPv2-SMI::enterprises.99999.1.1.6.0 = STRING: "2026-05-20T15:42:51Z"
```

Das sind die **System-Scalars** — Version, Uptime in Sekunden, Anzahl Environments, Anzahl Sources, DB-Health (`1 = ok`), Build-Timestamp.

:::tip[Was bedeuten die Zahlen?]
Jeder Wert hängt unter einer eindeutigen OID. Die letzten Zahlen sind die *Spalten* in der MIB — `.1.1.1.0` ist `rsgoVersion`, `.1.1.2.0` ist `rsgoUptimeSeconds`, usw. Mit der MIB-Datei (nächster Schritt) zeigt dein Tool stattdessen den symbolischen Namen.
:::

---

## Schritt 5: MIB-Datei herunterladen

Damit dein Monitoring-Tool nicht nur Zahlen-OIDs anzeigt, sondern sprechende Namen wie `rsgoProductStatusText`, brauchst du die **MIB** (Management Information Base).

Auf der SNMP-Settings-Seite findest du den Block **MIB file** mit einem **Download MIB**-Button.

![MIB-Download-Block mit Erklärung](/images/docs/snmp-08-mib-download.png)

Lade `READYSTACKGO-MIB.txt` herunter und importiere sie in dein Tool. Die genaue Anleitung dafür hängt vom Tool ab — siehe weiter unten den PRTG-Abschnitt.

:::note[Was ist eine MIB?]
Stell dir eine MIB wie ein **Telefonbuch** vor: links steht die OID (Nummer), rechts der menschenlesbare Name und der Datentyp. Ohne MIB siehst du `.1.3.6.1.4.1.99999.1.3.1.4.7.5`, mit MIB siehst du `rsgoProductName.7.5 = "ams.project"`.
:::

---

## Schritt 6: OID-Reference im Blick

Direkt unter dem MIB-Download zeigt die UI einen **OID-Reference-Baum** — pro Environment, Product, Stack und Service.

![OID-Reference-Baum mit Environment, Product und Stack](/images/docs/snmp-09-oid-reference.png)

Was du dort tun kannst:

- **Auf den Pfeil klicken**, um den Baum auf-/zuzuklappen.
- **Auf eine OID klicken**, um sie in die Zwischenablage zu kopieren.
- **Status-Badge prüfen** — du siehst direkt, ob der Stack `Running`, `Failed` oder `PartiallyRunning` ist.

So findest du die konkrete OID, mit der du z. B. den Status eines bestimmten Stacks in PRTG abfragen kannst.

---

## Schritt 7: SNMPv3-Benutzer anlegen (empfohlen)

SNMPv2c hat **keine Verschlüsselung**. Sobald dein Monitoring-Verkehr durch unsichere Netze läuft (oder du in der EU/USA mit Compliance-Anforderungen unterwegs bist), nimm **SNMPv3** mit USM.

Auf der Seite gibt es den Block **SNMPv3 users**. Klick auf **Add user**:

![Leeres SNMPv3-User-Formular](/images/docs/snmp-05-v3-form-empty.png)

| Feld | Was eintragen |
|------|---------------|
| **Name** | Der USM-Benutzername (kein OS-User). Beispiel: `docs-monitor` |
| **Auth protocol** | Wähle **SHA-256** (Default). MD5 und SHA-1 sind deprecated — siehe [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv). |
| **Auth passphrase** | Mindestens **8 Zeichen**. Je länger und zufälliger, desto besser. |
| **Priv protocol** | Wähle **AES-128** oder höher. DES vermeiden. |
| **Priv passphrase** | Wieder mindestens 8 Zeichen, anderer Wert als die Auth-Passphrase. |

![SNMPv3-User-Formular mit allen Feldern ausgefüllt](/images/docs/snmp-06-v3-form-filled.png)

Auf **Add user** klicken — der User erscheint in der Liste:

![SNMPv3-User in der Liste](/images/docs/snmp-07-v3-user-added.png)

:::caution[Die Passphrases werden nur einmal angezeigt]
ReadyStackGo speichert die Passphrases **verschlüsselt** in der Datenbank. Auf der Listenansicht siehst du sie **nicht** zurück. Wenn du sie verlierst, lösche den User und lege ihn neu an.
:::

### Mit SNMPv3 abfragen

```bash
snmpwalk -v 3 -u docs-monitor -l authPriv \
  -a SHA-256 -A 'AuthPass12345' \
  -x AES         -X 'PrivPass12345' \
  rsgo.local 1.3.6.1.4.1.99999.1.1
```

Erklärung der Flags:

- `-v 3` — Protokollversion 3
- `-u docs-monitor` — der USM-Name aus der UI
- `-l authPriv` — Security-Level (= Auth **und** Priv). Andere Werte: `noAuthNoPriv`, `authNoPriv`.
- `-a SHA-256 -A '…'` — Auth-Algorithmus + Passphrase
- `-x AES -X '…'` — Priv-Algorithmus + Passphrase

---

## Schritt 8: Traps konfigurieren

Polling ist gut für regelmäßige Abfragen — aber wenn ein Deployment kaputt geht, willst du das **sofort** wissen, nicht erst beim nächsten Poll. Dafür gibt's Traps.

Im Konfigurations-Block trägst du unter **Trap receivers** ein, wohin Traps gehen sollen — z. B. die IP deines PRTG-Servers:

```
prtg.local
10.0.0.42:162
```

Eine Adresse pro Zeile (oder kommagetrennt). Default-Port ist 162.

### Welche Traps gibt es?

| Trap | Wann wird gesendet? | Inhalt |
|------|---------------------|--------|
| **`rsgoTrapProductDeploymentFailed`** | Eine `ProductDeployment.MarkAsFailed`-Domain-Event-Notification feuert | ID, Name, Version, Fehlermeldung |
| **`rsgoTrapProductDeploymentAutoFinalized`** | Ein hängengebliebenes Deployment wird vom Watchdog wieder finalisiert | ID, Name, Version, alter und neuer Status, Anzahl gelungener/fehlgeschlagener Stacks |
| **`rsgoTrapProductMaintenanceModeChanged`** | Ein Produkt wechselt in oder aus **Maintenance** | ID, Name, neue Operation-Mode |

Jede Trap hängt unter `1.3.6.1.4.1.99999.1.6.<N>` (1, 2, 3). Die mitgelieferten **VarBinds** (Felder) hängen unter `.7.1` bis `.7.7`:

| VarBind | OID-Spalte | Typ | Bedeutung |
|---------|------------|-----|-----------|
| `rsgoTrapProductId` | `.7.1` | OctetString | UUID des Product Deployments |
| `rsgoTrapProductName` | `.7.2` | OctetString | Lesbarer Name |
| `rsgoTrapProductVersion` | `.7.3` | OctetString | z. B. `4.0.0-ci` |
| `rsgoTrapStatus` | `.7.4` | Integer | Statuscode (siehe MIB Enum) |
| `rsgoTrapStatusText` | `.7.5` | OctetString | Statustext, z. B. `Failed` |
| `rsgoTrapMessage` | `.7.6` | OctetString | Fehler- oder Begründungstext |
| `rsgoTrapOperationMode` | `.7.7` | OctetString | `Normal` oder `Maintenance` |

:::tip[Wie teste ich, ob Traps ankommen?]
Auf einem Linux-Host mit `net-snmp`:

```bash
snmptrapd -f -Lo -c /etc/snmp/snmptrapd.conf 0.0.0.0:162
```

Dann in ReadyStackGo unter **Trap receivers** die IP deines Test-Hosts eintragen und ein Test-Deployment absichtlich kaputt machen (z. B. ungültige Variable). Du solltest in `snmptrapd` einen Eintrag sehen.
:::

---

## Schritt 9: Beispiel — PRTG-Integration

So bindest du ReadyStackGo als SNMP-Sensor in **PRTG Network Monitor** ein.

### 9.1 MIB importieren

PRTG nutzt einen separaten **MIB-Importer** (`MIB Importer.exe` im PRTG-Programmverzeichnis), der `.mib`-Dateien in `.oidlib`-Dateien für PRTG umwandelt.

1. Lade `READYSTACKGO-MIB.txt` über den Button **Download MIB** herunter.
2. Benenne die Datei in `READYSTACKGO-MIB.mib` um (PRTG erwartet `.mib`).
3. Starte den **Paessler MIB Importer**.
4. **File → Import MIB File…** → `READYSTACKGO-MIB.mib` auswählen.
5. **File → Save for PRTG**, Zielordner ist meist `C:\Program Files (x86)\PRTG Network Monitor\snmplibs\`.
6. PRTG-Probe neu starten oder warten — die OID-Lib wird automatisch geladen.

### 9.2 Sensor anlegen

1. Im PRTG-Gerätebaum: **rechte Maustaste auf dein RSGO-Gerät → Add Sensor**.
2. Suche nach **SNMP Library** und wähle den Sensor-Typ.
3. Wähle die soeben importierte **`READYSTACKGO-MIB.oidlib`** aus.
4. PRTG zeigt dir alle Scalar-OIDs und Tabellen zur Auswahl.

**Empfehlungen für die wichtigsten Sensoren**:

| Sensor | OID-Pfad | Was er misst |
|--------|----------|--------------|
| **System-Health** | `rsgoSystem` → `rsgoDbHealth` | DB ist `1=ok`, alles andere ist ein Problem |
| **Uptime** | `rsgoSystem` → `rsgoUptimeSeconds` | Sekunden seit dem letzten Container-Restart |
| **Environment-Health** | `rsgoEnvironmentTable` | Anzahl gesunde/kranke Stacks pro Environment |
| **Product-Status** | `rsgoProductTable` → `rsgoProductStatus` | Pro Product: Status-Enum (`1=Running`, `4=Failed`, …) |
| **Service running** | `rsgoServiceTable` → `rsgoServiceRunning` | Boolean (`1`/`0`) pro Container |

### 9.3 Traps in PRTG empfangen

1. PRTG-Server-IP unter **Trap receivers** in der ReadyStackGo-UI eintragen.
2. In PRTG einen **SNMP Trap Receiver**-Sensor auf der Probe anlegen (Sensor läuft auf Port 162).
3. Optional: einen **Filter** für die OID `1.3.6.1.4.1.99999.1.6.*` definieren, damit nur ReadyStackGo-Traps zählen.

:::tip[Trap auch im Lookup-Menü]
Wenn der MIB-Import sauber gelaufen ist, sieht PRTG nicht `99999.1.6.1`, sondern `rsgoTrapProductDeploymentFailed` — leichter zu filtern und zu alarmieren.
:::

---

## Fehlerbehandlung

| Symptom | Ursache | Lösung |
|---------|---------|--------|
| `snmpwalk` läuft in Timeout | Port nicht erreichbar / Firewall blockt UDP | Prüfe `docker compose ps` (Port-Mapping korrekt?), `iptables -L`, ICMP zum Host |
| `Authentication failure` bei v3 | Passphrase falsch oder Algorithmus falsch eingestellt | Im UI: User löschen + neu anlegen. Auf Client-Seite Flags `-a` und `-x` mit den UI-Werten abgleichen |
| `noAccess` bei v2c | Falscher Community-String **oder** Community-Feld leer gelassen → v2c deaktiviert | UI prüfen, Community neu setzen, Save |
| OIDs zeigen nur Zahlen, keine Namen | MIB nicht (oder falsch) importiert | MIB neu herunterladen und in das richtige Verzeichnis des Tools legen |
| `Agent is not enabled` im Status-Block | Enabled-Toggle steht aus | Toggle anschalten, Save |
| Trap kommt nicht an | Trap-Receiver-Adresse falsch, Port-Block oder Empfänger-Daemon hört nicht | UDP/162 am Empfänger prüfen (`ss -ulpn | grep 162`), Adresse im UI korrigieren |

---

## Glossar

| Begriff | Erklärung |
|---------|-----------|
| **SNMP** | Simple Network Management Protocol. UDP-basiertes Standard-Protokoll zum Auslesen und Empfangen von Status-Werten aus Netzwerk- und Server-Geräten. |
| **OID** | Object Identifier. Eindeutiger Punkt-getrennter Zahlenpfad wie `1.3.6.1.4.1.99999.1.1.1.0`. Jeder messbare Wert hat seine eigene OID. |
| **MIB** | Management Information Base. Text-Datei, die OIDs auf sprechende Namen, Datentypen und Beschreibungen abbildet. |
| **Trap** | Ein vom Agent ausgehender "Push" — der Agent meldet sich von selbst, sobald etwas passiert (kein Polling nötig). |
| **Community String** | v1/v2c-Passwort. Klartext über das Netz — nur in vertrauten Netzen einsetzen. |
| **USM** | User-based Security Model. SNMPv3-Sicherheitsmodell mit benannten Usern, Auth- und Priv-Algorithmen. |
| **Auth / Priv** | Auth = Echtheits-Prüfung des Pakets (HMAC). Priv = Verschlüsselung des Inhalts. |
| **PEN** | Private Enterprise Number — die OID-Wurzel `1.3.6.1.4.1.<X>`, die jede Firma bei der IANA beantragen kann. ReadyStackGo nutzt aktuell `99999` als Platzhalter. |

---

## Weiterlesen

- [PRTG-Integration mit einem Klick](/de/docs/monitoring/snmp/prtg) — Bundle-Download, Schritt-für-Schritt-Setup
- [PRTG HTTP Sensor (5-Min-Setup)](/de/docs/monitoring/snmp/prtg-http-sensor) — kleinster gemeinsamer Nenner: eine URL, ein Sensor
- [SNMP-Grundlagen & Geschichte](/de/docs/monitoring/snmp/grundlagen) — wie SNMP funktioniert, warum es v1/v2c/v3 gibt
- [Sicherheitsmodelle (Community vs. USM)](/de/docs/monitoring/snmp/sicherheit) — Auswahl-Entscheidung mit Vor- und Nachteilen
- [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv) — welcher Algorithmus heute noch akzeptabel ist
