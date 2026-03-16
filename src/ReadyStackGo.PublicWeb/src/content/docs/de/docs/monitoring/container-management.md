---
title: Container Management
description: Überblick und direkte Steuerung aller Docker-Container in einer Environment — mit Health-Status, drei Ansichtsmodi und Container-Logs.
---

Die Container-Management-Seite zeigt alle Docker-Container einer Environment in Echtzeit. Sie zeigt Health-Status, Stack-Zugehörigkeit und Port-Mappings — und ermöglicht es, Container direkt zu starten, zu stoppen oder zu entfernen sowie ihre Logs live zu streamen.

## Übersicht

| Funktion | Beschreibung |
|----------|-------------|
| **Drei Ansichtsmodi** | Liste, Stack-Grupierung, Produkt-Gruppierung |
| **Health-Status** | Basiert auf RSGO-Health-Monitoring (nicht Docker HEALTHCHECK) |
| **Container-Aktionen** | Start, Stop, Entfernen (für nicht verwaltete Container) |
| **Live-Logs** | Echtzeit-Log-Streaming mit Auto-Scroll |
| **Orphaned Stacks** | Erkennung und Reparatur von verwaisten Stacks |

---

## Ansichtsmodi

### Listenansicht

Die Standardansicht zeigt alle Container in einer flachen Tabelle.

![Container-Management Listenansicht](/images/docs/container-mgmt-01-list.png)

Spalten in der Listenansicht:

| Spalte | Inhalt |
|--------|--------|
| **Container Name** | Name des Containers |
| **Stack** | Zugehöriger Stack-Name (mit „Orphaned"-Badge falls kein Deployment-Eintrag) |
| **Product** | Produktname (falls in einem Product Deployment) |
| **Image** | Docker-Image-Referenz |
| **Status** | Health-Status-Badge (healthy, unhealthy, starting, none) |
| **Port** | Primäres Port-Mapping (öffentlich:intern) |
| **Actions** | Start/Stop, Logs, Entfernen |

---

### Stack-Ansicht

Gruppiert Container nach Docker-Stack-Labels. Ideal um zu sehen, welche Container zu welchem Stack gehören.

![Container-Management Stack-Ansicht](/images/docs/container-mgmt-02-stack-view.png)

Jede Stack-Gruppe zeigt:
- Stack-Name als Header mit der Anzahl laufender Container
- „Orphaned"-Badge falls kein Deployment-Datensatz in der Datenbank existiert
- Für Orphaned Stacks: Buttons **Repair** (Deployment-Eintrag anlegen) und **Remove All**

---

### Produkt-Ansicht

Gruppiert Container nach Products und deren Stacks — ideal für eine strukturierte Übersicht über alle deployten Products.

![Container-Management Produkt-Ansicht](/images/docs/container-mgmt-03-product-view.png)

Die Hierarchie ist: **Product → Stack → Container**. Container ohne Produkt-Zuordnung erscheinen im Abschnitt „Unmanaged".

---

## Health-Status

ReadyStackGo verwendet **eigenes HTTP-Health-Monitoring** statt des nativen Docker HEALTHCHECK. Das verhindert falsche „unhealthy"-Meldungen wenn diagnostische Tools (curl, wget) im Container-Image nicht verfügbar sind.

| Status | Farbe | Bedeutung |
|--------|-------|-----------|
| **healthy** | grün | Container läuft und besteht alle Health-Checks |
| **unhealthy** | rot | Health-Checks schlagen fehl oder Container ist gestoppt |
| **starting** | gelb | Container startet gerade |
| **none** | grau | Kein Health-Check konfiguriert |

Für Container, die von keinem Stack-Deployment überwacht werden, gilt folgende Fallback-Logik:

- `state = running` → **healthy**
- `state = restarting / paused` → **degraded**
- `state = exited / dead` → **unhealthy**

---

## Container-Aktionen

### Start / Stop

Über die Action-Buttons kann ein Container direkt gestartet oder gestoppt werden. Während der Aktion wird ein Spinner angezeigt und der Button ist deaktiviert.

:::note[Verwaltete Container]
Container, die Teil eines Product Deployments sind, können **nicht** direkt über die Container-Management-Seite gestoppt werden. Für diese Container steht in der Deployment-Detailansicht ein eigener „Stop Containers"-Link zur Verfügung.
:::

### Entfernen

Der Entfernen-Button (Papierkorb-Icon) fragt nach einer Bestätigung bevor der Container gelöscht wird. Laufende Container werden forciert entfernt.

:::caution[Datenverlust]
Das Entfernen eines Containers ist nicht rückgängig zu machen. Container-Daten ohne Volume-Bindung gehen verloren.
:::

---

## Container-Logs

Ein Klick auf das Log-Icon öffnet die Log-Ansicht für den gewählten Container.

![Container Logs in Echtzeit](/images/docs/container-mgmt-04-logs.png)

Features der Log-Ansicht:

- **Live-Streaming** via WebSocket — Logs erscheinen in Echtzeit
- **Tail-Auswahl** — 50, 100, 200 oder 500 Zeilen historische Logs laden
- **Auto-Scroll** — folgt automatisch neuen Log-Zeilen; deaktiviert sich beim manuellen Scrollen
- **Scroll-to-Bottom-Button** — erscheint wenn nach oben gescrollt wurde
- **Connection-Indicator** — zeigt ob die WebSocket-Verbindung aktiv ist
- **Stream-Ende-Indikator** — erscheint wenn der Container stoppt

---

## Orphaned Stacks

Ein Stack gilt als „orphaned" (verwaist) wenn Docker-Container mit dem Label `rsgo.stack=<name>` existieren, aber kein entsprechender Deployment-Datensatz in der Datenbank vorhanden ist. Das kann nach manuellen Docker-Operationen oder nach einem Datenverlust auftreten.

Für Orphaned Stacks stehen zwei Aktionen zur Verfügung:

| Aktion | Beschreibung |
|--------|-------------|
| **Repair** | Legt einen neuen Deployment-Datensatz an — Container werden ab sofort durch ReadyStackGo verwaltet |
| **Remove All** | Entfernt alle Container des verwaisten Stacks |

Mit **Repair All** können alle Orphaned Stacks der Environment auf einmal repariert werden.

---

## API-Endpunkte

| Methode | Endpunkt | Beschreibung | Permission |
|---------|----------|-------------|------------|
| `GET` | `/api/containers?environment={envId}` | Alle Container auflisten | `Deployments.Read` |
| `GET` | `/api/containers/context?environment={envId}` | Stack/Produkt-Kontext | `Deployments.Read` |
| `POST` | `/api/containers/{id}/start?environment={envId}` | Container starten | `Deployments.Update` |
| `POST` | `/api/containers/{id}/stop?environment={envId}` | Container stoppen | `Deployments.Update` |
| `DELETE` | `/api/containers/{id}?environment={envId}&force={bool}` | Container entfernen | `Deployments.Update` |
| `GET` | `/api/containers/{id}/logs?environment={envId}&tail={n}` | Log-Stream | `Deployments.Read` |
| `POST` | `/api/containers/orphaned-stacks/{stackName}/repair?environment={envId}` | Orphaned Stack reparieren | `Deployments.Update` |
| `DELETE` | `/api/containers/orphaned-stacks/{stackName}?environment={envId}` | Orphaned Stack entfernen | `Deployments.Update` |
| `POST` | `/api/containers/repair-all-orphaned?environment={envId}` | Alle Orphaned Stacks reparieren | `Deployments.Update` |

Authentifizierung: **JWT Bearer Token** oder **API Key**.

---

## Fehlerbehandlung

| Situation | Verhalten |
|-----------|-----------|
| API nicht erreichbar | Rote Fehlermeldung oben auf der Seite |
| Container nicht gefunden | 404-Antwort, Fehlermeldung in der UI |
| Start/Stop schlägt fehl | Fehlermeldung in der Container-Zeile |
| WebSocket-Verbindung unterbrochen | Reconnect-Indikator in der Logs-Ansicht |
