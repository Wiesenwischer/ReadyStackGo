---
title: Docker Run
description: Manuelle Installation mit docker run
---

Falls Docker bereits installiert ist, kannst du ReadyStackGo direkt mit `docker run` starten.

## Voraussetzung

Docker muss bereits installiert und gestartet sein:

```bash
# Docker-Version prüfen
docker --version

# Docker-Dienst starten (falls nötig)
sudo systemctl start docker
```

---

## Installation

### Schritt 1: Datenverzeichnis erstellen

```bash
sudo mkdir -p /var/readystackgo
```

### Schritt 2: Container starten

```bash
docker run -d \
  --name readystackgo \
  --restart unless-stopped \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /var/readystackgo:/data \
  ghcr.io/ams/readystackgo:latest
```

---

## Parameter erklärt

| Parameter | Beschreibung |
|-----------|--------------|
| `-d` | Container im Hintergrund starten |
| `--name readystackgo` | Name des Containers |
| `--restart unless-stopped` | Automatischer Neustart nach System-Reboot |
| `-p 8080:8080` | Port-Mapping (Host:Container) |
| `-v /var/run/docker.sock:...` | Docker Socket für Container-Management |
| `-v /var/readystackgo:/data` | Persistente Daten (Konfiguration, Deployments) |

---

## Alternativer Port

Um einen anderen Port zu verwenden, ändere das Port-Mapping:

```bash
docker run -d \
  --name readystackgo \
  --restart unless-stopped \
  -p 3000:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /var/readystackgo:/data \
  ghcr.io/ams/readystackgo:latest
```

In diesem Beispiel ist ReadyStackGo unter Port `3000` erreichbar.

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
