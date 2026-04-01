# ReadyStackGo – Deployment Precheck

## Motivation

Aktuell startet ein Deployment sofort nach Klick auf "Deploy" — ohne vorherige Prüfung der Infrastruktur-Voraussetzungen. Wenn ein Image nicht pullbar ist, ein Port belegt ist oder ein Volume fehlt, erfährt der User das erst **mitten im Deployment**, nachdem alte Container bereits entfernt wurden (Point of No Return in der DeploymentEngine).

Das Deployment Precheck Feature prüft alle Voraussetzungen **vor** dem eigentlichen Deployment und zeigt dem User eine klare Übersicht: Was ist OK, was warnt, was blockiert.

---

## Zielgruppe

- **Admins** die über die UI deployen und vor dem Deployment wissen wollen ob alles passt
- **CI/CD Pipelines** die über Hooks deployen und bei Fehlern abbrechen können (Dry-Run)

---

## User Stories

### US-1: Precheck vor UI-Deployment
> Als Admin möchte ich vor einem Deployment eine Voraussetzungsprüfung sehen, damit ich Probleme erkennen und beheben kann bevor Container entfernt werden.

### US-2: Precheck-Ergebnisse verstehen
> Als Admin möchte ich für jede Prüfung sehen ob sie bestanden hat (✓), eine Warnung auslöst (⚠) oder fehlgeschlagen ist (✗), damit ich informiert entscheiden kann.

### US-3: Deployment blockieren bei Fehlern
> Als Admin möchte ich dass der Deploy-Button bei kritischen Fehlern deaktiviert ist, damit ich nicht versehentlich ein kaputtes Deployment starte.

### US-4: Deployment trotz Warnungen erlauben
> Als Admin möchte ich bei Warnungen (nicht-kritisch) trotzdem deployen können, damit der Precheck nicht unnötig blockiert.

### US-5: Precheck über Hooks API (Dry-Run)
> Als CI/CD-Pipeline möchte ich einen Precheck als Dry-Run ausführen können, damit ich bei Fehlern den Deploy abbrechen kann bevor etwas passiert.

---

## Precheck-Regeln

Jede Regel hat einen **Severity Level**:
- **Error** (✗): Blockiert Deployment. Muss behoben werden.
- **Warning** (⚠): Deployment möglich, aber Risiko. User entscheidet.
- **OK** (✓): Prüfung bestanden.

### Regel 1: Image-Verfügbarkeit
- **Prüfung**: Für jeden Service im Stack: Ist das Image lokal vorhanden ODER von der Registry pullbar?
- **Methode**: `IDockerService.ImageExistsAsync()` für lokale Prüfung. Falls nicht lokal: `IRegistryAccessChecker.CheckAccessAsync()` für Registry-Erreichbarkeit + Auth.
- **Error**: Image nicht lokal UND Registry nicht erreichbar / Auth fehlgeschlagen.
- **Warning**: Image nur lokal vorhanden (kein Registry-Pull möglich → funktioniert, aber nicht reproduzierbar).
- **OK**: Image lokal vorhanden ODER Registry-Pull möglich.
- **Anzeige**: Pro Service: Image-Referenz, Status, Fehlermeldung falls Error.

### Regel 2: Port-Konflikte
- **Prüfung**: Welche Host-Ports werden vom Stack benötigt? Sind diese Ports bereits von anderen Containern belegt?
- **Methode**: Aus ServiceTemplates die Port-Mappings extrahieren (`hostPort:containerPort`). Via `IDockerService.ListContainersAsync()` alle laufenden Container und deren Port-Bindings abfragen. Ports des eigenen Stacks (bei Upgrade/Redeploy) ausschließen.
- **Error**: Port ist belegt durch einen Container eines **anderen** Stacks.
- **OK**: Port ist frei ODER gehört zum selben Stack (Upgrade-Szenario).
- **Anzeige**: Belegter Port, Name des blockierenden Containers/Stacks.

### Regel 3: Variable-Validierung
- **Prüfung**: Sind alle Required Variables ausgefüllt? Erfüllen die Werte die Validierungs-Constraints (Pattern, Min/Max, Type)?
- **Methode**: Bestehender `StackVariableResolver.ValidateVariables()` und `DeploymentPrerequisiteValidationService`.
- **Error**: Required Variable fehlt oder Validierung fehlgeschlagen.
- **OK**: Alle Variablen gültig.
- **Anzeige**: Variable-Name, Regel die verletzt wurde, aktueller Wert (maskiert bei Passwörtern).

### Regel 4: Network-Verfügbarkeit
- **Prüfung**: Existiert das Management-Network `rsgo-net`? Können benötigte Custom-Networks erstellt werden?
- **Methode**: `IDockerService.EnsureNetworkAsync()` im Dry-Run-Modus (nur prüfen, nicht erstellen).
- **Warning**: Custom-Network existiert bereits und gehört einem anderen Stack.
- **OK**: Networks können erstellt werden oder existieren bereits korrekt.
- **Anzeige**: Network-Name, Status.

### Regel 5: Volume-Status
- **Prüfung**: Werden Named Volumes referenziert die bereits existieren (Daten bleiben erhalten)? Werden neue Volumes erstellt?
- **Methode**: `IDockerService.ListVolumesRawAsync()` / `InspectVolumeAsync()` für existierende Volumes.
- **Warning**: Volume existiert bereits → wird wiederverwendet (Daten bleiben). Bei Erstinstallation ist das unerwartet.
- **OK**: Volume existiert nicht (wird neu erstellt) ODER existiert und gehört zum selben Stack (Upgrade).
- **Anzeige**: Volume-Name, "wird erstellt" vs. "wird wiederverwendet (existierend)".

### Regel 6: Bestehende Deployment-Erkennung
- **Prüfung**: Existiert bereits ein Deployment für diesen Stack in dieser Environment? Wenn ja: Welcher Status?
- **Methode**: `IDeploymentRepository.FindByStackAndEnvironmentAsync()` o.ä.
- **Warning**: Bestehendes Deployment vorhanden → wird Upgrade (Container werden entfernt und neu erstellt).
- **Error**: Bestehendes Deployment im Status `Installing` oder `Upgrading` → Concurrent-Deployment nicht erlaubt.
- **OK**: Kein bestehendes Deployment (Fresh Install).
- **Anzeige**: Status des bestehenden Deployments, Aktion (Fresh Install / Upgrade / Blocked).

---

## Architektur

### Domain Layer

**Neues Value Object:** `PrecheckResult`
```
PrecheckResult
├── Checks: IReadOnlyList<PrecheckItem>
├── HasErrors: bool (mindestens ein Error)
├── HasWarnings: bool (mindestens eine Warning)
├── CanDeploy: bool (!HasErrors)
└── Summary: string ("6 checks passed, 1 warning" etc.)

PrecheckItem
├── Rule: string (z.B. "ImageAvailability", "PortConflict")
├── Severity: PrecheckSeverity (OK, Warning, Error)
├── Title: string (kurze Beschreibung)
├── Detail: string? (zusätzliche Info, z.B. welcher Port/Container)
└── ServiceName: string? (betroffener Service, falls zutreffend)

PrecheckSeverity
├── OK
├── Warning
└── Error
```

### Application Layer

**Neuer Query:** `RunDeploymentPrecheckQuery`
```
Input: EnvironmentId, StackId, Variables (Dictionary)
Output: PrecheckResult
```

**Neuer Handler:** `RunDeploymentPrecheckHandler`
- Orchestriert alle Precheck-Regeln
- Sammelt Ergebnisse von allen Checks
- Gibt aggregiertes PrecheckResult zurück

**Interface:** `IDeploymentPrecheckRule`
```csharp
public interface IDeploymentPrecheckRule
{
    string RuleName { get; }
    Task<IEnumerable<PrecheckItem>> CheckAsync(PrecheckContext context, CancellationToken ct);
}
```

**PrecheckContext** enthält alle Informationen die Rules brauchen:
```
PrecheckContext
├── Environment: Environment
├── StackDefinition: StackDefinition
├── Variables: Dictionary<string, string>
├── ExistingDeployment: Deployment? (falls Upgrade)
└── ResolvedServices: IReadOnlyList<ServiceTemplate>
```

**Rule-Implementierungen** (jeweils eine Klasse):
- `ImageAvailabilityRule`
- `PortConflictRule`
- `VariableValidationRule`
- `NetworkAvailabilityRule`
- `VolumeStatusRule`
- `ExistingDeploymentRule`

### Infrastructure Layer

- Rule-Implementierungen die Docker-Zugriff brauchen (`ImageAvailabilityRule`, `PortConflictRule`, `NetworkAvailabilityRule`, `VolumeStatusRule`) leben im Infrastructure Layer
- Reine Domain-Regeln (`VariableValidationRule`, `ExistingDeploymentRule`) leben im Application Layer
- DI: Alle Rules als `IEnumerable<IDeploymentPrecheckRule>` registriert

### API Layer

**Neuer Endpoint:** `POST /api/environments/{environmentId}/stacks/{stackId}/precheck`
```
Request Body:
{
  "variables": { "DB_HOST": "localhost", "DB_PORT": "5432" }
}

Response Body:
{
  "canDeploy": true,
  "hasErrors": false,
  "hasWarnings": true,
  "summary": "6 checks passed, 1 warning",
  "checks": [
    {
      "rule": "ImageAvailability",
      "severity": "ok",
      "title": "All images accessible",
      "detail": null,
      "serviceName": null
    },
    {
      "rule": "VolumeStatus",
      "severity": "warning",
      "title": "Volume 'pg-data' already exists",
      "detail": "Volume will be reused. Existing data will be preserved.",
      "serviceName": "postgres"
    }
  ]
}
```

**Permission:** `Deployments.Create` (selbe Permission wie Deploy, da es eine Vorstufe ist)

**Hooks API Erweiterung:** `POST /api/hooks/deploy` mit optionalem `dryRun: true` Parameter
```json
{
  "stackId": "my-stack",
  "environmentId": "...",
  "variables": { ... },
  "dryRun": true
}
```
Response bei `dryRun: true`: PrecheckResult statt DeploymentResponse.

### WebUI (rsgo-generic)

#### Änderung am bestehenden Deploy-Flow

Aktuell:
```
DeployStack Page → Variable eingeben → Deploy klicken → Deployment startet sofort
```

Neu:
```
DeployStack Page → Variable eingeben → "Check & Deploy" klicken
  → Precheck läuft (Loading-Spinner)
  → Precheck-Ergebnis-Panel erscheint:
    ✓ Image Availability: All 3 images accessible
    ✓ Port Conflicts: No conflicts detected
    ✓ Variables: All required variables provided
    ✓ Networks: rsgo-net available
    ⚠ Volumes: pg-data already exists (will be reused)
    ✓ Deployment: Fresh install

  Falls HasErrors:
    Deploy-Button disabled, Fehlermeldungen prominent
    [Cancel]  [Re-Check]

  Falls nur Warnings oder OK:
    [Cancel]  [Deploy Now ▶]
```

#### Neue Komponenten

1. **PrecheckPanel** (`components/deployment/PrecheckPanel.tsx`)
   - Zeigt Liste aller PrecheckItems
   - Gruppiert nach Severity (Errors oben, dann Warnings, dann OK)
   - Collapsible: OK-Items zusammengeklappt, Errors/Warnings aufgeklappt
   - Icons: ✗ rot, ⚠ gelb, ✓ grün
   - "Re-Check" Button zum erneuten Prüfen nach Korrekturen

2. **PrecheckItem** (`components/deployment/PrecheckItem.tsx`)
   - Einzelne Check-Zeile mit Icon, Title, optional Detail
   - Expandable für Details (z.B. welcher Container belegt den Port)

#### Anpassung bestehender Seite

- **DeployStack.tsx**: Neuer Zustand zwischen "Variablen eingegeben" und "Deployment gestartet"
  - State: `idle` → `prechecking` → `precheck-done` → `deploying`
  - Bei `precheck-done`: PrecheckPanel anzeigen
  - Deploy-Button nur aktiv wenn `canDeploy === true`

#### Store-Erweiterung

- **useDeployStackStore.ts**: Neue Actions:
  - `runPrecheck()` → API Call → State Update
  - `precheckResult: PrecheckResult | null`
  - `isPrechecking: boolean`

#### API-Client-Erweiterung

- **deployments.ts**: Neue Funktion:
  - `runPrecheck(environmentId, stackId, variables): Promise<PrecheckResult>`

---

## Nicht im Scope

- **Automatische Behebung** von Problemen (z.B. Port freigeben, Image pullen). Der Precheck zeigt nur an, der User behebt manuell.
- **Precheck für Rollback/Remove** — nur für Deploy/Upgrade.
- **Speicherung von Precheck-Ergebnissen** — sie sind flüchtig und werden nicht persistiert.
- **Precheck als eigenständige Seite** — er ist Teil des Deploy-Flows, keine separate Navigation.

---

## Test-Strategie

### Unit Tests
- **PrecheckResult**: Aggregation (HasErrors, HasWarnings, CanDeploy, Summary)
- **Jede Rule einzeln**:
  - `ImageAvailabilityRule`: Image lokal vorhanden, Image nicht vorhanden + Registry OK, Image nicht vorhanden + Registry fail
  - `PortConflictRule`: Kein Konflikt, Konflikt mit anderem Stack, eigener Stack (Upgrade), mehrere Konflikte
  - `VariableValidationRule`: Alle OK, Required fehlt, Pattern-Validierung fehlgeschlagen
  - `NetworkAvailabilityRule`: rsgo-net existiert, rsgo-net fehlt, Custom-Network-Konflikt
  - `VolumeStatusRule`: Neues Volume, Existierendes Volume (Upgrade), Existierendes Volume (Fresh Install)
  - `ExistingDeploymentRule`: Kein bestehendes, Running (Upgrade), Installing (Blocked), Failed (Retry)

### Integration Tests
- **RunDeploymentPrecheckHandler**: Alle Rules zusammen mit Mock-Docker-Service
- **API Endpoint**: Precheck Request/Response Roundtrip

### E2E Tests
- **Happy Path**: Stack deployen mit bestandenem Precheck
- **Error Case**: Stack mit belegtem Port → Precheck zeigt Error → Deploy-Button disabled
- **Warning Case**: Existierendes Volume → Warning → Deploy trotzdem möglich

---

## Offene Fragen

1. Soll der Precheck automatisch laufen wenn der User den Deploy-Flow betritt (eager), oder erst auf Knopfdruck (lazy)?
2. Soll die Precheck-API auch für den Hooks-Deploy genutzt werden (immer automatisch vor Deploy), oder nur als opt-in Dry-Run?
3. Timeout für den gesamten Precheck? (Image-Registry-Checks können langsam sein)
4. Soll der Precheck die Ergebnisse cachen (z.B. 30s) um bei erneutem Klick schneller zu sein?
