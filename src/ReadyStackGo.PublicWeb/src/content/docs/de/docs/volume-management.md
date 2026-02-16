---
title: Volume Management
description: Docker Volumes pro Environment verwalten, Orphaned Volumes erkennen und bereinigen
---

Diese Anleitung zeigt Ihnen, wie Sie Docker Volumes in ReadyStackGo verwalten — von der Übersicht über Details bis hin zum Erstellen und Löschen.

## Übersicht

ReadyStackGo bietet eine zentrale Verwaltung für Docker Volumes pro Environment. Sie können den persistenten Speicher Ihrer Stacks überwachen und verwaiste Volumes identifizieren.

| Funktion | Beschreibung |
|----------|--------------|
| **Volume-Liste** | Alle Docker Volumes des aktiven Environments anzeigen |
| **Orphaned Detection** | Erkennung von Volumes ohne Container-Referenz |
| **Volume Details** | Größe, Mountpoint, Labels und referenzierende Container einsehen |
| **Volume erstellen** | Neue Volumes mit Name und optionalem Driver anlegen |
| **Volume löschen** | Einzelne oder alle verwaisten Volumes entfernen |
| **Orphaned Filter** | Liste auf verwaiste Volumes einschränken |

---

## Schritt für Schritt: Volumes verwalten

### Schritt 1: Volumes-Seite öffnen

Navigieren Sie über die Sidebar zum Menüpunkt **Volumes**. Die Seite zeigt alle Docker Volumes des aktiven Environments in einer Tabelle mit Name, Driver, Container-Anzahl, Status und Erstellungsdatum.

![Volume-Liste mit Übersicht aller Volumes](/images/docs/volumes-01-list.png)

---

### Schritt 2: Volume erstellen

Klicken Sie auf **Create Volume**, um das Erstellungsformular einzublenden. Geben Sie einen **Volume Name** ein und optional einen **Driver** (Standard: `local`).

![Create-Formular für ein neues Volume](/images/docs/volumes-02-create-form.png)

Klicken Sie auf **Create**, um das Volume anzulegen. Es erscheint anschließend in der Liste.

![Neu erstelltes Volume in der Liste sichtbar](/images/docs/volumes-03-volume-created.png)

:::tip[Driver]
Der Standard-Driver `local` speichert Daten auf dem Host-Dateisystem. Für spezielle Anforderungen (NFS, CIFS etc.) können Sie einen anderen Driver angeben.
:::

---

### Schritt 3: Volume-Details anzeigen

Klicken Sie auf den **Volume-Namen** in der Liste, um die Detail-Seite zu öffnen. Dort finden Sie:

- **Volume Information**: Name, Driver, Scope, Mountpoint, Größe und Erstellungsdatum
- **Referenced by Containers**: Liste aller Container, die dieses Volume verwenden

![Volume-Detail-Seite mit Informationen und Container-Referenzen](/images/docs/volumes-04-detail.png)

---

### Schritt 4: Orphaned Volumes erkennen

Volumes ohne Container-Referenz werden automatisch als **orphaned** markiert (gelbes Badge). Diese Volumes belegen Speicherplatz, werden aber von keinem Container genutzt.

![Orphaned-Badge bei einem ungenutzten Volume](/images/docs/volumes-05-orphaned-badge.png)

Nutzen Sie den **Orphaned only** Filter, um nur verwaiste Volumes anzuzeigen:

![Orphaned-Filter aktiv — nur verwaiste Volumes sichtbar](/images/docs/volumes-06-orphaned-filter.png)

:::note[Orphaned Detection]
Die Orphaned-Erkennung prüft, ob ein Volume als Mount in einem laufenden oder gestoppten Container referenziert wird. Volumes ohne Referenz gelten als verwaist.
:::

---

### Schritt 5: Volume löschen

Klicken Sie auf **Remove** bei einem Volume. Es erscheint eine Inline-Bestätigung mit **Confirm** und **Cancel**:

![Lösch-Bestätigung mit Confirm/Cancel Buttons](/images/docs/volumes-07-delete-confirm.png)

Klicken Sie auf **Confirm**, um das Volume endgültig zu entfernen.

:::caution[Unwiderruflich]
Das Löschen eines Volumes entfernt alle darin gespeicherten Daten. Diese Aktion kann nicht rückgängig gemacht werden.
:::

**Bulk-Delete**: Falls verwaiste Volumes existieren, erscheint ein **Remove Orphaned** Button, mit dem alle Orphaned Volumes auf einmal gelöscht werden können (nach Bestätigung).

---

## Volume löschen von der Detail-Seite

Auf der Detail-Seite können Sie ein Volume über den **Remove Volume** Button löschen. Bei Volumes, die noch von Containern referenziert werden, wird ein Force-Modus verwendet.

:::caution[In-Use Volumes]
Beim Löschen eines Volumes, das noch von Containern referenziert wird, erscheint eine Warnung. Das Entfernen im Force-Modus kann zu Datenverlust in laufenden Containern führen.
:::

---

## API-Referenz

Die Volume-Verwaltung ist auch per REST API verfügbar:

| Endpoint | Methode | Beschreibung |
|----------|---------|--------------|
| `/api/volumes?environment={id}` | GET | Alle Volumes auflisten |
| `/api/volumes/{name}?environment={id}` | GET | Volume-Details abrufen |
| `/api/volumes?environment={id}` | POST | Volume erstellen |
| `/api/volumes/{name}?environment={id}&force={bool}` | DELETE | Volume löschen |

---

## Weiterführende Links

- [Stack Deployment](/de/docs/stack-deployment/) - Stacks deployen
- [CI/CD Integration](/de/docs/ci-cd-integration/) - Automatisierte Deployments
