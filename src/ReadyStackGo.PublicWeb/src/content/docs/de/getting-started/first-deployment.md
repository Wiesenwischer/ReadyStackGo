---
title: Ersten Stack deployen
description: Deinen ersten Container-Stack mit ReadyStackGo deployen
---

Nach der [Ersteinrichtung](/getting-started/initial-setup/) bist du bereit, deinen ersten Stack zu deployen. Diese Anleitung zeigt dir Schritt für Schritt, wie du Container-Stacks mit ReadyStackGo verwaltest.

## Voraussetzungen

Bevor du einen Stack deployen kannst, stelle sicher, dass:

- ✓ Du den Setup-Wizard und das Onboarding abgeschlossen hast
- ✓ Mindestens ein Environment konfiguriert ist
- ✓ Du als Admin eingeloggt bist

:::note[Kein Environment?]
Falls du den Environment-Schritt im Onboarding übersprungen hast, gehe zu **Environments** und füge ein Docker-Environment hinzu.
:::

---

## Stack Management öffnen

1. Melde dich bei ReadyStackGo an
2. Navigiere zu **Stacks** im Seitenmenü
3. Du siehst zwei Bereiche:
   - **Available Stacks** – Vordefinierte Stack-Templates
   - **Deployed Stacks** – Aktuell laufende Deployments

---

## Deployment-Optionen

ReadyStackGo bietet zwei Wege, um Stacks zu deployen:

### Option A: Template deployen

Wenn Stack-Quellen konfiguriert sind, werden verfügbare Templates im Bereich **Available Stacks** angezeigt.

1. **Stack finden** – Suche den gewünschten Stack in der Template-Liste
2. **Deploy klicken** – Klicke auf den **Deploy**-Button beim Stack
3. **Konfiguration anpassen** – Ein Modal öffnet sich mit den Stack-Einstellungen
4. **Variablen setzen** – Fülle erforderliche Umgebungsvariablen aus
5. **Deployment starten** – Klicke auf **Deploy**

### Option B: Custom Compose deployen

Du kannst auch eigene `docker-compose.yml` Dateien deployen:

1. Klicke auf **Deploy Custom** oben rechts
2. Das Deployment-Modal öffnet sich
3. Gib einen **Stack Name** ein
4. Füge deine **docker-compose.yml** ein oder bearbeite sie
5. Definiere optionale **Umgebungsvariablen**
6. Klicke auf **Deploy**

---

## Beispiel: Nginx deployen

Hier ein einfaches Beispiel, um einen Nginx-Webserver zu deployen:

### Schritt 1: Deploy Custom öffnen

Klicke auf den **Deploy Custom** Button in der Stack-Übersicht.

### Schritt 2: Stack konfigurieren

**Stack Name:**
```
nginx-demo
```

**Compose-Definition:**
```yaml
services:
  web:
    image: nginx:alpine
    ports:
      - "8081:80"
    restart: unless-stopped
```

### Schritt 3: Deployen

Klicke auf **Deploy** und warte, bis der Stack gestartet ist.

### Schritt 4: Überprüfen

- Der Stack erscheint in der **Deployed Stacks** Liste
- Status sollte **Running** zeigen
- Öffne `http://<server-ip>:8081` im Browser

---

## Stack-Details

### Status-Anzeigen

| Status | Bedeutung |
|--------|-----------|
| 🟢 **Running** | Alle Container laufen erfolgreich |
| 🔵 **Deploying** | Stack wird gerade deployed |
| 🟡 **Stopped** | Container sind gestoppt |
| 🔴 **Failed** | Deployment oder Container fehlgeschlagen |

### Stack-Informationen

Jeder deployete Stack zeigt:

- **Stack Name** – Der Name des Deployments
- **Version** – Version des Stack-Templates (falls verfügbar)
- **Services** – Anzahl der Container im Stack
- **Deployed At** – Zeitpunkt des Deployments
- **Status** – Aktueller Status

---

## Stack verwalten

### Stack entfernen

1. Finde den Stack in der **Deployed Stacks** Liste
2. Klicke auf **Remove**
3. Bestätige die Aktion

:::caution[Warnung]
Das Entfernen eines Stacks stoppt und löscht alle zugehörigen Container. Volumes bleiben standardmäßig erhalten.
:::

### Stack aktualisieren

Um einen Stack mit neuer Konfiguration zu deployen:

1. Entferne den bestehenden Stack
2. Deploye mit der aktualisierten Konfiguration

---

## Stack Sources synchronisieren

Falls du Stack-Templates aus Git-Repositories nutzt:

1. Klicke auf **Sync Sources** in der Stack-Übersicht
2. ReadyStackGo lädt die neuesten Definitionen
3. Neue oder aktualisierte Templates erscheinen in **Available Stacks**

---

## Beispiele für Produktiv-Stacks

### WordPress mit MySQL

```yaml
services:
  wordpress:
    image: wordpress:latest
    ports:
      - "8082:80"
    environment:
      WORDPRESS_DB_HOST: db
      WORDPRESS_DB_USER: wordpress
      WORDPRESS_DB_PASSWORD: ${DB_PASSWORD}
      WORDPRESS_DB_NAME: wordpress
    volumes:
      - wordpress-data:/var/www/html
    depends_on:
      - db
    restart: unless-stopped

  db:
    image: mysql:8.0
    environment:
      MYSQL_DATABASE: wordpress
      MYSQL_USER: wordpress
      MYSQL_PASSWORD: ${DB_PASSWORD}
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
    volumes:
      - db-data:/var/lib/mysql
    restart: unless-stopped

volumes:
  wordpress-data:
  db-data:
```

**Umgebungsvariablen:**
- `DB_PASSWORD`: Passwort für WordPress-Datenbankbenutzer
- `DB_ROOT_PASSWORD`: MySQL Root-Passwort

### Portainer Agent

```yaml
services:
  agent:
    image: portainer/agent:latest
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - /var/lib/docker/volumes:/var/lib/docker/volumes
    restart: unless-stopped
```

---

## Troubleshooting

### Stack startet nicht

1. **Logs prüfen** – Container-Logs im Docker-Host anzeigen:
   ```bash
   docker logs <container-name>
   ```

2. **Port-Konflikte** – Stelle sicher, dass die Ports nicht belegt sind:
   ```bash
   sudo netstat -tlpn | grep <port>
   ```

3. **Image-Probleme** – Prüfe, ob das Image verfügbar ist:
   ```bash
   docker pull <image-name>
   ```

### Environment-Verbindung fehlgeschlagen

- Prüfe, ob der Docker-Socket korrekt gemountet ist
- Verifiziere, dass Docker läuft: `docker info`
- Überprüfe die Berechtigungen auf den Socket

---

## Nächste Schritte

- Lerne mehr über [Stack Templates](/docs/templates/)
- Konfiguriere [Stack Sources](/docs/configuration/stack-sources/) für Git-Integration
- Richte [Backups](/docs/backups/) ein
