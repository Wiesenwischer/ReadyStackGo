# Phase: Deployments Overview with Product Info (v0.28)

## Ziel

Die Deployments-Übersicht zeigt aktuell nur einzelne Stack-Deployments. Product Deployments (aus v0.27) sind auf der Übersichtsseite nicht sichtbar. Ziel ist eine eigene **"Product Deployments"**-Sektion oberhalb der Stack-Liste, die Product-Name, Version, DeploymentName, Status, Stack-Fortschritt und Aktionen anzeigt. Zusätzlich wird das in v0.27 fehlende `listProductDeployments()` Frontend-API-Funktion ergänzt und die bereits implementierte Product-Level DeploymentName Refactoring ist Bestandteil dieser Version.

## Analyse

### Bestehende Architektur

**Backend (bereits vorhanden — kein Backend-Code nötig):**
- `ListProductDeploymentsEndpoint` (`GET /api/environments/{envId}/product-deployments`) existiert bereits
- `ListProductDeploymentsHandler` filtert removed Deployments und gibt `ProductDeploymentSummaryDto` zurück
- `ProductDeploymentSummaryDto` enthält: ProductDeploymentId, ProductGroupId, ProductName, ProductDisplayName, ProductVersion, DeploymentName, Status, CreatedAt, CompletedAt, ErrorMessage, TotalStacks, CompletedStacks, FailedStacks, CanUpgrade, CanRemove

**Frontend (Lücken):**
- `listProductDeployments()` API-Funktion fehlt in `api/deployments.ts` — TypeScript-Interfaces existieren bereits (`ProductDeploymentSummaryDto` ist nicht vorhanden)
- `Deployments.tsx` zeigt nur Stack-Deployments via `listDeployments()`
- Keine Product-Deployment-Integration in der Overview

**Bereits implementiert (in dieser Session):**
- Product-Level `DeploymentName` Refactoring — Single DeploymentName statt per-Stack Names
- Backend, Frontend, Tests sind bereits fertig — noch nicht committed

### Betroffene Bounded Contexts
- **Domain**: Keine Änderungen
- **Application**: Keine Änderungen (ListProductDeployments existiert)
- **Infrastructure**: Keine Änderungen
- **API**: Keine Änderungen (Endpoint existiert)
- **WebUI**: Hauptarbeitsbereich — neue Sektion in Deployments.tsx + API-Funktion

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: Product-Level DeploymentName** – Bereits implementiert in dieser Session
  - Betroffene Dateien: Domain, Application, API, EF Core, Frontend, Tests (alle bereits geändert)
  - Abhängig von: -

- [ ] **Feature 2: Frontend API Funktion `listProductDeployments()`** – Neue Funktion + TypeScript Interface
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/src/api/deployments.ts` — `listProductDeployments()` Funktion + `ListProductDeploymentsResponse`, `ProductDeploymentSummaryDto` Interfaces
  - Pattern-Vorlage: `listDeployments()` in gleicher Datei (Zeile 134-136)
  - Abhängig von: -

- [ ] **Feature 3: Product Deployments Sektion in Deployments.tsx** – Neue Sektion oberhalb der Stack-Liste
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/Deployments.tsx` — Product Deployment Cards rendern
  - Layout:
    - "Product Deployments" Überschrift (analog zu "Deployed Stacks")
    - Product Cards mit: ProductDisplayName, Version-Badge, Status-Badge, DeploymentName (mono), Stack-Fortschritt (3/5 stacks), Aktionen
    - Status-Farben: Running=Grün, Deploying/Upgrading=Brand, Failed=Rot, PartiallyRunning=Gelb
    - Aktionen: "Details" → `/catalog/{productId}`, "Upgrade" (wenn `canUpgrade`), "Remove" (wenn `canRemove`)
    - Empty State: "No product deployments" mit Link zum Catalog
  - Pattern-Vorlage: `DeploymentRow` Komponente in gleicher Datei
  - Abhängig von: Feature 2

- [ ] **Feature 4: Seitenüberschrift anpassen** – Text von "deployed stacks" auf "deployments" generalisieren
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/src/pages/Deployments/Deployments.tsx` — Subtitle-Text anpassen
  - Abhängig von: Feature 3

- [ ] **Phase abschließen** – Build, Tests, Commit, PR gegen main

## Test-Strategie
- **Unit Tests**: Keine neuen Backend-Tests nötig (API existiert bereits)
- **Integration Tests**: Keine neuen nötig
- **Manueller Test**: Docker-Container bauen, Product deployen, Deployments-Overview prüfen

## Offene Punkte
- [ ] Soll die Product-Sektion auch bei 0 Product Deployments angezeigt werden? (Vorschlag: Nein, nur wenn mindestens 1 existiert)
- [ ] Soll die Empty State Nachricht "No deployments" angepasst werden wenn nur Stacks aber keine Products vorhanden sind? (Vorschlag: Nein, aktuelles Verhalten beibehalten)

## Entscheidungen
| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Product-Sektion Position | Oben, Unten, Tab | Oben | User-Entscheidung: Separate Sektion oberhalb der Stacks |
| Roadmap-Einordnung | Standalone v0.28, Bundle | Standalone v0.28 | User-Entscheidung |
| DeploymentName Refactoring | Eigene Version, Hier einordnen | Hier einordnen | Wurde in gleicher Session implementiert, passt thematisch |
