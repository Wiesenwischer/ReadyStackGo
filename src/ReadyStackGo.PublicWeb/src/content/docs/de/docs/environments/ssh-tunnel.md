---
title: SSH Tunnel Environments
description: Remote Docker Server über SSH-Tunnel als Deployment-Ziele anbinden
---

ReadyStackGo kann Docker-Hosts auf entfernten Servern über **SSH-Tunnel** als Deployment-Ziele nutzen. Kein exponierter Docker-Port nötig, kein Agent auf dem Remote-Host – nur SSH-Zugang.

## Übersicht

| Environment-Typ | Verbindung | Anwendungsfall |
|-----------------|-----------|----------------|
| **Docker Socket** | Lokaler Unix-Socket | ReadyStackGo läuft auf demselben Host wie Docker |
| **SSH Tunnel** | SSH → Remote Docker Socket | Docker auf einem entfernten Server, erreichbar per SSH |

:::tip[Warum SSH Tunnel?]
SSH-Tunnel ist der einfachste Weg, Remote-Docker-Hosts anzubinden. Jeder Linux-Server hat SSH — keine zusätzliche Software, keine Firewall-Regeln, keine TLS-Zertifikate nötig.
:::

---

## Schritt für Schritt: SSH Tunnel Environment erstellen

### Schritt 1: Environments öffnen

Navigieren Sie zu **Environments** im Hauptmenü. Hier sehen Sie alle konfigurierten Docker-Environments.

![Environments-Liste](/images/docs/environments/ssh-tunnel-01-environments-list.png)

---

### Schritt 2: Typ auswählen

Klicken Sie auf **Add Environment**. Auf der Erstellungsseite sehen Sie den **Connection Type Selector** mit zwei Optionen:

- **Local Docker Socket** – Direkter Zugriff auf den lokalen Docker-Socket
- **SSH Tunnel** – Verbindung zu einem entfernten Docker-Host über SSH

![Typ-Selektor auf der Add Environment Seite](/images/docs/environments/ssh-tunnel-02-type-selector.png)

Klicken Sie auf **SSH Tunnel**, um das SSH-Formular zu öffnen.

---

### Schritt 3: SSH-Verbindungsdaten eingeben

Das Formular zeigt die SSH-spezifischen Felder:

![SSH Tunnel Formular](/images/docs/environments/ssh-tunnel-03-ssh-form.png)

| Feld | Pflicht | Beschreibung |
|------|---------|-------------|
| **Environment Name** | Ja | Beschreibender Name (z.B. "Production Server") |
| **SSH Host** | Ja | IP-Adresse oder Hostname des Remote-Servers |
| **SSH Port** | Nein | Standard: 22 |
| **SSH Username** | Ja | Benutzername für die SSH-Verbindung (z.B. "root", "deploy") |
| **Authentication Method** | Ja | Private Key (empfohlen) oder Password |
| **Private Key / Password** | Ja | SSH-Schlüssel im PEM-Format oder Passwort |
| **Remote Docker Socket Path** | Nein | Standard: `/var/run/docker.sock` |

---

### Schritt 4: Authentifizierung konfigurieren

Wählen Sie die gewünschte Authentifizierungsmethode:

**Private Key** (empfohlen): Fügen Sie den vollständigen privaten SSH-Schlüssel im PEM-Format ein.

**Password**: Geben Sie das SSH-Passwort ein.

![Passwort-Authentifizierung](/images/docs/environments/ssh-tunnel-04-password-auth.png)

:::caution[Sicherheit]
SSH-Zugangsdaten werden mit **AES-256-CBC** verschlüsselt in der Datenbank gespeichert. Der Master Key kann über die Umgebungsvariable `RSGO_ENCRYPTION_KEY` gesetzt werden. Ohne diese Variable wird automatisch ein Key generiert und in `/data/.encryption-key` gespeichert.
:::

---

### Schritt 5: Verbindung testen

Füllen Sie alle Felder aus und klicken Sie auf **Test Connection**, um die SSH-Verbindung und den Docker-Zugriff zu prüfen.

![Ausgefülltes SSH-Formular](/images/docs/environments/ssh-tunnel-05-filled-form.png)

Der Test:
1. Baut eine SSH-Verbindung auf
2. Erstellt einen TCP-Tunnel zum Docker-Socket
3. Ruft Docker System Info ab
4. Zeigt Docker-Version und Erfolg/Fehler an

![Test Connection Button](/images/docs/environments/ssh-tunnel-06-test-connection.png)

---

### Schritt 6: Environment erstellen

Klicken Sie auf **Create Environment**. Das SSH Tunnel Environment ist sofort einsatzbereit für Deployments, Health Monitoring und Container-Management.

---

## Technische Details

### Wie funktioniert der SSH-Tunnel?

```
RSGO Controller                         Remote Host
┌─────────────────┐    SSH Tunnel       ┌─────────────────┐
│ SshTunnelManager │────Port 22────────→│ sshd             │
│ (SSH.NET)        │                    │                  │
│                  │                    │  socat bridge    │
│ DockerService    │    TCP-Tunnel      │  TCP → Unix      │
│ (Docker.DotNet)  │──localhost:random──→│  docker.sock    │
└─────────────────┘                    └─────────────────┘
```

1. **SSH-Verbindung**: SSH.NET baut eine SSH-Verbindung zum Remote-Host auf
2. **Socat Bridge**: Auf dem Remote-Host startet `socat` eine Brücke von TCP zu Unix-Socket
3. **Port Forwarding**: SSH Local Port Forwarding leitet `localhost:random-port` zum socat-Port weiter
4. **Docker Client**: Docker.DotNet verbindet sich via `tcp://localhost:random-port`

### Voraussetzungen auf dem Remote-Host

- **SSH-Zugang** (Port 22 oder custom)
- **Docker installiert** und laufend
- **socat installiert** (`apt install socat` / `yum install socat`)
- SSH-Benutzer muss Zugriff auf den Docker-Socket haben (Gruppe `docker`)

### Verschlüsselung

SSH-Credentials (Private Keys und Passwords) werden mit AES-256-CBC verschlüsselt gespeichert:

| Konfiguration | Beschreibung |
|--------------|-------------|
| `RSGO_ENCRYPTION_KEY` | Environment Variable für den Master Key |
| `/data/.encryption-key` | Auto-generierter Key (Fallback) |

:::note[Produktion]
Setzen Sie in Produktionsumgebungen immer `RSGO_ENCRYPTION_KEY` als Environment Variable, damit die Verschlüsselung Container-Neustarts überlebt.
:::

---

## Fehlerbehandlung

| Fehler | Ursache | Lösung |
|--------|---------|--------|
| Connection refused | SSH-Server nicht erreichbar | Host/Port prüfen, Firewall checken |
| Authentication failed | Falsche Credentials | Username/Key/Password prüfen |
| socat not available | socat nicht installiert | `apt install socat` auf dem Remote-Host |
| Permission denied on socket | Kein Docker-Socket-Zugriff | User zur Gruppe `docker` hinzufügen |
| Connection timeout | Netzwerk-Problem | SSH-Erreichbarkeit prüfen (`ssh user@host`) |
