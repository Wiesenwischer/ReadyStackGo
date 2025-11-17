# Quick Start

Diese Anleitung führt Sie durch die schnellste Methode, um ReadyStackGo zu starten.

## Voraussetzungen

Stellen Sie sicher, dass Docker installiert ist:

```bash
docker --version
# Docker version 20.10.0 oder höher
```

## Schritt 1: Admin-Container starten

Starten Sie den ReadyStackGo Admin-Container:

```bash
docker run -d \
  --name readystackgo-admin \
  -p 8443:8443 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v rsgo-config:/app/config \
  --restart unless-stopped \
  your-registry/readystackgo/admin:0.1.0
```

### Parameter-Erklärung

- `-p 8443:8443` - HTTPS-Port für die Web-UI
- `-v /var/run/docker.sock:/var/run/docker.sock` - Docker-Socket-Zugriff
- `-v rsgo-config:/app/config` - Persistente Konfiguration
- `--restart unless-stopped` - Automatischer Neustart

## Schritt 2: Web-UI öffnen

Öffnen Sie im Browser:

```
https://<ihre-server-ip>:8443
```

**Hinweis**: Beim ersten Start wird ein Self-Signed-Zertifikat verwendet. Akzeptieren Sie die Sicherheitswarnung im Browser.

## Schritt 3: Setup-Wizard durchlaufen

Der Wizard führt Sie durch 4 Schritte:

### 1. Admin-Benutzer anlegen
- **Benutzername**: z.B. `admin`
- **Passwort**: Sicheres Passwort wählen

### 2. Organisation definieren
- **ID**: Technische ID (z.B. `kunde-a`)
- **Name**: Anzeigename (z.B. `Kunde A GmbH`)

### 3. Verbindungen konfigurieren (Simple Mode)
- **Transport**: Connection String für Message Queue
- **Persistence**: Datenbank-Connection String
- **EventStore**: EventStore-Connection String (optional)

Beispiele:
```
Transport: amqp://rabbitmq:5672
Persistence: Server=sqlserver;Database=ams;User=sa;Password=***
EventStore: esdb://eventstore:2113
```

### 4. Stack installieren
- Manifest auswählen (z.B. `v4.3.0`)
- Installation starten
- Warten bis alle Container gestartet sind

## Schritt 4: Anmelden

Nach erfolgreicher Installation:

1. Wizard wird beendet
2. Login-Seite erscheint
3. Mit Admin-Credentials anmelden

## Fertig!

Sie können nun:

- ✅ Container-Status einsehen
- ✅ Feature Flags verwalten
- ✅ TLS-Zertifikat hochladen
- ✅ Updates durchführen

## Nächste Schritte

- [Installation (detailliert)](Installation.md)
- [Wizard Flow](../Setup-Wizard/Wizard-Flow.md)
- [Configuration](../Configuration/Config-Files.md)

## Troubleshooting

### Container startet nicht
```bash
docker logs readystackgo-admin
```

### Port bereits belegt
Ändern Sie den Port-Mapping:
```bash
-p 9443:8443
```

### Docker Socket Permission denied
Stellen Sie sicher, dass der Docker-Socket zugänglich ist:
```bash
sudo chmod 666 /var/run/docker.sock
```

Weitere Hilfe: [Troubleshooting](../Operations/Troubleshooting.md)
