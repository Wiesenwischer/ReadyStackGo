---
title: Docker Compose
description: Manuelle Installation mit Docker Compose
---

Für eine strukturiertere Konfiguration kannst du Docker Compose verwenden. Diese Methode eignet sich besonders, wenn du die Konfiguration versionieren oder erweitern möchtest.

## Voraussetzung

Docker und Docker Compose müssen installiert sein:

```bash
# Docker-Version prüfen
docker --version

# Docker Compose Version prüfen
docker compose version
```

---

## Installation

### Schritt 1: Verzeichnis erstellen

```bash
sudo mkdir -p /opt/readystackgo
cd /opt/readystackgo
```

### Schritt 2: docker-compose.yml erstellen

Erstelle eine Datei `docker-compose.yml` mit folgendem Inhalt:

```yaml
services:
  readystackgo:
    image: ghcr.io/ams/readystackgo:latest
    container_name: readystackgo
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - readystackgo-data:/data

volumes:
  readystackgo-data:
```

### Schritt 3: Container starten

```bash
docker compose up -d
```

---

## Erweiterte Konfiguration

Du kannst die Compose-Datei nach Bedarf erweitern:

```yaml
services:
  readystackgo:
    image: ghcr.io/ams/readystackgo:latest
    container_name: readystackgo
    restart: unless-stopped
    ports:
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - readystackgo-data:/data
    environment:
      - TZ=Europe/Berlin
    deploy:
      resources:
        limits:
          memory: 512M

volumes:
  readystackgo-data:
```

### Konfigurationsoptionen

| Option | Beschreibung |
|--------|--------------|
| `TZ` | Zeitzone für Logs und Zeitstempel |
| `memory: 512M` | Speicherlimit für den Container |
| `ports: "3000:8080"` | Alternativer Port (hier: 3000) |

---

## Container-Management

```bash
# Container starten
docker compose up -d

# Container stoppen
docker compose down

# Logs anzeigen
docker compose logs -f

# Container neustarten
docker compose restart

# Update auf neueste Version
docker compose pull && docker compose up -d
```

---

## Installation überprüfen

```bash
# Container-Status anzeigen
docker compose ps

# Container-Logs anzeigen
docker compose logs readystackgo
```

Erfolgreiche Ausgabe:

```
NAME              IMAGE                              STATUS         PORTS
readystackgo      ghcr.io/ams/readystackgo:latest    Up 2 minutes   0.0.0.0:8080->8080/tcp
```

---

## Nächste Schritte

Nach der Installation kannst du ReadyStackGo unter `http://<server-ip>:8080` erreichen.

➡️ Weiter zur [Ersteinrichtung](/getting-started/initial-setup/)
