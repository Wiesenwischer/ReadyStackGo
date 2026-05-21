---
title: SNMP — PRTG Integration mit einem Klick
description: Lade dir aus ReadyStackGo ein fertiges PRTG-Bundle (Device-Template, MIB, Lookups) und stelle dein Monitoring in unter zehn Minuten auf.
---

ReadyStackGo bringt eine fertige PRTG-Integration mit: ein **Bundle** (ZIP) mit Device-Template, MIB und Lookup-Dateien, das du in dein PRTG-Installationsverzeichnis entpackst. Danach übernimmt die normale PRTG-Auto-Discovery den Rest — RSGO selbst muss PRTG nicht kennen, es gibt keine Outbound-Verbindung und keine PRTG-Credentials in RSGO.

:::tip[Was ist mit der SNMP-Grundeinrichtung?]
Diese Seite setzt voraus, dass du SNMP in RSGO schon aktiviert hast. Falls nicht: erst [SNMP einrichten](/de/docs/monitoring/snmp), dann hier weiter.
:::

## Wofür das Bundle gut ist

| Du willst… | Bekommst du mit dem Bundle |
|------------|-----------------------------|
| Auf einen Blick sehen, ob deine Stacks laufen | Eine Sensor-Tabelle "RSGO: Stacks" pro Host, farbcodiert (`Running` grün, `PartiallyRunning` gelb, `Failed` rot) |
| Maintenance erkennen | "Operation Mode"-Spalte mit Status `None` für Wartung — kein roter Alarm bei geplantem Stillstand |
| Pro Service prüfen, ob ein Container hochfährt | Sensor-Tabelle "RSGO: Services" mit `Running`-Flag, `HealthStatus` und `RestartCount` |
| Symbolische OID-Namen statt Zahlen | MIB ist im Bundle dabei, wird über den PRTG MIB Importer übernommen |
| Echtzeitmeldungen bei Deployment-Fehlern | Trap-Receiver konfigurieren (siehe Hauptseite, Schritt 8) — der MIB-Import sorgt dafür, dass PRTG `rsgoTrapProductDeploymentFailed` statt `99999.1.6.1` anzeigt |

## Schritt 1: Bundle herunterladen

Auf der **SNMP-Settings-Seite** (`/settings/snmp`) gibt es einen Block **PRTG integration** mit einem **Download PRTG bundle**-Button:

![PRTG integration card auf der SNMP-Settings-Seite mit Download-Button und 4-Schritt-Anleitung](/images/docs/prtg-v1-bundle-card.png)

Klick darauf — du bekommst eine Datei wie `readystackgo-prtg-bundle-0.66.0.zip`.

Der Inhalt der ZIP entspricht eins-zu-eins der Ordner­struktur, die PRTG erwartet:

```
readystackgo-prtg-bundle/
├── README.txt
├── devicetemplates/
│   └── readystackgo.template       (XML mit deinem aktuellen Root-OID)
├── snmplibs/
│   └── READYSTACKGO-MIB.txt
└── lookups/custom/
    ├── rsgo.productstatus.ovl
    ├── rsgo.stackstatus.ovl
    ├── rsgo.healthstatus.ovl
    ├── rsgo.environmenttype.ovl
    ├── rsgo.servicerunning.ovl
    ├── rsgo.dbhealth.ovl
    └── rsgo.operationmode.ovl
```

:::note[Dynamisch generiert]
Das Bundle wird **bei jedem Download neu erzeugt**. Der Root-OID-Wert wird zur Request-Zeit aus deinen aktuellen SNMP-Settings gelesen. Hast du also später (z. B. nach der IANA-PEN-Zuweisung) den Root-OID umgestellt, lädst du das Bundle einfach erneut herunter — Re-Release von RSGO ist nicht nötig.
:::

## Schritt 2: ZIP ins PRTG-Verzeichnis entpacken

Stoppe den **PRTG Probe**-Dienst (oder nutze später "Reload templates" im PRTG-Web-UI). Entpacke dann den ZIP-Inhalt direkt in:

```
C:\Program Files (x86)\PRTG Network Monitor\
```

Die Ordner aus dem ZIP (`devicetemplates/`, `snmplibs/`, `lookups/custom/`) **mit** existierenden PRTG-Ordnern verschmelzen — bestehende Dateien bleiben unangetastet, neue Dateien kommen dazu.

## Schritt 3: MIB importieren

Starte den **Paessler MIB Importer** (im PRTG-Programmverzeichnis):

1. **File → Import MIB File…** → `snmplibs\READYSTACKGO-MIB.txt`
2. **File → Save for PRTG** (Standard-Zielordner akzeptieren)

Erst dadurch übersetzt PRTG die OIDs in symbolische Namen.

## Schritt 4: Probe starten + Auto-Discovery

1. Starte den **PRTG Probe**-Dienst wieder.
2. Öffne im PRTG-Web-UI den **Device**, der deinen ReadyStackGo-Host darstellt.
3. Setze eine **SNMP-Credential** auf dem Device (Community für v2c oder USM-User für v3 — passt zu dem, was du in RSGO konfiguriert hast).
4. Rechtsklick → **Run Auto-Discovery (with template)**.
5. Wähle im Wizard **ReadyStackGo Deployment**.

Nach ein paar Sekunden tauchen die neuen Sensoren auf:

- **RSGO: System Version** / **Build Timestamp**
- **RSGO: DB Health**, **Environment Count**, **Source Count**
- **RSGO: Environments**, **Product Deployments**, **Stacks**, **Services** (Tabellen-Sensoren mit Status + Counts)

Default-Polling-Intervall ist **60 Sekunden** — kannst du pro Sensor anpassen.

## Was die Lookups bewirken

Die `.ovl`-Dateien im Bundle übersetzen Integer-Status-Werte in Texte **und Sensor-States**:

| Datei | Beispiel-Mapping |
|-------|------------------|
| `rsgo.productstatus.ovl` | `1=Running:Ok`, `2=PartiallyRunning:Warning`, `4=Failed:Error`, `6=Removed:None` |
| `rsgo.stackstatus.ovl` | analog für `StackDeploymentStatus` |
| `rsgo.healthstatus.ovl` | `0=Healthy:Ok`, `2=Unhealthy:Error`, `3=Unknown:Ok` |
| `rsgo.operationmode.ovl` | `0=Normal:Ok`, `1=Maintenance:None` |
| `rsgo.servicerunning.ovl` | `0=stopped:Error`, `1=running:Ok` |
| `rsgo.environmenttype.ovl` | DockerSocket / DockerTcp / DockerAgent / SshTunnel |
| `rsgo.dbhealth.ovl` | `0=unknown:Warning`, `1=ok:Ok`, `2=fail:Error` |

Der zweite Wert pro Eintrag (`Ok` / `Warning` / `Error` / `None`) ist der **PRTG-Sensor-State** — dadurch werden Sensoren automatisch rot/gelb/grün, ohne dass du in PRTG Schwellwerte definierst.

## Updates

Wenn sich am OID-Layout, an einer Enum oder am Root-OID etwas ändert: **lade das Bundle erneut herunter** und entpacke es wieder ins PRTG-Verzeichnis (überschreibt nur die `rsgo.*`-Dateien). Bestehende Sensoren laufen weiter; neue Spalten werden bei der nächsten Auto-Discovery angeboten.

## Fehlerbehandlung

| Symptom | Ursache | Fix |
|---------|---------|-----|
| Sensoren melden "No such name" | OID-Pfad im Template passt nicht zum Agent | Root-OID in RSGO prüfen, Bundle neu downloaden |
| Status wird als Zahl angezeigt (`4` statt `Failed`) | Lookups wurden nicht geladen | `lookups/custom/`-Verzeichnis im PRTG-Programmordner prüfen, Probe neu starten |
| Tabellen-Sensoren zeigen 0 Zeilen | SNMP-Credential auf dem PRTG-Device fehlt oder falsch | In PRTG: Device → Settings → SNMP Credentials anpassen, "Test Sensor" laufen lassen |
| Trap-Sensor empfängt nichts | Trap-Receiver in RSGO nicht gesetzt oder Firewall blockt UDP/162 | RSGO-Settings → Trap receivers, dann `ss -ulpn \| grep 162` auf dem PRTG-Host |

## Verwandte Seiten

- [SNMP einrichten in ReadyStackGo](/de/docs/monitoring/snmp) — Grundkonfiguration des Agenten
- [Sicherheitsmodelle](/de/docs/monitoring/snmp/sicherheit) — wann v2c reicht, wann du v3 brauchst (PRTG kann beides)
- [Auth- und Priv-Algorithmen](/de/docs/monitoring/snmp/auth-priv) — empfohlene Auswahl für die PRTG-Credentials
