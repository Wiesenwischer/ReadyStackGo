# Phase: Orphaned Container Recovery

## Ziel

Wenn RSGO mit gelöschten Volumes (frische Datenbank) gestartet wird, aber Docker-Container von früheren Deployments noch laufen, sind diese Container "verwaist" — sie haben `rsgo.*`-Labels, aber es gibt keinen passenden `Deployment`-Eintrag in der Datenbank. Das System zeigt sie bereits als "Orphaned" an (amber Badge im Container-View), bietet aber keine Aktionen an.

Ziel ist es, dem User zwei Aktionen für verwaiste Container-Gruppen anzubieten:

1. **Repair (Reconcile)**: Container einem Deployment-Eintrag zuordnen, sodass sie wieder im Deployment-View sichtbar und verwaltbar sind
2. **Bulk Remove**: Alle Container einer verwaisten Stack-Gruppe auf einmal löschen

## Analyse

### Bestehende Architektur

**Orphaned Detection (bereits vorhanden):**
- `GetContainerContextHandler` erkennt verwaiste Stacks: Container mit `rsgo.stack`-Label, aber `IDeploymentRepository.GetByStackName()` gibt `null` zurück
- `StackContextInfo.DeploymentExists = false` kennzeichnet die Orphaned-Stacks
- UI zeigt `OrphanedBadge` (amber) im Container-View (List, Stack, Product Views)
- Endpoint: `GET /api/containers/context?environment={envId}`

**Container Labels (von DeploymentEngine gesetzt):**
- `rsgo.stack` — Stack-Name (z.B. `myapp-db`)
- `rsgo.context` — Service-Name innerhalb des Stacks
- `rsgo.environment` — Environment-ID
- `rsgo.lifecycle` — `init` oder `service`
- `rsgo.maintenance` — `ignore` (für Helper-Container)

**Bestehende Recovery-Mechanismen:**
- `DeploymentRecoveryService` (Startup): Markiert stuck `Installing`/`Upgrading` Deployments als `Failed` — berührt keine Docker-Container
- `ProductDeploymentHealthSyncService` (60s Loop): Synct Status zwischen `ProductDeployment` und `Deployment` — nur für Stacks die eine `DeploymentId` haben
- `DeploymentCompletedHandler`: Erstellt `ProductDeployment` via `CreateFromExternalDeployment()` wenn ein Deployment completed und kein ProductDeployment existiert
- `DeploymentRemovedHandler`: Synct `ProductStackDeployment` Status auf `Removed`

**Re-Adoption Path (bereits vorhanden):**
- `ProductDeployment.CreateFromExternalDeployment()` erstellt ein `ProductDeployment` direkt im `Running`-Status mit einem bereits fertigen Stack
- Wird heute von `DeploymentCompletedHandler` genutzt — kann als Basis für die Repair-Logik dienen

**Container-Entfernung (bereits vorhanden):**
- `IDockerService.RemoveContainerAsync(envId, containerId, force)` — einzelnen Container löschen
- `DeploymentEngine.RemoveStackAsync()` — alle Container mit `rsgo.stack == stackName` entfernen
- Kein öffentliches "remove by stack label" API — nur über den Deployment-Flow

### Betroffene Bounded Contexts

- **Domain**: Keine Änderungen (CreateFromExternalDeployment reicht aus)
- **Application**: Neue Commands/Handlers für Repair und Bulk Remove
- **Infrastructure**: Ggf. neue Methode auf `IDockerService` zum Entfernen nach Label
- **API**: Neue Endpoints
- **WebUI**: Aktions-Buttons im Container-View für Orphaned Stacks

### Gap-Analyse

| Szenario | Heute | Ziel |
|----------|-------|------|
| Orphaned Container sichtbar | Ja (amber Badge) | Ja |
| Orphaned Container reparieren | Nein | Ja — Deployment-Eintrag erstellen |
| Orphaned Container bulk-löschen | Nein | Ja — alle Container einer Stack-Gruppe entfernen |
| Orphaned Product reparieren | Nein | Ja — ProductDeployment + Deployment erstellen |
| Startup-Reconciliation | Nein (nur stuck Deployments) | Nein (manuell via UI) |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: Remove Orphaned Stack** – Alle Container einer verwaisten Stack-Gruppe entfernen
  - Neuer Command: `RemoveOrphanedStackCommand(EnvironmentId, StackName)`
  - Handler: Container mit `rsgo.stack == stackName` listen, `RemoveContainerAsync(force: true)` für jeden
  - Neuer Endpoint: `DELETE /api/containers/orphaned-stacks/{stackName}?environment={envId}`
  - Permission: `Deployments.Delete`
  - Abhängig von: -

- [ ] **Feature 2: Repair Orphaned Stack** – Deployment-Eintrag für verwaiste Container erstellen
  - Neuer Command: `RepairOrphanedStackCommand(EnvironmentId, StackName)`
  - Handler:
    1. Container mit `rsgo.stack == stackName` aus Docker listen
    2. Stack im Catalog suchen (über `rsgo.stack` Label-Value → `IProductCache`)
    3. `Deployment` Aggregat erstellen (Status: `Running`, Configs aus laufenden Containern ableiten)
    4. Optional: `ProductDeployment` via `CreateFromExternalDeployment()` erstellen wenn Product im Catalog gefunden
    5. Persistieren
  - Neuer Endpoint: `POST /api/containers/orphaned-stacks/{stackName}/repair?environment={envId}`
  - Permission: `Deployments.Create`
  - Response: Erfolg/Misserfolg + erstellte DeploymentId
  - Abhängig von: -

- [ ] **Feature 3: UI-Aktionen im Container-View** – Buttons für Remove und Repair bei Orphaned Stacks
  - In allen drei Views (List, Stack, Product): Aktions-Buttons bei Orphaned Badge anzeigen
  - "Repair" Button: Ruft Repair-Endpoint auf, refresht View
  - "Remove" Button: Confirmation Dialog, ruft Remove-Endpoint auf, refresht View
  - Stack-View: Buttons im `StackGroupHeader`
  - List-View: Buttons in der Orphaned-Badge Zeile
  - Product-View: Buttons im "Unknown Product" Bereich
  - Abhängig von: Feature 1, Feature 2

- [ ] **Feature 4: Tests** – Unit Tests für Handler, Edge Cases
  - `RemoveOrphanedStackHandlerTests`: Happy path, Stack nicht gefunden, Environment nicht gefunden
  - `RepairOrphanedStackHandlerTests`: Happy path, Catalog-Match gefunden, kein Catalog-Match, Container ohne Labels
  - Edge Cases: Was passiert wenn Container während Repair/Remove gestoppt werden?
  - Abhängig von: Feature 1, Feature 2

- [ ] **Phase abschließen** – Build, Tests, Commit, PR gegen main

## Test-Strategie

- **Unit Tests**: Handler-Tests mit gemockten Services (IDockerService, IProductCache, Repositories)
- **Integration Tests**: Keine neuen nötig (kein neues DB-Schema)
- **Manueller Test**: Container bauen, Product deployen, Volumes löschen, Container neustarten, Orphaned Stacks reparieren/entfernen

## Offene Punkte

- [x] Soll Repair auch ohne Catalog-Match funktionieren? → **Ja**, Deployment-only Fallback
- [x] Soll es eine "Repair All" Bulk-Aktion geben oder nur pro Stack? → **Beides**: Per-Stack + "Repair All"
- [x] Braucht es eine Confirmation für Repair oder nur für Remove? → **Beides** braucht Confirmation

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Scope | Nur Remove, Nur Repair, Beides | Beides | User-Entscheidung |
| Startup-Auto-Reconciliation | Automatisch, Manuell via UI | Manuell via UI | Automatisches Re-Adopten kann unerwünscht sein wenn User absichtlich sauber starten will |
| Repair-Basis | Catalog-Lookup, User-Input, Nur Deployment | Catalog-Lookup + Fallback | Wenn der Stack im Catalog ist, können alle Metadaten abgeleitet werden; ohne Catalog nur ein generischer Deployment-Eintrag |
| Roadmap-Einordnung | Eigenes Epic | Eigenes Epic | User-Entscheidung: Standalone Epic ohne Versionsnummer |
