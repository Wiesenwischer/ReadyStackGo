---
title: Bootstrap Script
description: Automatische Installation mit dem Convenience-Script
---

Der einfachste Weg ist die Installation mit unserem Bootstrap-Script. Dieses Script kümmert sich um alles – auch um die Docker-Installation falls nötig.

## Schnellinstallation

**Standard-Installation (Port 8080):**

```bash
curl -fsSL https://readystackgo.pages.dev/install.sh | sudo bash
```

**Mit alternativem Port:**

```bash
curl -fsSL https://readystackgo.pages.dev/install.sh | sudo bash -s 3000
```

:::note
Docker wird automatisch installiert, falls es nicht vorhanden ist.
:::

---

## Was macht das Script?

Das Bootstrap-Script führt automatisch folgende Schritte aus:

| Schritt | Beschreibung |
|---------|--------------|
| **1. Systemprüfung** | Erkennt deine Linux-Distribution und Paketmanager |
| **2. Docker-Installation** | Installiert Docker falls nicht vorhanden (via `get.docker.com`) |
| **3. Docker-Start** | Startet und aktiviert den Docker-Dienst |
| **4. Verzeichnisstruktur** | Erstellt `/var/readystackgo` für persistente Daten |
| **5. Container-Start** | Lädt und startet den ReadyStackGo-Container |

---

## Erfolgreiche Installation

Nach erfolgreicher Installation zeigt das Script die URL an:

```
[OK] Docker wurde installiert
[OK] ReadyStackGo läuft jetzt!
URL: http://192.168.1.100:8080
```

---

## Installation überprüfen

```bash
# Container-Status anzeigen
docker ps | grep readystackgo

# Container-Logs anzeigen
docker logs readystackgo
```

Erfolgreiche Ausgabe:

```
CONTAINER ID   IMAGE                              STATUS         PORTS                    NAMES
abc123...      ghcr.io/ams/readystackgo:latest    Up 2 minutes   0.0.0.0:8080->8080/tcp   readystackgo
```

---

## Nächste Schritte

Nach der Installation kannst du ReadyStackGo unter `http://<server-ip>:8080` erreichen.

➡️ Weiter zur [Ersteinrichtung](/getting-started/initial-setup/)
