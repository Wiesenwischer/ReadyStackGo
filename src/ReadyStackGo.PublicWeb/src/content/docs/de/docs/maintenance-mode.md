---
title: Maintenance Mode
description: Produkte in den Wartungsmodus versetzen und kontrolliert wieder freigeben
---

Der **Maintenance Mode** ermöglicht es, ein Product Deployment gezielt in den Wartungsmodus zu versetzen. Dabei werden alle Container gestoppt, sodass Wartungsarbeiten wie Datenbank-Migrationen, Hardware-Updates oder geplante Downtimes sicher durchgeführt werden können. Das Trigger-System stellt sicher, dass manuell aktivierter Maintenance nicht versehentlich durch den Observer aufgehoben wird.

## Übersicht

| Aspekt | Normal Mode | Maintenance Mode |
|--------|-------------|-----------------|
| **Container** | Laufen normal | Werden gestoppt |
| **Trigger** | — | Manual oder Observer |
| **Beenden** | — | Nur durch den Trigger, der Maintenance aktiviert hat |
| **Observer** | Kann Maintenance aktivieren | Kann manuellen Maintenance nicht aufheben |

### Trigger-Ownership

Das zentrale Prinzip: **Wer Maintenance aktiviert hat, kontrolliert auch das Ende.**

- **Manual Trigger**: Maintenance wurde vom Benutzer über die UI oder API aktiviert. Nur der Benutzer kann Maintenance wieder beenden — der Observer hat keinen Einfluss.
- **Observer Trigger**: Maintenance wurde automatisch durch den Maintenance Observer aktiviert (z.B. externe Health-Check-Quelle meldet Wartung). Nur wenn der Observer wieder Normal meldet, wird Maintenance aufgehoben.

---

## Schritt für Schritt: Maintenance Mode aktivieren

### Schritt 1: Product Deployment öffnen

Navigieren Sie zur **Product Deployment Detail**-Seite. Im Normalzustand sehen Sie den **Operation Mode: Normal** in den Overview Cards und den Button **Enter Maintenance** in der Aktionsleiste.

![Product Deployment im Normal Mode mit Enter Maintenance Button](/images/docs/maintenance-01-normal-mode.png)

---

### Schritt 2: Maintenance Mode aktivieren

Klicken Sie auf den Button **Enter Maintenance**. ReadyStackGo versetzt das Produkt in den Maintenance Mode und stoppt alle Container der zugehörigen Stacks.

Nach der Aktivierung ändert sich die Ansicht:

- Ein **Maintenance Badge** erscheint neben dem Status
- Das **Maintenance Info-Panel** zeigt den Trigger-Typ (Manual) an
- Der **Operation Mode** wechselt auf „Maintenance"
- Der Button wechselt zu **Exit Maintenance**

![Product Deployment im Maintenance Mode mit Info-Panel](/images/docs/maintenance-02-in-maintenance.png)

:::tip[Maintenance Reason]
Beim Aktivieren über die API kann optional ein Grund angegeben werden (z.B. „Scheduled database migration"). Dieser wird im Info-Panel angezeigt.
:::

---

### Schritt 3: Status prüfen

Die Overview Cards zeigen den aktuellen Zustand auf einen Blick. Während Maintenance ist der **Operation Mode** auf „Maintenance" gesetzt und das Info-Panel zeigt Details zum aktiven Maintenance.

![Overview Cards während Maintenance Mode](/images/docs/maintenance-03-overview-cards.png)

---

### Schritt 4: Stacks während Maintenance

Auch im Maintenance Mode bleibt die **Stacks-Tabelle** sichtbar. Sie zeigt alle zum Produkt gehörenden Stacks mit ihrem jeweiligen Status an.

![Stacks-Tabelle während Maintenance mit Reason](/images/docs/maintenance-05-stacks-during.png)

---

### Schritt 5: Maintenance Mode beenden

Klicken Sie auf **Exit Maintenance**, um den Normalbetrieb wiederherzustellen. ReadyStackGo startet alle Container der zugehörigen Stacks neu.

- Der **Operation Mode** wechselt zurück auf „Normal"
- Das **Maintenance Info-Panel** verschwindet
- Der Button wechselt zurück zu **Enter Maintenance**

![Product Deployment nach Beenden des Maintenance Mode](/images/docs/maintenance-04-exited.png)

:::caution[Observer-Maintenance]
Wenn Maintenance durch den Observer aktiviert wurde, kann es **nicht** manuell über die UI beendet werden. Der Exit-Button ist in diesem Fall nicht sichtbar. Maintenance wird erst aufgehoben, wenn die externe Quelle wieder Normal meldet.
:::

---

## API-Endpoint

Der Maintenance Mode kann auch über die REST API gesteuert werden:

```
PUT /api/environments/{environmentId}/product-deployments/{productDeploymentId}/operation-mode
```

### Request Body

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|-------------|
| `mode` | string | Ja | `"Maintenance"` oder `"Normal"` |
| `reason` | string | Nein | Optionaler Grund für die Wartung |

### Beispiele

**Maintenance aktivieren:**
```json
{
  "mode": "Maintenance",
  "reason": "Scheduled database migration"
}
```

**Maintenance beenden:**
```json
{
  "mode": "Normal"
}
```

### HTTP Status Codes

| Code | Bedeutung |
|------|-----------|
| 200 | Modus erfolgreich geändert |
| 404 | Product Deployment nicht gefunden |
| 409 | Transition blockiert — Trigger-Ownership verletzt (z.B. manuelles Beenden von Observer-Maintenance) |

---

## Fehlerbehandlung

| Situation | Verhalten |
|-----------|----------|
| Manuelles Exit bei Observer-Maintenance | Blockiert mit HTTP 409 — Observer kontrolliert das Ende |
| Produkt bereits im gewünschten Modus | Keine Aktion, erfolgreiche Rückgabe (No-Op) |
| Observer meldet Normal bei manuellem Maintenance | Keine Aktion — manueller Trigger hat Vorrang |
