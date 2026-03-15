# Plan: Maintenance Mode Status-Anzeige korrigieren

## Problem

Wenn ein Product Deployment in den Wartungsmodus wechselt, werden die Container gestoppt (`StopStackContainersAsync`). Es gibt drei zusammenhängende Probleme:

### Problem 1: Product-Detail zeigt "Running" im Maintenance Mode

```
ams.project  v3.1.0-pre  [Running]  [Maintenance]
```

- **STATUS: Running** — irreführend, Container sind gestoppt
- **OPERATION MODE: Maintenance** — korrekt

### Problem 2: Child-Stacks zeigen "Normal" statt "Maintenance"

```
ams-project-projectexporting  [Unhealthy]
Operation Mode: Normal        ← falsch, Parent ist in Maintenance
```

`ChangeProductOperationModeHandler` stoppt die Container aller Child-Stacks, **propagiert aber den Maintenance-Mode nicht** zu den einzelnen `Deployment`-Entities. Daher:
- Stack-Detail zeigt `OperationMode: "Normal"` obwohl Container gestoppt sind
- Health Collector berechnet Health mit `OperationMode.Normal` → gestoppte Container = **Unhealthy** statt erwartetes **Degraded**

### Problem 3: Health History zeigt Maintenance als "Unhealthy"

Die Health History Timeline zeigt Maintenance-Perioden als rot (Unhealthy) statt als eigene Kategorie. Da der Stack `OperationMode.Normal` hat, werden gestoppte Container als Unhealthy bewertet — obwohl der Zustand erwartet und gewollt ist.

---

## Architektur-Hintergrund

Die Domain trennt bewusst zwei Dimensionen:

| Dimension | Feld | Bedeutung |
|-----------|------|-----------|
| **Lifecycle Status** | `ProductDeploymentStatus` / `DeploymentStatus` | Wo steht das Deployment im Lifecycle? (Deploying → Running → ...) |
| **Operation Mode** | `OperationMode` | Laufzeit-Zustand: Normal, Maintenance |

`EnterMaintenance()` ändert nur `OperationMode`, nicht den `Status`. Das ist architektonisch korrekt — `ExitMaintenance()` erfordert `IsOperational` (Running/PartiallyRunning).

### Root Cause

`ProductDeployment.EnterMaintenance()` und `ChangeProductOperationModeHandler` setzen nur den Product-Level `OperationMode`. Die Child-Stack `Deployment`-Entities behalten `OperationMode.Normal`. Dadurch:
1. UI zeigt falschen OperationMode für Stacks
2. Health Collector berechnet Health falsch (Normal + Container stopped = Unhealthy)
3. Health History zeichnet Maintenance als Unhealthy auf

---

## Lösung

### Gewählte Variante: B — Status-Badge überschreiben

Wenn `operationMode === "Maintenance"`:
- Status-Badge zeigt **"Stopped"** (orange) statt "Running" (grün)
- Maintenance-Badge bleibt daneben

**Ergebnis Product-Level:**
```
ams.project  v3.1.0-pre  [Stopped]  [Maintenance]
```

**Ergebnis Stack-Level:**
```
ams-project-projectexporting  [Stopped]  [Maintenance]
```

---

## Features

- [x] **Feature 1: Maintenance-Mode zu Child-Stacks propagieren**
  - `ChangeProductOperationModeHandler`: Beim `EnterMaintenance` auch `deployment.EnterMaintenance(trigger)` für alle Running Child-Stacks aufrufen
  - Beim `ExitMaintenance` auch `deployment.ExitMaintenance(source)` für alle Maintenance Child-Stacks aufrufen
  - Dadurch automatisch korrekt:
    - Stack-Detail zeigt `OperationMode: "Maintenance"`
    - Health Collector berechnet mit `OperationMode.Maintenance` → `MinimumHealthStatus = Degraded` (nicht Unhealthy)
    - Health Transitions zeichnen den Mode-Wechsel auf
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Deployments/ChangeProductOperationMode/ChangeProductOperationModeHandler.cs`

- [x] **Feature 2: Status-Badge-Logik bei Maintenance anpassen (UI)**
  - Wenn `operationMode === "Maintenance"`: Status-Badge "Stopped" (orange) anzeigen statt "Running" (grün)
  - Gilt für:
    - Product-Detail Header-Badges in `ProductDeploymentDetail.tsx`
    - Product-Detail Status-Card (STATUS-Kachel) in `ProductDeploymentDetail.tsx`
    - Stack-Detail Header in `DeploymentDetail.tsx`
    - Deployment-Listendarstellung in `Deployments.tsx`
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/Deployments.tsx`
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/pages/Deployments/DeploymentDetail.tsx` (falls vorhanden)

- [-] **Feature 3: Health History — Maintenance-Perioden kennzeichnen** (bereits implementiert, keine Änderungen nötig — Chart zeigt Maintenance in Blau wenn operationMode korrekt gesetzt ist)
  - Health History Timeline soll Maintenance-Perioden visuell unterscheidbar darstellen
  - Statt rot (Unhealthy) → eigene Farbe oder Muster für Maintenance (z.B. grau oder gelb gestreift)
  - Nach Feature 1 wird der Health Collector `OperationMode.Maintenance` korrekt erfassen → Health Transitions enthalten den Mode
  - Die Timeline-Komponente muss den `operationMode` in den Transitions auswerten
  - Betroffene Dateien:
    - Health History Komponente (zu identifizieren)
    - `packages/core/src/api/health.ts` (Transitions-Types)

- [x] **Feature 4: Unit Tests**
  - Test: `ChangeProductOperationModeHandler` propagiert Maintenance zu Child-Stacks
  - Test: `ChangeProductOperationModeHandler` propagiert ExitMaintenance zu Child-Stacks
  - Test: UI-Logik Status-Badge bei Maintenance
  - Betroffene Dateien:
    - `tests/ReadyStackGo.UnitTests/`

- [x] **Feature 5: AMS UI Impact-Check** — AMS UI ist betroffen: `ProductDeploymentDetail.tsx` zeigt `deployment.status` ohne `operationMode`-Berücksichtigung. Fix als separater Task im AMS-Repo.
  - AMS UI verwendet eigene Deployment-Detail-Komponenten
  - Prüfen ob das gleiche Problem dort existiert
  - Ggf. separaten Fix im AMS-Repo planen

---

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Fix-Ebene | Nur UI, Nur Backend, Beides | **Beides** | Backend muss Maintenance zu Child-Stacks propagieren, UI muss Status-Badge anpassen |
| Badge-Darstellung | A: OperationMode ersetzt Status, B: Status überschreiben, C: Kombiniert | **B** | User-Entscheidung: `[Stopped] [Maintenance]` zeigt beide Dimensionen klar |
| Maintenance-Propagation | A: Handler propagiert zu Deployments, B: API/Health prüft Parent, C: Computed Property | **A** | Sauberste Lösung — jeder Stack hat seinen eigenen korrekten OperationMode. Health, API, UI funktionieren automatisch korrekt |
