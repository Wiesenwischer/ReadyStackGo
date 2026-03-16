---
title: Produkt entfernen
description: Ein Product Deployment vollständig aus einer Environment entfernen — mit Bestätigungsscreen, Echtzeit-Fortschrittsanzeige und Stack-Ergebnissen.
---

Mit **Remove Product** wird ein Product Deployment inklusive aller Stacks und Container vollständig aus einer Environment entfernt. Der Vorgang zeigt eine Bestätigung mit Warnhinweis, eine Echtzeit-Fortschrittsanzeige pro Stack und ein abschließendes Ergebnis-Screen mit dem Status jedes Stacks.

## Übersicht

| Schritt | Beschreibung |
|---------|-------------|
| **Bestätigung** | Warnung, Produktdetails und Liste der zu entfernenden Stacks |
| **Fortschritt** | Echtzeit-Status pro Stack mit Progress-Bar |
| **Ergebnis** | Übersicht aller Stacks mit Erfolg/Fehler-Status |

---

## Schritt für Schritt: Produkt entfernen

### Schritt 1: Deployments-Übersicht

Navigiere zur Deployments-Seite um eine Übersicht aller deployten Products zu erhalten.

![Deployments-Übersicht](/images/docs/deployments/product-remove-01-deployments.png)

---

### Schritt 2: Produkt-Detail mit Remove-Button

Öffne die Detailseite eines Product Deployments. Der **Remove**-Link ist in der Aktionsleiste sichtbar, wenn das Produkt im Status Running, Stopped oder PartiallyRunning ist.

![Product Deployment Detail mit Remove-Button](/images/docs/deployments/product-remove-02-detail.png)

:::caution[Irreversibler Vorgang]
Das Entfernen eines Produkts löscht alle zugehörigen Docker-Container und Deployment-Einträge. Volumes werden **nicht** automatisch gelöscht — Docker-Volumes bleiben erhalten und können separat bereinigt werden.
:::

---

### Schritt 3: Bestätigungsscreen

Nach Klick auf **Remove** öffnet sich der Bestätigungsscreen mit:

- **Warnhinweis** (rote Card) — deutliche Warnung vor dem irreversiblen Vorgang
- **Product Details** — Name, Version, Environment, Anzahl Stacks und Services
- **Stacks to remove** — Liste aller Stacks in Entfernungsreihenfolge (umgekehrt zur Deployment-Reihenfolge)
- **Cancel** — Zurück zum Catalog ohne Aktion
- **Remove All Stacks** — Startet den Entfernungsvorgang

![Remove Product Bestätigungsscreen](/images/docs/deployments/product-remove-03-confirm.png)

Stacks werden in **umgekehrter Reihenfolge** entfernt — der zuletzt deployete Stack wird zuerst entfernt. Das gewährleistet eine sichere Auflösung von Abhängigkeiten.

---

### Schritt 4: Fortschrittsanzeige

Nach Klick auf **Remove All Stacks** wechselt die Ansicht in den Fortschrittsmodus:

![Remove Product Fortschrittsanzeige](/images/docs/deployments/product-remove-04-progress.png)

**Linkes Panel — Stack-Liste:**
Jeder Stack zeigt seinen aktuellen Status:

| Status | Icon | Bedeutung |
|--------|------|-----------|
| Pending | Leerer Kreis | Wartet auf Entfernung |
| Removing | Roter Spinner | Wird gerade entfernt |
| Removed | Grüner Haken | Erfolgreich entfernt |
| Failed | Rotes X | Entfernung fehlgeschlagen |

**Rechtes Panel — Stack-Detail:**
Ein Klick auf einen Stack in der linken Liste zeigt Details:
- **Pending:** „Waiting to remove..."
- **Removing:** Aktuelle Fortschrittsmeldung vom Backend
- **Removed:** Grüne Erfolgsmeldung
- **Failed:** Rote Fehlermeldung mit Details

Die **rote Progress-Bar** am oberen Rand zeigt den Gesamtfortschritt (X/Y Stacks entfernt).

---

### Schritt 5: Ergebnis

Nach Abschluss des Vorgangs erscheint der Ergebnis-Screen:

![Remove Product Ergebnis](/images/docs/deployments/product-remove-05-success.png)

**Erfolgreich entfernt:**
- Grünes Erfolgs-Icon
- Heading: „Product Removed Successfully!"
- Zusammenfassung mit Produktname und Anzahl der Stacks
- Tabelle mit allen Stacks und grünem Haken
- Links: **View Deployments** und **Browse Catalog**

**Mit Fehlern abgeschlossen:**
- Rotes Warn-Icon
- Heading: „Removal Completed with Errors"
- Fehlermeldung vom Backend
- Zählung: „X removed, Y failed of Z stacks"
- Tabelle mit Einzelergebnissen — fehlgeschlagene Stacks zeigen die Fehlermeldung

---

## API-Endpunkt

| Methode | Endpunkt | Beschreibung | Permission |
|---------|----------|-------------|------------|
| `DELETE` | `/api/environments/{envId}/product-deployments/{id}` | Produkt entfernen | `Deployments.Update` |

**Request-Body:**
```json
{
  "sessionId": "product-remove-<name>-<timestamp>"
}
```

Die `sessionId` wird vom Client generiert und dient zur Zuordnung von Echtzeit-Fortschrittsmeldungen via SignalR.

**Response:**
```json
{
  "success": true,
  "productDeploymentId": "...",
  "productName": "...",
  "status": "Removed",
  "stackResults": [
    {
      "stackName": "...",
      "stackDisplayName": "...",
      "success": true,
      "serviceCount": 2
    }
  ]
}
```

---

## Fehlerbehandlung

| Situation | Verhalten |
|-----------|-----------|
| Produkt kann nicht entfernt werden | Fehlermeldung auf dem Bestätigungsscreen statt der Stack-Liste |
| Einzelner Stack schlägt fehl | Andere Stacks werden weiter entfernt; Ergebnis zeigt Einzelfehler |
| Netzwerkfehler während Removal | Fehlermeldung nach Timeout; Status im Backend kann davon abweichen |
| Produkt bereits entfernt | 404-Fehler beim Laden → Fehler-Screen mit Meldung |

:::tip[Volumes bleiben erhalten]
Docker-Volumes werden beim Entfernen eines Produkts **nicht** automatisch gelöscht. Verwende die [Volume Management](/de/docs/monitoring/volume-management/)-Seite um verwaiste Volumes nach dem Entfernen zu bereinigen.
:::
