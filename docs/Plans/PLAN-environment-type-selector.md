<!-- GitHub Epic: #282 -->
# Phase: Environment Type Selector (v0.49 Nachbesserung)

## Ziel

Add Environment von einer einzelnen Seite mit Inline-Typ-Selektor auf einen Zwei-Schritt-Flow umbauen: erst Typ wählen, dann auf einer separaten Seite die typspezifische Konfiguration eingeben. Analog zum bestehenden AddStackSource-Pattern.

## Analyse

### Bestehende Architektur

**Aktuell (AddEnvironment.tsx):**
- Alles auf einer Seite: Type-Toggle-Buttons (Docker Socket / SSH Tunnel) + konditionelles Formular
- Route: `/environments/add`
- Formular wechselt Felder je nach `envType` State

**Ziel-Pattern (AddStackSourceSelect.tsx):**
- Typ-Auswahl auf Index-Seite mit `TypeSelector` Komponente
- Route: `/settings/stack-sources/add` → Auswahl → `/add/local`, `/add/git`, `/add/catalog`
- Jeder Typ hat eine eigene Seite mit spezifischem Formular

**TypeSelector Komponente:**
- `packages/ui-generic/src/components/ui/TypeSelector.tsx`
- Generisch, unterstützt 2/3/4 Spalten, Icons, Labels, Descriptions
- Bereits vorhanden und wiederverwendbar

### Betroffene Bounded Contexts
- **Domain**: Keine Änderung
- **Application**: Keine Änderung
- **Infrastructure**: Keine Änderung
- **API**: Keine Änderung
- **WebUI (rsgo-generic)**: Seiten-Split, neue Routes, TypeSelector-Nutzung

## AMS UI Counterpart

- [x] **Ja** — AMS-Counterpart wird als eigenes PLAN file im AMS Repo angelegt
  - RSGO PLAN referenziert: `docs/Plans/PLAN-environment-type-selector.md` (dieses File)
  - AMS PLAN: `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-environment-type-selector.md`
  - AMS PLAN enthält: ConsistentUI-spezifische Implementierungsschritte + Verweis auf dieses RSGO-PLAN
  - Zeitpunkt: Im nächsten AMS-Release
  - AMS unterstützt aktuell nur DockerSocket — der Type Selector ermöglicht spätere SSH-Erweiterung

## Features / Schritte

- [ ] **Feature 1: AddEnvironment aufteilen — Type Selector Seite**
  - Bestehende `AddEnvironment.tsx` wird zur Typ-Auswahl-Seite (analog `AddStackSourceSelect.tsx`)
  - Nutzt `TypeSelector` Komponente mit zwei Optionen:
    - Local Docker Socket: Icon, "Direct connection via Unix socket"
    - SSH Tunnel: Icon, "Remote Docker via SSH connection"
  - Nach Auswahl: Navigation zu `/environments/add/docker-socket` oder `/environments/add/ssh-tunnel`
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Environments/AddEnvironment.tsx` (Rewrite → Typ-Selector)
  - Pattern-Vorlage: `packages/ui-generic/src/pages/Settings/StackSources/AddStackSourceSelect.tsx`

- [ ] **Feature 2: AddDockerSocketEnvironment Seite**
  - Neue Seite für Docker Socket Konfiguration
  - Felder: Environment Name, Docker Socket Path
  - Test Connection Button
  - Route: `/environments/add/docker-socket`
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Environments/AddDockerSocketEnvironment.tsx` (neu)
    - `apps/rsgo-generic/src/App.tsx` (Route hinzufügen)

- [ ] **Feature 3: AddSshTunnelEnvironment Seite**
  - Neue Seite für SSH Tunnel Konfiguration
  - Felder: Environment Name, SSH Host, Port, Username, Auth Method (Key/Password), Secret, Remote Socket Path
  - Test Connection Button
  - Route: `/environments/add/ssh-tunnel`
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Environments/AddSshTunnelEnvironment.tsx` (neu)
    - `apps/rsgo-generic/src/App.tsx` (Route hinzufügen)

- [ ] **Feature 4: Routes und Imports aktualisieren**
  - `/environments/add` → Type Selector
  - `/environments/add/docker-socket` → Docker Socket Form
  - `/environments/add/ssh-tunnel` → SSH Tunnel Form
  - Environments-Liste: "Add Environment" Link bleibt auf `/environments/add`
  - Betroffene Dateien:
    - `apps/rsgo-generic/src/App.tsx`

- [ ] **Phase abschließen** — Tests grün, PR gegen main

## Test-Strategie
- **E2E Tests**: Type Selector Navigation, Docker Socket Creation, SSH Tunnel Form, Cancel/Back Navigation
- **Unit Tests**: Keine nötig (rein UI-Refactoring, keine Business Logic)

## Offene Punkte
- (keine)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Pattern | A: Inline-Toggle beibehalten, B: TypeSelector → Sub-Routes | **B** | Konsistent mit AddStackSource-Pattern, bessere UX bei wachsender Typ-Anzahl (Docker TCP, Remote Agent kommen noch) |
| Route-Schema | A: `/environments/add/:type`, B: `/environments/add/docker-socket` etc. | **B** | Explizite Routes, konsistent mit Stack Sources Pattern |
