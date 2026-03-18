<!-- GitHub Epic: #340 -->
# Phase: Transient Variables

## Ziel

Manifest-Autoren sollen Variablen als `transient: true` markieren können. Diese Variablen werden beim Deployment abgefragt, aber **nicht in der Datenbank gespeichert**. Beim Redeployment müssen sie neu eingegeben werden. Typischer Use Case: Admin-Passwörter, Setup-Credentials, oder andere sensitive Einmalwerte.

## Analyse

### Bestehende Architektur

**Variable Definition** (Manifest → Domain):
- `RsgoVariable` (YAML Model) → `Variable` (Domain Model) → `StackVariable` (Frontend DTO)
- Variable Properties: Name, Label, DefaultValue, IsRequired, Type, Pattern, Group, Order, etc.
- Kein `transient` Flag vorhanden

**Variable Persistierung** (Deployment):
- `Deployment.SetVariables(dict)` speichert ALLE Variablen als JSON in `VariablesJson` Column
- `Deployment.GetRedeploymentData()` gibt alle gespeicherten Variablen für Redeployment zurück
- Webhook Redeploy: stored Variables als Basis + Webhook Variables als Override

**Variable Flow**:
```
Manifest (YAML, transient: true)
  → RsgoManifestParser → Variable (IsTransient)
  → API → StackVariable (isTransient)
  → UI (markiert, User gibt Wert ein)
  → DeployStackCommand (alle Variablen inkl. transient)
  → DeploymentService
    ├─ Docker: alle Variablen (inkl. transient) an Container übergeben ✓
    └─ DB: nur non-transient Variablen persistieren ✓ (NEU)
```

### Betroffene Bounded Contexts

- **Domain**: `Variable` + `RsgoVariable` — neues `IsTransient` Flag
- **Application**: `DeployStackHandler`, `DeployViaHookHandler`, `RedeployStackHandler` — Transient-Filterung
- **Infrastructure**: `DeploymentService` — Variablen vor Persistierung filtern; `RsgoManifestParser` — `transient` aus YAML parsen
- **API**: `ListProductsHandler`, `GetProductHandler` — `isTransient` im DTO exponieren
- **WebUI**: Variable-Input-Komponenten — visuelles Label für transiente Variablen

## AMS UI Counterpart

- [x] **Ja (deferred)** — AMS UI Variable-Input muss `isTransient` ebenfalls anzeigen

## Features / Schritte

- [ ] **Feature 1: Domain — IsTransient Flag** – `Variable` und `RsgoVariable` erweitern
  - Betroffene Dateien:
    - `src/ReadyStackGo.Domain/StackManagement/Stacks/Variable.cs` — `bool IsTransient` Property
    - `src/ReadyStackGo.Domain/StackManagement/Manifests/RsgoVariable.cs` — `bool? Transient` YAML Property
  - Pattern-Vorlage: `IsRequired` Property im gleichen File
  - Abhängig von: -

- [ ] **Feature 2: Manifest Parsing** – `transient: true` aus YAML lesen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Parsing/RsgoManifestParser.cs` — Mapping RsgoVariable.Transient → Variable.IsTransient
  - YAML Beispiel:
    ```yaml
    variables:
      ADMIN_PASSWORD:
        required: true
        type: Password
        label: "Admin Password"
        transient: true
    ```
  - Abhängig von: Feature 1

- [ ] **Feature 3: API DTOs** – `isTransient` in StackVariable DTO exponieren
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Stacks/ListStacks/ListStacksQuery.cs` — `StackVariableItem`
    - `src/ReadyStackGo.Application/UseCases/Stacks/ListProducts/ListProductsQuery.cs` — `StackVariableItem`
    - `src/ReadyStackGo.WebUi/packages/core/src/api/stacks.ts` — `StackVariable` Interface
  - Abhängig von: Feature 1

- [ ] **Feature 4: Deployment — Transient Variablen nicht persistieren** – Filterung vor DB-Speicherung
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/Deployment/DeploymentService.cs` — vor `SetVariables()` filtern
  - Logik:
    - Alle Variablen (inkl. transient) an Docker übergeben
    - Nur non-transient Variablen in `Deployment.SetVariables()` speichern
    - Stack Definition muss zur Deployment-Zeit verfügbar sein um zu wissen welche transient sind
  - Abhängig von: Feature 1

- [ ] **Feature 5: Redeploy Handling** – Transient Variablen bei Redeploy neu abfragen
  - Betroffene Dateien:
    - `src/ReadyStackGo.Application/UseCases/Hooks/DeployStack/DeployViaHookCommand.cs`
    - `src/ReadyStackGo.Application/UseCases/Hooks/RedeployStack/RedeployStackCommand.cs`
    - UI: Redeploy-Dialog muss transiente Variablen als required anzeigen (keine gespeicherten Werte)
  - Webhook: Transient Variables MÜSSEN im Webhook Request mitgegeben werden
  - Abhängig von: Feature 4

- [ ] **Feature 6: UI — Transient Label und Redeploy Warning**
  - Betroffene Dateien:
    - `src/ReadyStackGo.WebUi/packages/ui-generic/src/components/variables/VariableInput.tsx` — Label/Icon für transient
    - Deploy-Seiten: Hinweis "This value will not be saved"
    - Redeploy-Seiten: Transiente Variablen als leer und required anzeigen
  - Abhängig von: Feature 3

- [ ] **Feature 7: Unit Tests**
  - Variable Domain: IsTransient Validation
  - Manifest Parsing: transient: true/false/default
  - DeploymentService: Transient Variables nicht in DB
  - Redeploy: Transient Variables fehlen in stored data
  - Webhook: Transient Variables required
  - Abhängig von: Feature 1-5

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**:
  - Variable.IsTransient Parsing und Defaults
  - DeploymentService filtert transient Vars vor Persistierung
  - Redeployment gibt keine transient Vars zurück
  - Webhook ohne transient Vars → Fehler oder Warnung
- **E2E Tests**:
  - Deploy mit transient Variable → Variable wird an Docker übergeben aber nicht in DB gespeichert
  - Redeploy → transient Variable muss neu eingegeben werden

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| YAML Syntax | A: `transient: true`, B: `persist: false`, C: `storage: none` | A | Klarste Semantik, konsistent mit `required: true` Pattern |
| Default-Wert für transient | A: false (Default), B: true | A | Backward-kompatibel, bestehende Variablen bleiben persistent |
| Webhook ohne transient Var | A: Fehler, B: Warnung + Deploy ohne Var, C: Silent skip | A | Transient Vars sind oft required; fehlen sie → Deployment scheitert eh am Container |
| Wo filtern | A: DeploymentService, B: Deployment Entity | A | Service hat Zugriff auf Stack Definition mit IsTransient Info |
