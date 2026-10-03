# Phase: Maintenance Mode Pages

## Ziel

Enter/Exit Maintenance bekommt eigene Seiten mit Bestätigung und Rückmeldung — analog zu StopProduct/RestartProduct. Aktuell sind es nur Buttons auf der ProductDeploymentDetail-Seite ohne Bestätigung, ohne Übersicht betroffener Stacks, ohne Ergebnis-Anzeige.

## Analyse

### Bestehende Architektur

**Pattern-Vorlage: StopProduct** (identischer Flow)
- `useStopProductStore` (`packages/core/src/hooks/useStopProductStore.ts`) — State Machine: loading → confirm → stopping → success/error
- `StopProduct.tsx` (`packages/ui-generic/src/pages/Deployments/StopProduct.tsx`) — 5 States: Loading, Error (no deployment), Confirm, Stopping, Success, Error (partial)
- Route: `/stop-product/:productDeploymentId` in `App.tsx`
- ProductDeploymentDetail verlinkt per `<Link to={/stop-product/...}>`

**Bestehende Maintenance-API** (bereits vorhanden, kein Backend-Änderung nötig):
- `enterProductMaintenanceMode(environmentId, productDeploymentId, request)` in `packages/core/src/api/health.ts`
- `exitProductMaintenanceMode(environmentId, productDeploymentId)` in `packages/core/src/api/health.ts`
- Response: `ChangeProductOperationModeResponse` mit `success`, `message`, `previousMode`, `newMode`

**Aktuell**: `handleEnterMaintenance`/`handleExitMaintenance` in `useProductDeploymentDetailStore` — direkte API-Aufrufe ohne eigene Seite.

### Betroffene Bounded Contexts
- **Domain**: Keine Änderung (Maintenance-Logik existiert bereits)
- **Application**: Keine Änderung (Handler existiert bereits)
- **Infrastructure**: Keine Änderung
- **API**: Keine Änderung (Endpoint existiert bereits)
- **WebUI (rsgo-generic)**: Neue Seiten + Store + Route

## AMS UI Counterpart

- [x] **Ja (deferred)** — AMS-Counterpart wird später geplant
  - Begründung: AMS UI hat noch keinen vollständigen Maintenance-Support (siehe PLAN-maintenance-mode-status-fix.md)
  - Erinnerung: Nach Umsetzung der AMS-Maintenance-Fixes als Follow-up planen

## Features / Schritte

- [x] **Feature 1: `useMaintenanceProductStore` Hook** — Neuer Store analog zu `useStopProductStore`
  - State Machine: `loading` → `confirm` → `entering`/`exiting` → `success` → `error`
  - Lädt ProductDeployment-Daten für Confirm-Ansicht
  - Ruft `enterProductMaintenanceMode` / `exitProductMaintenanceMode` auf
  - Gibt Ergebnis mit `ChangeProductOperationModeResponse` zurück
  - Betroffene Dateien:
    - `packages/core/src/hooks/useMaintenanceProductStore.ts` (neu)
    - `packages/core/src/index.ts` (Export hinzufügen)
  - Pattern-Vorlage: `packages/core/src/hooks/useStopProductStore.ts`

- [x] **Feature 2: `EnterMaintenanceProduct.tsx` Seite** — Bestätigungsseite für Enter Maintenance
  - States: Loading, Error, Confirm (mit Stack-Liste + Warnung), Entering (Spinner), Success, Error (mit Details)
  - Confirm zeigt: Product Name, Version, Environment, Stacks, Service-Count
  - Warning: "This will stop all containers and enter maintenance mode"
  - Success: "Maintenance Mode Activated" mit Mode-Übergang (Normal → Maintenance)
  - Route: `/enter-maintenance/:productDeploymentId`
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Deployments/EnterMaintenanceProduct.tsx` (neu)
    - `apps/rsgo-generic/src/App.tsx` (Route hinzufügen)
  - Pattern-Vorlage: `packages/ui-generic/src/pages/Deployments/StopProduct.tsx`

- [x] **Feature 3: `ExitMaintenanceProduct.tsx` Seite** — Bestätigungsseite für Exit Maintenance
  - States: Loading, Error, Confirm, Exiting (Spinner), Success, Error
  - Confirm zeigt: Product Name, aktuelle Maintenance-Info (Trigger, Grund, Seit wann)
  - Warning: "This will restart all containers and exit maintenance mode"
  - Success: "Maintenance Mode Deactivated" mit Mode-Übergang (Maintenance → Normal)
  - Route: `/exit-maintenance/:productDeploymentId`
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Deployments/ExitMaintenanceProduct.tsx` (neu)
    - `apps/rsgo-generic/src/App.tsx` (Route hinzufügen)
  - Pattern-Vorlage: `packages/ui-generic/src/pages/Deployments/StopProduct.tsx`

- [x] **Feature 4: ProductDeploymentDetail — Buttons durch Links ersetzen**
  - "Enter Maintenance" Button → `<Link to={/enter-maintenance/...}>` Button-Style
  - "Exit Maintenance" Button → `<Link to={/exit-maintenance/...}>` Button-Style
  - `handleEnterMaintenance`/`handleExitMaintenance` und `modeActionLoading`/`modeActionError` aus Store entfernen
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx`
    - `packages/core/src/hooks/useProductDeploymentDetailStore.ts` (Maintenance-Handler entfernen)

- [-] **Feature 5: Unit Tests** — Store-Logik ist rein React Hooks (useState/useEffect/useCallback), kein testbarer Business-Logic-Layer. Tests über E2E abgedeckt.
  - Test: `useMaintenanceProductStore` State Transitions
  - Betroffene Dateien:
    - `tests/ReadyStackGo.UnitTests/` (falls Store-Logik testbar)

- [ ] **Dokumentation & Website**
- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Test-Strategie
- **Unit Tests**: Store State Machine (loading → confirm → entering → success/error)
- **E2E Tests**: Enter Maintenance Flow, Exit Maintenance Flow, Cancel-Button, Error-State

## Offene Punkte
- (keine)

## Entscheidungen
| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Scope | Product + Stack, Nur Product | **Nur Product** | User-Entscheidung — Stack-Level bleibt Button-Klick |
| Confirm-Felder | Grund + Bestätigung, Nur Bestätigung, Grund + Checkboxen | **Nur Bestätigung** | User-Entscheidung — analog StopProduct Pattern |
| Pattern | Eigenes Pattern, StopProduct-Analogie | **StopProduct-Analogie** | Bewährtes Pattern, konsistente UX |
