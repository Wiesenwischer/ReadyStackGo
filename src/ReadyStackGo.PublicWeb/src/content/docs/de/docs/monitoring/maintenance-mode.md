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

## Das Produkt benachrichtigen (Maintenance-Setter)

Standardmäßig ist die Maintenance-Integration **read-only**: Der Observer pollt ein Flag, das
das Produkt setzt. Daraus entsteht eine Lücke — startet ein Operator die Wartung **direkt in
ReadyStackGo**, erfährt das Produkt selbst nichts davon. Der optionale **Maintenance-Setter**
schließt diese Lücke: Er ist das Spiegelbild zum Observer und **propagiert** einen
RSGO-initiierten Wartungszustand **aktiv** ans Produkt — so kann das Produkt seine Clients
vorwarnen/geordnet beenden, und sein eigenes Maintenance-Flag bleibt mit RSGO konsistent.

Der Setter wird pro Produkt im Manifest deklariert (`maintenance.setter`) und unterstützt zwei Typen:

| Typ | Was er tut | Gut für |
|-----|------------|---------|
| `sqlExtendedProperty` | Schreibt **dieselbe** SQL-Server-Extended-Property, die der Observer liest | Konsistenz von RSGO & SQL-Flag; funktioniert auch wenn das Produkt down ist |
| `webhook` | `POST { "state": "maintenance" \| "normal" }`, HMAC-SHA256-signiert | Synchrone Produkt-Reaktion (z. B. Clients vorwarnen), solange es noch läuft |

**Wann er feuert:**

- Eintritt in Maintenance: Setter schreibt `maintenance` **vor** dem Stoppen der Container.
- Rückkehr in Normal: Setter schreibt `normal` **nach** dem Neustart der Container.
- Ein optionales `gracePeriod` verzögert den Container-Stop, damit das Produkt seine Clients drainen kann.

:::note[Kein Feedback-Loop]
Der Setter feuert **nur bei manuellen/Operator-Übergängen** — nie bei observer-getriggerten
(dort hat das Produkt das Flag bereits gesetzt). Der SQL-Write ist idempotent, sodass Observer
(lesen) und Setter (schreiben) sich nicht gegenseitig aufschaukeln können.
:::

:::caution[Best-effort & sicher]
Setter-Fehler (DB nicht erreichbar, Webhook-Timeout/5xx) sind **non-fatal**: Sie werden geloggt
und in der API-Antwort ausgewiesen, verhindern den Wartungsübergang aber nie. Webhook-Secrets
werden nie geloggt, externe Webhook-URLs nie serverseitig gelesen (kein SSRF).
:::

Die YAML-Felder sind in der [Manifest-Format-Referenz](/de/reference/manifest-format/#setter-konfiguration) dokumentiert.

---

## Datenbankzugriff während der Wartung

Viele Produkte brauchen ihre Datenbank während der eigenen Wartung **exklusiv** — ein Update
schaltet sie auf `SINGLE_USER`, ein Restore nimmt sie offline. Ein SQL-Observer liest das
Maintenance-Flag aber genau in dieser Datenbank. ReadyStackGo behandelt das explizit:

**RSGO hält keine Verbindung offen.** Alle Verbindungen, die RSGO für Observer und Setter selbst
aufbaut, sind **nicht gepoolt**: Die Session existiert nur für die Dauer eines einzelnen Lesevorgangs
und ist danach weg. Produkt-Routinen, die vor dem exklusiven Zugriff warten, bis alle Verbindungen
geschlossen sind, warten dadurch nie auf ReadyStackGo. Die Container des Produkts sind davon nicht
betroffen — deren Connection-Strings baut RSGO nicht, sie behalten ihr Pooling. RSGOs eigene Sessions
tragen den Application Name `ReadyStackGo-Maintenance` und sind so in `sys.dm_exec_sessions` bzw.
`sp_who2` eindeutig identifizierbar.

**RSGO liest keine Datenbank, die es nicht anfassen darf.** Vor jedem Lesevorgang prüft ein
SQL-Observer über eine `master`-Verbindung in `sys.databases`, ob die Datenbank verfügbar ist.
`master` bleibt erreichbar, während eine andere Datenbank `SINGLE_USER`, `RESTORING` oder `OFFLINE`
ist; die Abfrage setzt kein Lock auf der Zieldatenbank und kann den Single-User-Platz nicht belegen,
den das Produkt-Update selbst benötigt. Solange die Datenbank nicht verfügbar ist, meldet der
Observer Maintenance mit dem beobachteten Wert `database-exclusive (<Status>/<Zugriffsmodus>)` und
lässt die Datenbank in Ruhe.

Sobald sie wieder `ONLINE` und `MULTI_USER` ist, liest der nächste Poll das Flag normal — die
automatische Rückkehr in den Normalbetrieb funktioniert also weiter, unabhängig davon, wie lange das
Produkt braucht.

:::note[Manuelle Wartung setzt den Observer aus]
Ist Maintenance **manuell** aktiviert, pollt der Observer gar nicht. Er dürfte manuelle Wartung
ohnehin nicht beenden (Trigger-Ownership), könnte also auf kein Ergebnis reagieren. Während eines
manuellen Wartungsfensters baut RSGO damit überhaupt keine Verbindung zum Produkt auf.
:::

:::caution[Zugriff auf master]
Kann die Verfügbarkeit nicht ermittelt werden — etwa weil der Observer-Login keinen Zugriff auf
`master` hat — loggt RSGO einmalig eine Warnung und liest das Flag wie bisher direkt. Ein Zugriff auf
`master` für den Observer-Login stellt den Schutz wieder her.
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
