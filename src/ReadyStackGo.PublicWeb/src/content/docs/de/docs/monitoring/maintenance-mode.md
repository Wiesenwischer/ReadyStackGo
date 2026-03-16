---
title: Maintenance Mode
description: Produkte in den Wartungsmodus versetzen und kontrolliert wieder freigeben
---

Der **Maintenance Mode** ermöglicht es, ein Product Deployment gezielt in den Wartungsmodus zu versetzen. Dabei werden alle Container gestoppt und alle Child-Stacks erhalten den Operation Mode „Maintenance". So können Wartungsarbeiten wie Datenbank-Migrationen, Hardware-Updates oder geplante Downtimes sicher durchgeführt werden. Das Trigger-System stellt sicher, dass manuell aktivierter Maintenance nicht versehentlich durch den Observer aufgehoben wird.

## Übersicht

| Aspekt | Normal Mode | Maintenance Mode |
|--------|-------------|-----------------|
| **Product Status** | Running | Stopped |
| **Stack Status** | Running | Stopped |
| **Operation Mode** | Normal | Maintenance (propagiert auf alle Stacks) |
| **Trigger** | — | Manual oder Observer |
| **Beenden** | — | Nur durch den Trigger, der Maintenance aktiviert hat |

### Trigger-Ownership

Das zentrale Prinzip: **Wer Maintenance aktiviert hat, kontrolliert auch das Ende.**

- **Manual Trigger**: Maintenance wurde vom Benutzer über die UI oder API aktiviert. Nur der Benutzer kann Maintenance wieder beenden — der Observer hat keinen Einfluss.
- **Observer Trigger**: Maintenance wurde automatisch durch den Maintenance Observer aktiviert. Nur wenn der Observer wieder Normal meldet, wird Maintenance aufgehoben.

---

## Schritt für Schritt: Maintenance Mode aktivieren

### Schritt 1: Product Deployment öffnen

Navigieren Sie zur **Product Deployment Detail**-Seite. Im Normalzustand sehen Sie den **Operation Mode: Normal** in den Overview Cards und den Link **Enter Maintenance** in der Aktionsleiste.

![Product Deployment im Normal Mode mit Enter Maintenance Link](/images/docs/maintenance-01-normal-mode.png)

---

### Schritt 2: Bestätigungsseite prüfen

Klicken Sie auf **Enter Maintenance**. Sie werden zur Bestätigungsseite weitergeleitet, die Folgendes anzeigt:

- Produktname und Version
- Das Environment
- Alle betroffenen Stacks mit Service-Anzahl
- Eine Warnung, dass alle Container gestoppt werden

Prüfen Sie die betroffenen Stacks, bevor Sie bestätigen.

![Enter Maintenance Bestätigungsseite mit Stack-Vorschau](/images/docs/maintenance-02-in-maintenance.png)

---

### Schritt 3: Bestätigen und aktivieren

Klicken Sie auf **Enter Maintenance Mode** um zu bestätigen. ReadyStackGo:

1. Setzt den Product Operation Mode auf Maintenance
2. Propagiert Maintenance auf alle Child-Stacks
3. Stoppt alle Container

Nach erfolgreicher Aktivierung sehen Sie eine Erfolgsseite mit dem Mode-Übergang (Normal → Maintenance).

![Maintenance erfolgreich aktiviert](/images/docs/maintenance-03-overview-cards.png)

:::tip[Maintenance Reason]
Beim Aktivieren über die API kann optional ein Grund angegeben werden (z.B. „Scheduled database migration"). Dieser wird im Maintenance Info-Panel auf der Deployment-Detailseite angezeigt.
:::

---

### Schritt 4: Stacks während Maintenance

Auf der Product Deployment Detail-Seite zeigen alle Stacks den Status **Stopped** während Maintenance. Der Product Status zeigt ebenfalls **Stopped** mit einem **Maintenance** Badge.

![Stacks zeigen Stopped-Status während Maintenance](/images/docs/maintenance-05-stacks-during.png)

---

### Schritt 5: Maintenance Mode beenden

Klicken Sie auf **Exit Maintenance** um zur Bestätigungsseite zu navigieren. Diese zeigt die aktuelle Maintenance-Info (Trigger-Quelle, Grund, Dauer) und die Stacks, die neu gestartet werden.

Klicken Sie auf **Exit Maintenance Mode** um zu bestätigen. ReadyStackGo startet alle Container neu und versetzt das Produkt zurück in den Normalbetrieb.

![Maintenance erfolgreich deaktiviert](/images/docs/maintenance-04-exited.png)

:::caution[Observer-Maintenance]
Wenn Maintenance durch den Observer aktiviert wurde, kann es **nicht** manuell über die UI beendet werden. Der Exit-Link ist in diesem Fall nicht sichtbar. Maintenance wird erst aufgehoben, wenn die externe Quelle wieder Normal meldet.
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
