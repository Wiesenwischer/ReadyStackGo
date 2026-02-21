---
title: Product Deployment
description: Ganze Produkte mit allen Stacks in einem Vorgang deployen, upgraden und verwalten
---

Bei Multi-Stack Produkten wie z.B. einer Microservices-Architektur mit 10+ Stacks mussten bisher alle Stacks einzeln deployed werden. Mit **Product Deployment** wird das gesamte Produkt in einem Vorgang ausgerollt — mit gemeinsamen Variablen, Fortschrittsanzeige und koordiniertem Lifecycle.

## Übersicht

| Aspekt | Einzelnes Stack Deployment | Product Deployment |
|--------|---------------------------|-------------------|
| **Deploy** | Ein Stack pro Vorgang | Alle Stacks eines Produkts am Stück |
| **Variablen** | Pro Stack einzeln konfigurieren | Shared Variables einmal, dann pro Stack |
| **Fortschritt** | Ein Stack mit eigener Session | Ein SessionId, N Stacks mit Overall-Fortschritt |
| **Status** | Kein aggregierter Status | `ProductDeploymentStatus` mit State Machine |
| **Fehlerbehandlung** | Stack-individuell | `ContinueOnError` — bei Teilfehlern weitermachen |

---

## Schritt für Schritt: Produkt deployen

Diese Anleitung zeigt den kompletten Workflow zum Deployment eines Multi-Stack Produkts über die Web-Oberfläche.

### Schritt 1: Produkt im Katalog finden

Navigieren Sie zum **Stack Catalog**. Multi-Stack Produkte werden mit einem Badge angezeigt, das die Anzahl der enthaltenen Stacks zeigt (z.B. „2 stacks").

![Multi-Stack Produkt im Katalog mit Stack-Badge](/images/docs/product-deploy-01-catalog.png)

---

### Schritt 2: Produkt-Details ansehen

Klicken Sie auf **View Details**, um die Produkt-Detailseite zu öffnen. Hier sehen Sie alle enthaltenen Stacks mit ihren Beschreibungen und den **Deploy All** Button.

![Produkt-Detailseite mit Stack-Übersicht und Deploy All Button](/images/docs/product-deploy-02-detail.png)

:::tip[Per-Stack Deployment Status]
Wenn ein Produkt bereits deployed ist, zeigt die Detailseite den Status jedes einzelnen Stacks an — einschließlich Health-Indikatoren und Versionsinformationen.
:::

---

### Schritt 3: Deployment konfigurieren

Nach Klick auf **Deploy All** gelangen Sie zur Konfigurationsseite. Hier konfigurieren Sie:

- **Shared Variables** — Werte die an alle Stacks weitergegeben werden (z.B. Log Level, Datenbank-Host)
- **Stack-Konfiguration** — Pro Stack können Variablen individuell angepasst werden (Accordion-Ansicht)
- **Continue on Error** — Legt fest ob bei einem Stack-Fehler die verbleibenden Stacks trotzdem deployed werden

![Deployment-Konfigurationsseite mit Shared Variables und Stack-Konfiguration](/images/docs/product-deploy-03-configure.png)

In der Sidebar sehen Sie eine Zusammenfassung mit **Product Info** und dem **Deploy All Stacks** Button.

---

### Schritt 4: Deployment verfolgen (Split-View)

Nach Klick auf **Deploy All Stacks** wechselt die Ansicht in den **Split-View Modus**:

- **Links**: Kompakte Stack-Liste mit Echtzeit-Statusindikatoren (Pending, Deploying, Running, Failed)
- **Rechts**: Detaillierte Fortschrittsanzeige für den ausgewählten Stack — Progress Bar, Service-Zähler, Init Container Logs

![Split-View Deployment-Fortschritt mit Stack-Liste und Detail-Panel](/images/docs/product-deploy-04-deploying.png)

Die Stacks werden **sequenziell** deployed. Der aktuell laufende Stack wird automatisch ausgewählt. Sie können jederzeit auf einen anderen Stack klicken, um dessen Fortschritt oder Ergebnis einzusehen.

:::note[Echtzeit-Updates]
Die Fortschrittsanzeige wird über SignalR in Echtzeit aktualisiert. Der Verbindungsstatus wird durch einen farbigen Punkt angezeigt: grün = verbunden, gelb = verbindet/reconnecting, rot = nicht verfügbar.
:::

---

### Schritt 5: Ergebnis prüfen

Nach Abschluss des Deployments wird das Ergebnis angezeigt — entweder **Product Deployed Successfully** oder **Deployment Failed** mit Details pro Stack.

![Deployment-Ergebnis mit Stack-Resultaten](/images/docs/product-deploy-05-result.png)

Von hier aus können Sie direkt zu den **Deployments** navigieren.

---

### Schritt 6: Deployments verwalten

Auf der **Deployments** Seite erscheinen die einzelnen Stack-Deployments des Produkts. Jeder Stack kann separat verwaltet werden (Logs, Redeploy, etc.).

![Deployments-Seite mit den deployed Product Stacks](/images/docs/product-deploy-06-deployments.png)

---

### Schritt 7: Status auf der Produkt-Detailseite

Wenn Sie zum Katalog zurückkehren und die Produkt-Detailseite erneut aufrufen, sehen Sie den **Deployment Status** für jeden einzelnen Stack — inklusive Health-Checks.

![Produkt-Detailseite mit per-Stack Deployment Status](/images/docs/product-deploy-07-status.png)

---

## Konzept: Zwei-Ebenen-Architektur

Product Deployment arbeitet auf zwei Ebenen:

1. **ProductDeployment** (Aggregate Root) — koordiniert alle Stacks, trackt den Gesamtstatus
2. **Deployment** (pro Stack) — die bestehende Stack-Deployment-Logik mit Container-Operations

```
┌─────────────────────────────────────────────┐
│ ProductDeployment                            │
│ Status: Running │ Version: 3.1.0             │
│                                              │
│ ┌─ Stack: infrastructure (Order: 0) ───┐     │
│ │ Status: Running                      │──→  Deployment (infra-stack)
│ └──────────────────────────────────────┘     │
│                                              │
│ ┌─ Stack: identity-access (Order: 1) ──┐     │
│ │ Status: Running                      │──→  Deployment (identity-stack)
│ └──────────────────────────────────────┘     │
│                                              │
│ ┌─ Stack: business (Order: 2) ─────────┐     │
│ │ Status: Running                      │──→  Deployment (business-stack)
│ └──────────────────────────────────────┘     │
└─────────────────────────────────────────────┘
```

Die Stacks werden **sequenziell** in Manifest-Reihenfolge deployed — das respektiert Abhängigkeiten zwischen Stacks (z.B. Datenbank vor Application Server).

---

## Status-Lifecycle

Product Deployments durchlaufen folgende Status:

```
Deploying ──→ Running              (alle Stacks erfolgreich)
          ──→ PartiallyRunning     (einige Stacks fehlgeschlagen)
          ──→ Failed               (alle Stacks fehlgeschlagen)

Running ──→ Upgrading ──→ Running / PartiallyRunning / Failed
        ──→ Removing  ──→ Removed (terminal)

Failed ──→ Upgrading (erneuter Versuch mit neuer Version)
       ──→ Removing  (Aufräumen)
```

| Status | Bedeutung |
|--------|-----------|
| `Deploying` | Deployment läuft, Stacks werden sequenziell ausgerollt |
| `Running` | Alle Stacks erfolgreich deployed und aktiv |
| `PartiallyRunning` | Einige Stacks laufen, andere sind fehlgeschlagen |
| `Failed` | Deployment komplett fehlgeschlagen |
| `Upgrading` | Upgrade auf eine neue Version läuft |
| `Removing` | Alle Stacks werden entfernt |
| `Removed` | Alle Stacks entfernt (Endzustand) |

---

## Variablen-Konfiguration

Product Deployment unterstützt ein dreistufiges Variablen-System:

1. **Stack Defaults** — in der Stack-Definition hinterlegte Standardwerte
2. **Shared Variables** — produktübergreifende Variablen (z.B. Datenbank-Host)
3. **Per-Stack Overrides** — stack-spezifische Überschreibungen

Die Priorität aufsteigend: Stack Defaults → Shared Variables → Per-Stack Overrides.

:::tip[Shared Variables]
Setze gemeinsam genutzte Werte wie Datenbank-Verbindungen oder API-URLs als Shared Variables. Diese werden automatisch an alle Stacks weitergegeben und können bei Bedarf pro Stack überschrieben werden.
:::

---

## API-Endpunkte

### POST /api/environments/{environmentId}/product-deployments

Startet ein neues Product Deployment. Alle Stacks des Produkts werden sequenziell deployed.

**Permission:** `Deployments.Create`

**Request:**

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `productId` | string | Ja | Produkt-ID aus dem Katalog (z.B. `stacks:myproduct:1.0.0`) |
| `stackConfigs` | array | Ja | Konfiguration für jeden Stack |
| `stackConfigs[].stackId` | string | Ja | Stack-ID aus dem Katalog |
| `stackConfigs[].deploymentStackName` | string | Ja | Name für das Deployment |
| `stackConfigs[].variables` | object | Nein | Stack-spezifische Variablen |
| `sharedVariables` | object | Nein | Produktübergreifende Variablen |
| `sessionId` | string | Nein | Client-generierte Session ID für SignalR Tracking |
| `continueOnError` | boolean | Nein | Bei Fehler weitermachen (Standard: `true`) |

```json
{
  "productId": "stacks:ams.project:3.1.0",
  "stackConfigs": [
    {
      "stackId": "stacks:ams.project:infrastructure:3.1.0",
      "deploymentStackName": "ams-infra",
      "variables": {
        "DB_PASSWORD": "secret123"
      }
    },
    {
      "stackId": "stacks:ams.project:identity:3.1.0",
      "deploymentStackName": "ams-identity",
      "variables": {}
    },
    {
      "stackId": "stacks:ams.project:business:3.1.0",
      "deploymentStackName": "ams-business",
      "variables": {}
    }
  ],
  "sharedVariables": {
    "DB_HOST": "postgres.local",
    "REDIS_URL": "redis://cache:6379"
  },
  "continueOnError": true
}
```

**Response (200):**

```json
{
  "success": true,
  "productDeploymentId": "a1b2c3d4-...",
  "productName": "ams.project",
  "productVersion": "3.1.0",
  "status": "Running",
  "sessionId": "product-ams.project-20260217120000000",
  "stackResults": [
    {
      "stackName": "infrastructure",
      "stackDisplayName": "Infrastructure",
      "success": true,
      "deploymentId": "d1e2f3...",
      "deploymentStackName": "ams-infra",
      "serviceCount": 3
    },
    {
      "stackName": "identity",
      "stackDisplayName": "Identity Access",
      "success": true,
      "deploymentId": "g4h5i6...",
      "deploymentStackName": "ams-identity",
      "serviceCount": 2
    }
  ]
}
```

**Fehlerantwort (400) — Produkt nicht gefunden:**

```json
{
  "success": false,
  "message": "Product 'nonexistent:product:1.0.0' not found in catalog."
}
```

---

### GET /api/environments/{environmentId}/product-deployments

Listet alle Product Deployments in einer Umgebung auf (ohne `Removed`).

**Permission:** `Deployments.Read`

**Response (200):**

```json
{
  "success": true,
  "productDeployments": [
    {
      "productDeploymentId": "a1b2c3d4-...",
      "productGroupId": "stacks:ams.project",
      "productName": "ams.project",
      "productDisplayName": "AMS Project",
      "productVersion": "3.1.0",
      "status": "Running",
      "createdAt": "2026-02-17T12:00:00Z",
      "completedAt": "2026-02-17T12:05:30Z",
      "totalStacks": 3,
      "completedStacks": 3,
      "failedStacks": 0,
      "canUpgrade": true,
      "canRemove": true
    }
  ]
}
```

---

### GET /api/environments/{environmentId}/product-deployments/{id}

Gibt ein spezifisches Product Deployment mit allen Stack-Details zurück.

**Permission:** `Deployments.Read`

**Response (200):**

```json
{
  "productDeploymentId": "a1b2c3d4-...",
  "environmentId": "env-123",
  "productGroupId": "stacks:ams.project",
  "productId": "stacks:ams.project:3.1.0",
  "productName": "ams.project",
  "productDisplayName": "AMS Project",
  "productVersion": "3.1.0",
  "status": "Running",
  "createdAt": "2026-02-17T12:00:00Z",
  "completedAt": "2026-02-17T12:05:30Z",
  "continueOnError": true,
  "totalStacks": 3,
  "completedStacks": 3,
  "failedStacks": 0,
  "upgradeCount": 0,
  "canUpgrade": true,
  "canRemove": true,
  "durationSeconds": 330.5,
  "stacks": [
    {
      "stackName": "infrastructure",
      "stackDisplayName": "Infrastructure",
      "stackId": "stacks:ams.project:infrastructure:3.1.0",
      "deploymentId": "d1e2f3...",
      "deploymentStackName": "ams-infra",
      "status": "Running",
      "startedAt": "2026-02-17T12:00:01Z",
      "completedAt": "2026-02-17T12:02:15Z",
      "order": 0,
      "serviceCount": 3,
      "isNewInUpgrade": false
    }
  ],
  "sharedVariables": {
    "DB_HOST": "postgres.local",
    "REDIS_URL": "redis://cache:6379"
  }
}
```

---

### GET /api/environments/{environmentId}/product-deployments/by-product/{groupId}

Gibt das aktive Product Deployment für eine bestimmte Product Group zurück.

**Permission:** `Deployments.Read`

Die Response hat das gleiche Format wie `GET .../{id}`.

:::note
`groupId` ist die logische Produkt-Kennung ohne Version, z.B. `stacks:ams.project`. Es wird immer das neueste, nicht entfernte Deployment zurückgegeben.
:::

---

### POST /api/environments/{environmentId}/product-deployments/{id}/upgrade

Aktualisiert ein laufendes Product Deployment auf eine neue Version. Alle Stacks werden sequenziell mit Variable-Merging aus dem bestehenden Deployment aktualisiert.

**Permission:** `Deployments.Write`

**Request:**

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `targetProductId` | string | Ja | Produkt-ID der Zielversion (z.B. `stacks:myproduct:2.0.0`) |
| `stackConfigs` | array | Ja | Konfiguration für jeden Stack der Zielversion |
| `stackConfigs[].stackId` | string | Ja | Stack-ID aus dem Zielprodukt |
| `stackConfigs[].deploymentStackName` | string | Ja | Name für das Deployment |
| `stackConfigs[].variables` | object | Nein | Stack-spezifische Variable-Overrides |
| `sharedVariables` | object | Nein | Neue Shared Variables für das Upgrade |
| `sessionId` | string | Nein | Client-generierte Session ID für SignalR Tracking |
| `continueOnError` | boolean | Nein | Bei Fehler weitermachen (Standard: `true`) |

**Response (200):**

```json
{
  "success": true,
  "productDeploymentId": "new-id-...",
  "productName": "ams.project",
  "previousVersion": "3.1.0",
  "newVersion": "4.0.0",
  "status": "Running",
  "sessionId": "product-upgrade-ams.project-20260217...",
  "stackResults": [
    {
      "stackName": "infrastructure",
      "stackDisplayName": "Infrastructure",
      "success": true,
      "deploymentId": "d1e2f3...",
      "serviceCount": 3,
      "isNewInUpgrade": false
    }
  ]
}
```

:::note
Beim Upgrade werden bestehende Variable-Werte aus dem aktuellen Deployment übernommen. Die Variable-Merge-Priorität ist: Stack Defaults → Bestehende Werte → Shared Variables → Per-Stack Overrides. Neue Stacks in der Zielversion werden mit `isNewInUpgrade: true` gekennzeichnet.
:::

---

### GET /api/environments/{environmentId}/product-deployments/{id}/upgrade/check

Prüft ob ein Upgrade für ein Product Deployment verfügbar ist, indem die deployed Version mit dem Katalog verglichen wird.

**Permission:** `Deployments.Read`

**Response (200):**

```json
{
  "success": true,
  "upgradeAvailable": true,
  "currentVersion": "3.1.0",
  "latestVersion": "4.0.0",
  "latestProductId": "stacks:ams.project:4.0.0",
  "availableVersions": [
    {
      "version": "4.0.0",
      "productId": "stacks:ams.project:4.0.0",
      "sourceId": "stacks",
      "stackCount": 4
    },
    {
      "version": "3.2.0",
      "productId": "stacks:ams.project:3.2.0",
      "sourceId": "stacks",
      "stackCount": 3
    }
  ],
  "newStacks": ["monitoring"],
  "removedStacks": [],
  "canUpgrade": true
}
```

---

### DELETE /api/environments/{environmentId}/product-deployments/{id}

Entfernt ein Product Deployment mit allen Stacks. Stacks werden in **umgekehrter** Manifest-Reihenfolge entfernt (Abhängigkeiten zuletzt).

**Permission:** `Deployments.Delete`

**Request (optionaler Body):**

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `sessionId` | string | Nein | Client-generierte Session ID für SignalR Tracking |

**Response (200):**

```json
{
  "success": true,
  "productDeploymentId": "a1b2c3d4-...",
  "productName": "ams.project",
  "status": "Removed",
  "sessionId": "product-remove-ams.project-20260217...",
  "stackResults": [
    {
      "stackName": "business",
      "stackDisplayName": "Business",
      "success": true,
      "serviceCount": 4
    },
    {
      "stackName": "identity",
      "stackDisplayName": "Identity Access",
      "success": true,
      "serviceCount": 2
    },
    {
      "stackName": "infrastructure",
      "stackDisplayName": "Infrastructure",
      "success": true,
      "serviceCount": 3
    }
  ]
}
```

:::caution
Das Entfernen wird fortgesetzt, auch wenn einzelne Stack-Entfernungen fehlschlagen. Das Aggregate wechselt in jedem Fall zu `Removed` — fehlgeschlagene Docker-Entfernungen werden in `stackResults` gemeldet, verhindern aber nicht den Statuswechsel.
:::

---

## Health Sync

Ein Hintergrund-Service synchronisiert periodisch den `ProductDeployment`-Status mit den zugrunde liegenden `Deployment`-Aggregaten. Dies korrigiert Abweichungen, wenn Container außerhalb der ReadyStackGo-Orchestrierung abstürzen oder sich erholen (z.B. Docker startet einen Container neu).

- **Sync-Intervall**: alle 60 Sekunden (nach 20 Sekunden Initialverzögerung)
- **Scope**: nur `Running` und `PartiallyRunning` Deployments
- **Transitions**: `Running` → `PartiallyRunning` (wenn ein Stack ausfällt) und `PartiallyRunning` → `Running` (wenn alle Stacks sich erholen)

---

## Echtzeit-Fortschritt via SignalR

Während des Deployments sendet ReadyStackGo Echtzeit-Updates über SignalR:

1. **Vor jedem Stack**: Fortschrittsmeldung mit Stack-Index und Gesamtanzahl
2. **Während jedes Stacks**: Service-Level Fortschritt (vom bestehenden Stack Deployment)
3. **Nach Abschluss**: Gesamtergebnis mit Statusmeldung

Verbinden Sie sich über die `DeploymentHub` und abonnieren Sie die Session ID:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/deploymentHub")
  .build();

connection.on("DeploymentProgress", (data) => {
  console.log(`${data.phase}: ${data.message} (${data.percentComplete}%)`);
});

await connection.start();
await connection.invoke("SubscribeToDeployment", sessionId);
```

---

## Fehlerbehandlung

| HTTP Status | Bedeutung |
|-------------|-----------|
| 200 | Erfolgreich |
| 400 | Ungültige Anfrage (Produkt nicht gefunden, aktives Deployment existiert, leere Stack-Konfiguration) |
| 401 | Nicht authentifiziert |
| 403 | Nicht autorisiert (fehlende Permission) |
| 404 | Product Deployment nicht gefunden (bei GET-Requests) |

### ContinueOnError-Verhalten

| `continueOnError` | Bei Stack-Fehler |
|-------------------|-----------------|
| `true` (Standard) | Nächster Stack wird trotzdem deployed. Endstatus: `PartiallyRunning` |
| `false` | Deployment wird abgebrochen. Verbleibende Stacks bleiben auf `Pending`. Endstatus: `Failed` |
