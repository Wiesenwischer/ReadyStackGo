---
title: Self-Update
description: ReadyStackGo per Knopfdruck aktualisieren — direkt aus der Web-Oberfläche
---

ReadyStackGo kann sich selbst aktualisieren, ohne dass manuell auf dem Server `docker compose pull` ausgeführt werden muss. Sobald eine neue Version verfügbar ist, erscheint ein **Update-Banner** in der Sidebar. Ein Klick auf **Update now** startet den gesamten Aktualisierungsprozess automatisch.

## Übersicht

| Schritt | Was passiert |
|---------|-------------|
| **Erkennung** | RSGO prüft über die GitHub Releases API, ob eine neuere Version existiert |
| **Pull** | Das neue Docker Image wird automatisch heruntergeladen |
| **Swap** | Ein Helper-Container übernimmt den Austausch des laufenden Containers |
| **Update-Seite** | Während des Neustarts wird eine Wartungsseite auf Port 8080 angezeigt |
| **Fertig** | Der Browser erkennt automatisch, wenn die neue Version läuft, und lädt die Seite neu |

---

## Schritt für Schritt: Update durchführen

### Schritt 1: Update-Banner erkennen

Wenn eine neue Version verfügbar ist, erscheint automatisch ein Banner in der linken Sidebar unterhalb der Navigation. Das Banner zeigt die verfügbare Versionsnummer und zwei Aktionen:

- **Update now** — Startet das Update sofort
- **See what's new** — Öffnet die Release Notes auf GitHub

![Sidebar mit Update-Banner](/images/docs/self-update-01-update-banner.png)

:::tip[Banner ausblenden]
Sie können das Banner mit dem **X**-Button schließen. Es wird erst wieder angezeigt, wenn eine noch neuere Version verfügbar ist.
:::

---

### Schritt 2: Update starten

Klicken Sie auf **Update now**. Das Banner wechselt zum Aktualisierungsstatus mit einem Spinner und der Meldung:

> *Updating to vX.Y.Z... RSGO will restart momentarily.*

![Update wird durchgeführt](/images/docs/self-update-02-updating.png)

Im Hintergrund passiert Folgendes:

1. Das neue Image (`wiesenwischer/readystackgo:X.Y.Z`) wird heruntergeladen
2. Ein neuer Container mit der gleichen Konfiguration wird vorbereitet
3. Ein Helper-Container (`rsgo-updater`) übernimmt den Austausch
4. Der alte Container wird gestoppt und entfernt
5. Der neue Container wird umbenannt und gestartet

---

### Schritt 3: Automatischer Neustart

Während des Container-Austauschs ist RSGO kurzzeitig nicht erreichbar. In dieser Zeit:

- Der **Helper-Container** zeigt eine Wartungsseite mit Auto-Refresh an
- Die Web-Oberfläche **pollt automatisch** alle 3 Sekunden, ob der neue Server bereit ist
- Sobald die neue Version antwortet, wird die Seite **automatisch neu geladen**

Sie müssen nichts weiter tun — warten Sie einfach, bis die Seite sich aktualisiert.

---

## Fehlerbehandlung

Falls das Update fehlschlägt (z.B. weil das Image nicht heruntergeladen werden kann), zeigt RSGO eine Fehlermeldung an:

![Fehler beim Update](/images/docs/self-update-03-error.png)

Mögliche Fehlerursachen:

| Fehler | Ursache | Lösung |
|--------|---------|--------|
| Docker error: unable to pull image | Keine Internetverbindung oder Image nicht gefunden | Netzwerkverbindung prüfen, Version auf Docker Hub verfügbar? |
| Docker error: connection refused | Docker Socket nicht erreichbar | Prüfen ob `/var/run/docker.sock` als Volume gemountet ist |
| Update timeout | Neuer Container startet nicht | Container-Logs prüfen: `docker logs readystackgo` |

:::caution[Voraussetzungen]
Für das Self-Update muss der RSGO Container Zugriff auf den **Docker Socket** haben (`/var/run/docker.sock`). Dies ist in der Standard-Installation bereits konfiguriert.
:::

---

## Technische Details

### Helper-Container

Das Update wird von einem separaten Helper-Container (`wiesenwischer/rsgo-updater`) durchgeführt. Dieser:

- Basiert auf `docker:cli` (Alpine mit Docker CLI)
- Wird mit `--network=host` und `AutoRemove` gestartet
- Stoppt den alten RSGO Container, benennt den neuen um und startet ihn
- Zeigt während des Austauschs eine Wartungsseite an
- Räumt sich nach Abschluss automatisch auf

### Versionserkennung

RSGO erkennt neue Versionen über die **GitHub Releases API**. Die aktuelle Version wird beim Build als Assembly-Metadaten eingebettet. Lokale Entwicklungs-Builds verwenden `0.0.0-dev` als Versionsnummer.
