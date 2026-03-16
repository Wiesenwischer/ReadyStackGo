---
title: Container stoppen & neustarten
description: Container eines Product Deployments gezielt stoppen, neustarten oder wiederherstellen — mit Bestätigungsscreen und Echtzeit-Fortschritt.
---

**Container Control** ermöglicht es, die Docker-Container eines Product Deployments gezielt zu stoppen oder neu zu starten — ohne das Deployment zu entfernen. Alle Aktionen durchlaufen einen Bestätigungsscreen und zeigen den Fortschritt in Echtzeit.

## Übersicht

| Aktion | Beschreibung |
|--------|-------------|
| **Stop Containers** | Alle Container des Produkts stoppen (Deployment bleibt erhalten) |
| **Restart Containers** | Gestoppte Container neu starten (Restore-Funktion) |

---

## Schritt für Schritt: Container stoppen

### Schritt 1: Product Deployment Detail

Öffne die Detailseite eines laufenden Product Deployments. Im Status **Running** oder **PartiallyRunning** sind die Links **Stop Containers** und **Restart Containers** sichtbar.

![Product Deployment Detail mit Stop/Restart-Buttons](/images/docs/monitoring/container-control-01-buttons.png)

---

### Schritt 2: Stop-Bestätigung

Ein Klick auf **Stop Containers** öffnet den Bestätigungsscreen:

- **Product Details** — Name, Version, Environment
- **Stacks to stop** — Liste aller Stacks mit Service-Anzahl
- **Cancel** — zurück zur Deployment-Detailseite
- **Stop All Containers** — startet den Stop-Vorgang

![Stop Containers Bestätigungsscreen](/images/docs/monitoring/container-control-02-stop-confirm.png)

:::note[Kein Datenverlust]
Beim Stoppen werden nur die Container angehalten. Das Deployment und alle Konfigurationen bleiben erhalten. Container können jederzeit über **Restart Containers** wieder gestartet werden.
:::

---

### Schritt 3: Stop-Fortschritt

Während des Stoppens zeigt die Seite einen Fortschritts-Spinner.

![Container Stop in progress](/images/docs/monitoring/container-control-03-stop-loading.png)

---

### Schritt 4: Stop-Ergebnis

Nach Abschluss erscheint das Ergebnis mit dem Status jedes Stacks.

![Container Stop Ergebnis](/images/docs/monitoring/container-control-04-stop-result.png)

**Erfolgreich gestoppt:**
- Heading: „Containers Stopped Successfully!"
- Liste aller Stacks mit Ergebnis

**Mit Fehlern:**
- Heading: „Stop Completed with Errors"
- Fehlermeldungen pro Stack

---

### Schritt 5: Stopped-Status im Deployment

Nach dem Stoppen zeigt die Deployment-Detailseite den Status **Stopped**.

![Product Deployment im Stopped-Status](/images/docs/monitoring/container-control-05-stopped-status.png)

Von hier aus kann das Produkt über **Restart Containers** wieder gestartet werden.

---

## Schritt für Schritt: Container neustarten

### Schritt 1: Restart-Bestätigung

Über **Restart Containers** (auf der Deployment-Detailseite oder direkt über `/restart-product/:id`) öffnet sich der Restart-Bestätigungsscreen:

- **Product Details**
- **Stacks to restart**
- **Restart All Containers** — startet die Container neu

![Restart Containers Bestätigungsscreen](/images/docs/monitoring/container-control-06-restart-confirm.png)

---

### Schritt 2: Restart-Ergebnis

Nach dem Neustart zeigt die Seite das Ergebnis.

![Container Restart Ergebnis](/images/docs/monitoring/container-control-07-restart-result.png)

**Erfolgreich neugestartet:** Heading „Containers Restarted Successfully!" — das Produkt ist wieder im Status **Running**.

---

## Wann welche Aktion verwenden?

| Szenario | Empfohlene Aktion |
|----------|-------------------|
| Wartungsfenster | **Stop** → Wartung → **Restart** |
| Container hängen | **Restart Containers** |
| Ressourcen freigeben | **Stop Containers** |
| Produkt dauerhaft entfernen | [Remove Product](/de/docs/deployments/product-remove/) |

---

## API-Endpunkte

| Methode | Endpunkt | Beschreibung | Permission |
|---------|----------|-------------|------------|
| `POST` | `/api/environments/{envId}/product-deployments/{id}/stop-containers` | Container stoppen | `Deployments.Update` |
| `POST` | `/api/environments/{envId}/product-deployments/{id}/restart-containers` | Container neustarten | `Deployments.Update` |

---

## Fehlerbehandlung

| Situation | Verhalten |
|-----------|-----------|
| Produkt nicht stoppbar | Fehler-Screen statt Bestätigungsscreen |
| Einzelner Stack schlägt fehl | Andere Stacks werden weiter gestoppt/gestartet |
| Bereits gestoppt | `canRestart: false` → Fehler-Screen beim Öffnen von `/restart-product/:id` |
