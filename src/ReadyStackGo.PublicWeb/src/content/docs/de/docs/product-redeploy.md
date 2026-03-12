---
title: Product Redeploy
description: Anleitung zum erneuten Deployment eines Produkts mit Echtzeit-Fortschrittsanzeige pro Stack
---

Ein Redeploy startet alle Stacks eines Produkts neu — mit einem frischen Image-Pull bei gleicher Version und Konfiguration. Die Split-View-Oberfläche zeigt den Fortschritt jedes Stacks in Echtzeit mit detaillierten Phase-, Service- und Init-Container-Informationen.

## Wann ein Redeploy sinnvoll ist

| Anwendungsfall | Beschreibung |
|----------------|--------------|
| **Images aktualisiert** | Container-Images wurden neu gebaut (z.B. `:latest` Tag), ohne Versionsänderung |
| **Konfiguration neu anwenden** | Umgebungsvariablen oder Mounts sollen ohne Versionsupgrade neu eingelesen werden |
| **Container-Fehler beheben** | Container im Zustand *Crashed* oder *Unhealthy* nach einem Problem neu starten |
| **Init-Container** | Datenbankmigrationen oder Setup-Container erneut ausführen |

:::note[Redeploy vs. Upgrade]
Ein Redeploy verwendet die **gleiche Version** — es werden keine neuen Variablen aus dem Manifest gelesen.
Für einen Versionswechsel verwenden Sie die [Upgrade-Funktion](/de/docs/stack-upgrade/).
:::

---

## Schritt für Schritt: Produkt Redeploy

### Schritt 1: Produkt-Deployment öffnen

Öffnen Sie die **Deployments**-Seite und wählen Sie das gewünschte Produkt aus der Liste. Auf der Detailseite des Produkt-Deployments finden Sie den **Redeploy**-Button für Produkte im Status *Running*.

![Deployments-Seite mit Produkt-Deployment](/images/docs/product-redeploy-01-deployments.png)

---

### Schritt 2: Redeploy bestätigen

Nach dem Klick auf *Redeploy* erscheint der Bestätigungsbildschirm mit allen relevanten Informationen:

![Redeploy-Bestätigung mit Produktdetails und Stack-Liste](/images/docs/product-redeploy-03-confirm.png)

Der Bestätigungsbildschirm zeigt:

- **Produktname** und aktuelle Version
- **Ziel-Environment**
- **Anzahl der Stacks**
- **Liste aller Stacks** die neu deployt werden, mit Anzahl der Services

Klicken Sie auf **Redeploy All Stacks** um das Redeploy zu starten, oder auf **Cancel** um zur Produktdetailseite zurückzukehren.

:::caution[Image-Pull]
Beim Redeploy werden alle Container-Images neu aus der Registry gezogen.
Stellen Sie sicher, dass die Registry erreichbar ist und die Images verfügbar sind.
:::

---

### Schritt 3: Echtzeit-Fortschrittsanzeige

Sobald das Redeploy startet, wechselt die Ansicht zur **Split-View-Fortschrittsanzeige**:

![Redeploy-Fortschritt mit Split-View und pro-Stack Details](/images/docs/product-redeploy-04-progress.png)

#### Linke Spalte — Stack-Übersicht

Alle Stacks des Produkts werden als klickbare Einträge dargestellt. Jeder Eintrag zeigt:

- **Status-Indikator** (Spinner oder Icon)
- **Stack-Name** in der entsprechenden Farbe
- **Status-Badge** mit aktuellem Zustand

| Status-Badge | Bedeutung |
|-------------|-----------|
| ⚪ **Pending** | Wartet auf Ausführung |
| 🟠 **Removing** | Alter Stack wird entfernt (docker compose down) |
| 🔵 **Deploying** | Stack wird deployt (docker compose up) |
| 🟢 **Running** | Stack erfolgreich deployt |
| 🔴 **Failed** | Stack-Redeploy fehlgeschlagen |

#### Rechte Spalte — Detail-Panel

Je nach Status des ausgewählten Stacks zeigt das Detail-Panel unterschiedliche Informationen:

| Status | Anzeige im Detail-Panel |
|--------|-------------------------|
| Pending | Wartenachricht |
| Removing | Orange Spinner mit "Removing existing stack before redeploy..." |
| Deploying | `DeploymentProgressPanel` mit Phase, Fortschrittsbalken, Service-Zähler, Init-Container-Logs |
| Running | Grüner Erfolgshinweis + letzter bekannter Fortschritt |
| Failed | Roter Fehlerhinweis + letzter bekannter Fortschritt |

#### Gesamtfortschrittsbalken

Am oberen Rand zeigt ein blauer Balken den Gesamtfortschritt: wieviele Stacks abgeschlossen sind von der Gesamtanzahl.

:::tip[Stack auswählen]
Klicken Sie auf einen Stack in der linken Liste, um dessen Details im rechten Panel anzuzeigen.
Das System wählt den aktuell aktiven Stack automatisch aus — Sie können jederzeit manuell wechseln.
:::

---

### Schritt 4: Abschluss

Nach erfolgreichem Redeploy erscheint der Erfolgsbildschirm mit einer Übersicht aller neu deployton Stacks:

![Redeploy erfolgreich abgeschlossen](/images/docs/product-redeploy-05-success.png)

Von hier aus können Sie:

- **View Deployment** — Zum Produkt-Deployment zurückkehren
- **All Deployments** — Zur Deployments-Übersicht navigieren

---

## DeploymentProgressPanel

Das rechte Detail-Panel beim Status *Deploying* zeigt die gleiche Fortschrittsanzeige wie beim initialen Deployment:

| Element | Beschreibung |
|---------|--------------|
| **Phase** | Aktuelle Deployment-Phase (z.B. *Pulling Images*, *Starting Services*) |
| **Fortschrittsbalken** | Prozentualer Fortschritt der aktuellen Phase |
| **Service-Zähler** | Anzahl verarbeiteter vs. gesamter Services |
| **Aktueller Service** | Name des gerade verarbeiteten Services |
| **Init-Container-Logs** | Terminal-Ausgabe von Init-Containern (z.B. Datenbankmigrationen) |

Alle Daten werden via **SignalR** in Echtzeit übertragen — ohne Seitenneuladen.

---

## API-Endpunkt

```http
POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/redeploy
Content-Type: application/json

{
  "sessionId": "optional-signalr-session-id"
}
```

**Antwort:**

```json
{
  "success": true,
  "message": "Product redeployed successfully",
  "stackResults": [
    {
      "stackName": "e2e-platform-backend",
      "stackDisplayName": "Backend",
      "serviceCount": 2,
      "success": true
    }
  ]
}
```

| Parameter | Typ | Pflicht | Beschreibung |
|-----------|-----|---------|--------------|
| `sessionId` | string | Nein | SignalR-Session-ID für Echtzeit-Fortschrittsupdates |

Die `sessionId` ist optional, aber für die Echtzeit-Fortschrittsanzeige in der UI empfohlen. Der Client muss sich vor dem API-Aufruf beim SignalR Deployment Hub mit dieser Session-ID registrieren.

---

## Weiterführende Dokumentation

- [Product Deployment](/de/docs/product-deployment/) — Produkte erstmalig deployen
- [Stack Upgrade](/de/docs/stack-upgrade/) — Stacks auf neue Versionen aktualisieren
- [Stack Deployment](/de/docs/stack-deployment/) — Einzelne Stacks deployen
