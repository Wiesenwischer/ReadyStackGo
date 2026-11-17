# Phase: Product Deployment UX Improvements

## Ziel

Die Product Deployment Ansicht während des Deployments verbessern: Die kompakte Stack-Status-Liste (links) beibehalten und um eine detaillierte Fortschrittsanzeige pro Stack (rechts) ergänzen — wiederverwendet vom Single-Stack Deployment Layout.

## Analyse

### Aktuelle Situation

**DeployProduct Deploying View** (`DeployProduct.tsx`, Zeile 632-787):
- Zentriertes, einspaltiges Layout
- Stack-Status-Liste: Jeder Stack mit Icon (pending/deploying/running/failed) und Badge
- Kleiner Detail-Block unterhalb mit Phase + Message des aktuellen Stacks
- Init Container Logs am unteren Rand
- Connection Status Indikator

**DeployStack Deploying View** (`DeployStack.tsx`, Zeile 432-527):
- Zentriertes, einspaltiges Layout
- Progress Bar mit Prozentanzeige und Phase-Label
- Status Message mit aktuellem Service-Fortschritt
- Service-Counter pro Phase (Images: 3/5, Services: 2/4, etc.)
- Init Container Logs
- Connection Status Indikator

### Rahmenbedingungen

- **Sequentielles Deployment**: Stacks werden nacheinander deployed, **nicht parallel**. Das Backend (`DeployProductHandler`) orchestriert dies bereits sequentiell — ein Stack muss abgeschlossen sein bevor der nächste beginnt.
- **Detail-Ansicht = Einzel-Stack-Deployment**: Die rechte Spalte soll exakt so viel Platz einnehmen und dieselben Informationen anzeigen wie beim Deployment eines einzelnen Stacks. Kein verkleinertes oder vereinfachtes Layout — volle Darstellung.

### Problem

Während eines Product Deployments fehlt die detaillierte Fortschrittsanzeige pro Stack. Der Nutzer sieht nur:
- Welcher Stack gerade deployed wird (Status-Icon)
- Eine einzeilige Phase-Info unterhalb der Liste

Es fehlt:
- Progress Bar pro Stack (Prozent-Fortschritt)
- Service-Counter (Images: 2/3, Services: 1/2)
- Aktueller Service-Name
- Phase-Details (Pulling Images → Starting Services → ...)

### Bug-Fix (bereits implementiert)

**Root Cause**: `DeployStackHandler` sendete `DeploymentCompleted`/`DeploymentFailed` SignalR Events nach jedem einzelnen Stack — auf dem **gleichen** Session-ID wie das Product Deployment. Frontend interpretierte das erste `isComplete: true` als Abschluss des gesamten Products.

**Fix**: `SuppressNotification` Flag unterdrückt jetzt auch die SignalR Final-Events (nicht nur In-App Notifications). Per-Stack Progress-Updates (PullingImages, StartingServices) fließen weiterhin.

## Features / Schritte

- [ ] **Feature 1: Split-View Layout für Deploying State** – Zweispaltiges Layout mit Stack-Liste links und Detail-Ansicht rechts
  - Betroffene Dateien: `DeployProduct.tsx`
  - Abhängig von: -
- [ ] **Feature 2: Shared DeploymentProgressPanel Komponente** – Wiederverwendbare Progress-Ansicht aus DeployStack extrahieren
  - Betroffene Dateien: Neue Komponente `components/deployments/DeploymentProgressPanel.tsx`, `DeployStack.tsx` (refactored), `DeployProduct.tsx`
  - Abhängig von: Feature 1
- [ ] **Feature 3: Stack-Auswahl in der Liste** – Klick auf einen Stack in der Liste zeigt dessen Detail-Fortschritt rechts
  - Betroffene Dateien: `DeployProduct.tsx`
  - Abhängig von: Feature 2
- [ ] **Feature 4: E2E Tests und Screenshots** – Playwright Tests für den neuen Split-View
  - Betroffene Dateien: `e2e/product-deployment.spec.ts`
  - Abhängig von: Feature 3

## UI-Design

### Layout-Konzept (Deploying State)

```
┌─────────────────────────────────────────────────────────────────────┐
│  Deploying Product...                                               │
│  Deploying E2E Platform v1.0.0 to default                         │
├───────────────────────────┬─────────────────────────────────────────┤
│                           │                                         │
│  STACK OVERVIEW           │  STACK DETAIL                           │
│                           │                                         │
│  ○ Frontend    [Pending]  │  Deploying: Backend                     │
│  ● Backend    [Deploying] │                                         │
│  ○ Database   [Pending]   │  ┌─── Progress ──────────────────────┐  │
│                           │  │ Pulling Images          67%       │  │
│  1/3 stacks completed     │  │ ████████████░░░░░░               │  │
│  ┌─── Overall ──────────┐ │  └───────────────────────────────────┘  │
│  │ ███░░░░░░  33%       │ │                                         │
│  └──────────────────────┘ │  Images: 2 / 3                          │
│                           │  (current: redis:7-alpine)               │
│  ● Live updates           │                                         │
│                           │  ┌─── Init Container Logs ───────────┐  │
│                           │  │ backend-init:                      │  │
│                           │  │ > Running migrations...            │  │
│                           │  │ > Applied 3 migrations             │  │
│                           │  └───────────────────────────────────┘  │
│                           │                                         │
│                           │  ● Live updates                         │
├───────────────────────────┴─────────────────────────────────────────┤
```

### Responsive Verhalten

- **Desktop (lg+)**: Zweispaltiges Grid (`lg:grid-cols-3` — Liste 1fr, Detail 2fr). Die rechte Detail-Spalte nimmt 2/3 der Breite ein und zeigt die **identische** Darstellung wie beim Einzel-Stack Deployment (Progress Bar, Phase, Service-Counter, Init Logs — volle Größe, nicht komprimiert).
- **Tablet/Mobile**: Einspaltig, Stack-Liste oben, Detail-Panel darunter (wie aktuell, nur mit erweiterter Detail-Ansicht)

### Stack-Auswahl Verhalten

1. **Automatisch**: Der aktuell deploying Stack wird automatisch selektiert
2. **Manuell**: Klick auf einen Stack in der Liste zeigt dessen letzten bekannten Fortschritt
3. **Visueller Indikator**: Selected Stack hat hervorgehobenen Rahmen (ring-2 ring-brand-500)
4. **Completed Stacks**: Zeigen eine Zusammenfassung (Dauer, Service-Count, Erfolg/Fehler)

### Stack-Detail Panel Inhalte

Je nach Stack-Status unterschiedliche Darstellung:

**Status: Pending**
```
Waiting to deploy...
This stack will be deployed after the current stack completes.
```

**Status: Deploying**
- Phase Label (Pulling Images / Initializing Containers / Starting Services)
- Progress Bar mit Prozent
- Service-Counter (Format: "Images: 2/3" oder "Services: 1/2")
- Aktueller Service-Name (font-mono)
- Status Message
- Init Container Logs (falls vorhanden)

**Status: Running (Completed)**
```
✓ Deployed successfully
  3 services started
  Duration: 12s
```

**Status: Failed**
```
✗ Deployment failed
  Error: <error message>
  2/3 services started before failure
```

## Technische Umsetzung

### DeploymentProgressPanel Komponente

Extrahiert aus `DeployStack.tsx` Deploying State (Zeile 432-527):

```typescript
interface DeploymentProgressPanelProps {
  progressUpdate: DeploymentProgressUpdate | null;
  initContainerLogs: Record<string, string[]>;
  connectionState: ConnectionState;
  stackName?: string;
  // Optional: für completed/failed Stacks
  completedInfo?: {
    success: boolean;
    serviceCount: number;
    errorMessage?: string;
  };
}

function DeploymentProgressPanel({
  progressUpdate,
  initContainerLogs,
  connectionState,
  stackName,
  completedInfo,
}: DeploymentProgressPanelProps) {
  // Wiederverwendet die Progress Bar, Service Counter,
  // Init Container Logs und Connection Status aus DeployStack
}
```

### State Management in DeployProduct

Erweiterte States für per-Stack Tracking:

```typescript
// Neuer State: per-Stack Progress-Updates speichern
const [perStackProgress, setPerStackProgress] = useState<
  Record<string, DeploymentProgressUpdate>
>({});

// Neuer State: per-Stack Init Container Logs
const [perStackLogs, setPerStackLogs] = useState<
  Record<string, Record<string, string[]>>
>({});

// Neuer State: selektierter Stack für Detail-Ansicht
const [selectedStack, setSelectedStack] = useState<string | null>(null);
```

### SignalR Event Routing

Der `handleDeploymentProgress` Callback muss erweitert werden:

```typescript
const handleDeploymentProgress = useCallback((update: DeploymentProgressUpdate) => {
  if (update.sessionId !== deploymentSessionIdRef.current) return;

  setProgressUpdate(update);

  if (update.phase === 'ProductDeploy' && update.currentService) {
    // Product-level: neuer Stack beginnt
    setCurrentDeployingStack(update.currentService);
    setSelectedStack(update.currentService); // Auto-select
    setStackStatuses(prev => ({
      ...prev,
      [update.currentService!]: 'deploying'
    }));
  } else if (currentDeployingStack) {
    // Stack-level: Detail-Update für aktuellen Stack
    setPerStackProgress(prev => ({
      ...prev,
      [currentDeployingStack]: update
    }));
  }
}, [currentDeployingStack]);
```

### Init Container Log Routing

```typescript
const handleInitContainerLog = useCallback((log: InitContainerLogEntry) => {
  if (log.sessionId !== deploymentSessionIdRef.current) return;

  if (currentDeployingStack) {
    setPerStackLogs(prev => ({
      ...prev,
      [currentDeployingStack]: {
        ...(prev[currentDeployingStack] || {}),
        [log.containerName]: [
          ...(prev[currentDeployingStack]?.[log.containerName] || []),
          log.logLine
        ]
      }
    }));
  }
}, [currentDeployingStack]);
```

## Offene Punkte

- [ ] Soll der Detail-Panel auch für bereits abgeschlossene Stacks eine Zusammenfassung zeigen? → **Ja** (Duration, Service-Count, Erfolg/Fehler)
- [ ] Soll die Stack-Liste anklickbar sein (manueller Wechsel)? → **Ja**, mit Auto-Select für den aktuell deployenden Stack

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Deployment-Reihenfolge | A) Parallel, B) Sequentiell | B | User-Vorgabe: Stacks nacheinander, nicht parallel. Backend macht das bereits so. |
| Layout-Ansatz | A) Split-View (Liste + Detail nebeneinander), B) Accordion (Detail unterhalb jedes Stacks), C) Tabs (ein Tab pro Stack) | A | User-Wunsch: Stack-Übersicht links, Detail-Ansicht rechts auf dem Bildschirm. |
| Spalten-Verhältnis | A) 1:1, B) 1:2, C) 1:3 | B (1:2) | Detail-Panel soll so viel Platz einnehmen wie beim Einzel-Stack Deployment. Liste ist kompakt, braucht weniger Platz. |
| Shared Component | A) Copy-Paste aus DeployStack, B) Extrahierte Komponente | B | DRY-Prinzip. DeployStack und DeployProduct nutzen die gleiche Progress-Darstellung. |
| Progress-Speicherung | A) Nur aktueller Stack, B) Alle Stacks im State | B | Ermöglicht Rückblick auf bereits abgeschlossene Stacks. |
