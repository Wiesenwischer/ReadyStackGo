# Phase: WebUI Headless Refactoring

## Ziel

Die WebUI so refactoren, dass Business-Logik (API-Calls, State-Management, SignalR, Validierung) in einem framework-unabhängigen `core/`-Layer liegt und die Darstellungsschicht (`ui/`) austauschbar ist. Damit kann ein Downstream-Fork (z.B. für Corporate Identity mit eigenem UI-Framework) den `ui/`-Layer komplett ersetzen und `core/` 1:1 wiederverwenden.

**Nicht-Ziel**: Die bestehende UI visuell verändern. Nach dem Refactoring muss die Anwendung identisch aussehen und funktionieren.

## Analyse

### Ist-Zustand

Die WebUI ist eine React SPA mit Tailwind CSS. Aktuell gibt es bereits eine gute Trennung beim API-Layer (`src/api/`), aber Seiten und Komponenten vermischen Business-Logik mit Darstellung.

| Layer | Dateien | Kopplung | Status |
|-------|---------|----------|--------|
| `api/` | 12 | Kein React, reines TypeScript | Sauber — verschieben |
| `hooks/` | 4 | SignalR + React vermischt | Aufteilen |
| `context/` | 4 | React Context + API-Calls | Aufteilen |
| `components/` | ~25 | Business-Logik + Tailwind inline | Aufteilen |
| `pages/` | ~20 | State + API + SignalR + Tailwind | Aufteilen |
| `layout/` | 5 | Sidebar/Header + Tailwind | UI-Layer |

### Detailanalyse pro Layer

#### `api/` — Sofort wiederverwendbar
- 12 Dateien, kein React-Import
- `client.ts`: HTTP-Abstraktion (`apiGet<T>`, `apiPost<T>`, etc.) mit Auth-Header-Injection
- Pro Domain: `deployments.ts`, `environments.ts`, `stacks.ts`, `registries.ts`, `apiKeys.ts`, etc.
- **Aktion**: 1:1 nach `core/api/` verschieben

#### `hooks/` — SignalR muss raus
- `useDeploymentHub.ts`: SignalR-Connection für Deployment-Progress (Events, Session-ID, Refs)
- `useHealthHub.ts`: SignalR-Connection für Health-Monitoring
- `useModal.ts`: Einfacher Boolean-Toggle
- `useGoBack.ts`: Navigation-History
- **Problem**: SignalR-Infrastruktur lebt in React-Hooks statt in einem Service
- **Aktion**: SignalR-Connection-Management → `core/services/`, React-Hooks als dünne Wrapper behalten

#### `context/` — API-Logik extrahieren
- `AuthContext.tsx`: Login/Logout, Token-Persistenz (localStorage), User-State
- `EnvironmentContext.tsx`: Environments laden, aktives Environment setzen, localStorage-Persistenz
- `SidebarContext.tsx`: Sidebar-Zustand (expand/hover/mobile)
- `ThemeContext.tsx`: Dark-Mode-Toggle
- **Problem**: Auth und Environment rufen API direkt auf und persistieren in localStorage
- **Aktion**: Auth-Service und Environment-Service → `core/services/`, Contexts delegieren nur

#### `components/` — Gemischte Qualität
- **Gut**: `components/variables/` hat Factory-Pattern (`VariableInput.tsx` → `inputs/*`)
- **Schlecht**: `components/settings/RegistrySettings.tsx` (460 Zeilen) — CRUD-State, API-Calls, Modals, Tailwind alles in einer Datei
- **Ähnlich**: `StackSourcesSettings.tsx`, `TlsSettings.tsx`
- **Aktion**: Store-Hooks extrahieren, Komponenten werden rein darstellend

#### `pages/` — Hauptaufwand
- Komplexeste Seiten: `DeployStack.tsx` (~300 Zeilen, State-Machine + SignalR + Form), `StackCatalog.tsx`, Wizard-Steps
- Pattern: `useState`-Soup + `useEffect`-Chains + API-Calls + Tailwind-Rendering in einer Datei
- **Aktion**: Pro Seite einen Store-Hook extrahieren, Seite wird reine Composition

### Ziel-Architektur

```
src/ReadyStackGo.WebUi/src/
├── core/                          ← Framework-unabhängig (kein React, kein Tailwind)
│   ├── api/                       ← HTTP-Client + Domain-APIs (existiert, verschieben)
│   │   ├── client.ts
│   │   ├── deployments.ts
│   │   ├── environments.ts
│   │   ├── stacks.ts
│   │   ├── registries.ts
│   │   ├── apiKeys.ts
│   │   └── ...
│   ├── services/                  ← Infrastruktur-Services
│   │   ├── AuthService.ts         ← Login/Logout/Token (aus AuthContext)
│   │   ├── EnvironmentService.ts  ← Load/Select Environment (aus EnvironmentContext)
│   │   ├── DeploymentHub.ts       ← SignalR Deployment-Connection (aus useDeploymentHub)
│   │   └── HealthHub.ts           ← SignalR Health-Connection (aus useHealthHub)
│   └── types/                     ← Shared TypeScript Interfaces
│       ├── deployment.ts
│       ├── environment.ts
│       ├── stack.ts
│       └── ...
│
├── ui/                            ← React + Tailwind (austauschbar)
│   ├── components/                ← UI-Primitives und Domain-Komponenten
│   │   ├── variables/             ← Variable-Input-Komponenten (bestehend)
│   │   ├── settings/              ← Nur UI, kein State (refactored)
│   │   ├── health/                ← Health-Cards (bestehend, leicht)
│   │   ├── header/                ← Header-Komponenten (bestehend)
│   │   └── ...
│   ├── hooks/                     ← React-Wrapper für core/services
│   │   ├── useAuth.ts             ← Delegiert an AuthService
│   │   ├── useEnvironments.ts     ← Delegiert an EnvironmentService
│   │   ├── useDeploymentHub.ts    ← Delegiert an DeploymentHub
│   │   ├── useHealthHub.ts        ← Delegiert an HealthHub
│   │   ├── useModal.ts            ← Bleibt (rein UI)
│   │   └── useGoBack.ts           ← Bleibt (rein UI)
│   ├── context/                   ← React Contexts (dünn, delegieren an services)
│   │   ├── AuthContext.tsx
│   │   ├── EnvironmentContext.tsx
│   │   ├── SidebarContext.tsx
│   │   └── ThemeContext.tsx
│   ├── layouts/                   ← App-Shell (bestehend)
│   ├── pages/                     ← Nur Composition, Logik in Hooks
│   └── stores/                    ← Page-level State-Hooks
│       ├── useDeployStore.ts      ← State-Machine für DeployStack
│       ├── useRegistryStore.ts    ← CRUD-State für Registries
│       ├── useCatalogStore.ts     ← Filter/Suche für Stack-Katalog
│       ├── useStackSourceStore.ts ← CRUD-State für Stack Sources
│       └── ...
│
├── App.tsx
├── main.tsx
└── index.css
```

### Konvention für Store-Hooks

Jeder Store-Hook folgt dem gleichen Pattern:

```typescript
// ui/stores/useRegistryStore.ts
import { useState, useEffect, useCallback } from 'react';
import { getRegistries, createRegistry, ... } from '../../core/api/registries';
import type { Registry, CreateRegistryRequest } from '../../core/types/registry';

export function useRegistryStore() {
  const [registries, setRegistries] = useState<Registry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => { ... }, []);
  const create = useCallback(async (req: CreateRegistryRequest) => { ... }, []);
  const remove = useCallback(async (id: string) => { ... }, []);

  useEffect(() => { load(); }, [load]);

  return { registries, isLoading, error, create, remove, ... } as const;
}
```

Die zugehörige Page wird dann rein darstellend:

```tsx
// ui/pages/Settings/RegistrySettings.tsx
export default function RegistrySettings() {
  const store = useRegistryStore();
  const modal = useModal();

  if (store.isLoading) return <LoadingSpinner />;
  return (
    <>
      <RegistryTable registries={store.registries} onDelete={store.remove} />
      <RegistryModal isOpen={modal.isOpen} onSubmit={store.create} onClose={modal.close} />
    </>
  );
}
```

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten — von innen nach außen:

- [ ] **Feature 1: Core API Layer** — `api/` nach `core/api/` verschieben, Types extrahieren nach `core/types/`
  - Betroffene Dateien: Alle 12 `api/*.ts`, neue `core/types/*.ts`
  - Import-Pfade in allen Konsumenten aktualisieren
  - Abhängig von: -
  - Tests: Build + `npm run dev` muss funktionieren, kein visueller Unterschied

- [ ] **Feature 2: Core Services** — SignalR und Auth/Environment-Logik in `core/services/` extrahieren
  - `AuthService.ts` aus `AuthContext.tsx` (Login, Logout, Token-Management, localStorage)
  - `EnvironmentService.ts` aus `EnvironmentContext.tsx` (Load, Select, Persist)
  - `DeploymentHub.ts` aus `useDeploymentHub.ts` (SignalR-Connection, Event-Subscriptions)
  - `HealthHub.ts` aus `useHealthHub.ts` (SignalR-Connection)
  - Betroffene Dateien: `context/AuthContext.tsx`, `context/EnvironmentContext.tsx`, `hooks/useDeploymentHub.ts`, `hooks/useHealthHub.ts`
  - Abhängig von: Feature 1
  - Tests: Login, Environment-Wechsel, Deployment-Progress, Health-Updates müssen funktionieren

- [ ] **Feature 3: Store-Hooks für Settings-Seiten** — Business-Logik aus den schwersten Komponenten extrahieren
  - `useRegistryStore.ts` aus `RegistrySettings.tsx` (460 Zeilen → Store + UI)
  - `useStackSourceStore.ts` aus `StackSourcesSettings.tsx`
  - `useTlsStore.ts` aus `TlsSettings.tsx`
  - `useApiKeyStore.ts` aus der API-Key-Settings-Seite
  - Betroffene Dateien: `components/settings/*`, `pages/Settings/*`
  - Abhängig von: Feature 1
  - Tests: Alle Settings-Seiten funktional testen (CRUD-Operationen, Modals, Error-States)

- [ ] **Feature 4: Store-Hooks für Deployment-Seiten** — Komplexeste Business-Logik extrahieren
  - `useDeployStore.ts` aus `DeployStack.tsx` (State-Machine: loading → configure → deploying → success/error)
  - `useRollbackStore.ts` aus der Rollback-Seite
  - `useUpgradeStore.ts` aus der Upgrade-Seite
  - `useRemoveStore.ts` aus der Remove-Seite
  - Betroffene Dateien: `pages/Deployments/*`
  - Abhängig von: Feature 2 (wegen SignalR-Dependency)
  - Tests: Deploy-Flow end-to-end, Progress-Updates, Error-Handling

- [ ] **Feature 5: Store-Hooks für restliche Seiten** — Katalog, Environments, Monitoring, Wizard
  - `useCatalogStore.ts` für StackCatalog + ProductDetail
  - `useEnvironmentStore.ts` für Environments-Seite
  - `useHealthDashboardStore.ts` für Health-Dashboard
  - `useContainerStore.ts` für Container-Monitoring
  - `useWizardStore.ts` für Wizard-Steps
  - Betroffene Dateien: Alle verbleibenden `pages/*`
  - Abhängig von: Feature 1, 2
  - Tests: Navigation, Filter, Wizard-Flow

- [ ] **Feature 6: Cleanup und Dokumentation** — Alte Importe bereinigen, README für Downstream-Forks
  - Alte Import-Pfade final prüfen
  - `core/README.md` mit API-Dokumentation für Fork-Teams
  - Bestehende E2E-Tests müssen alle grün sein
  - Abhängig von: Feature 1-5

- [ ] **Phase abschließen** — Alle Tests grün, PR gegen main

## Offene Punkte

Alle offenen Punkte wurden durch die Distribution-Specs (`docs/specs/rsgo-distributions/`) beantwortet:

- [x] Soll `core/` als eigenständiges npm-Package extrahiert werden? → **Ja, als `@rsgo/core` in pnpm Monorepo**
- [x] Sollen die Store-Hooks in `ui/stores/` oder `ui/hooks/` leben? → **In `@rsgo/core/src/hooks/` als ViewModel-Hooks**
- [x] Brauchen wir ein State-Management-Library? → **Nein, plain React Hooks (useState/useCallback)**

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| State-Management | A) Zustand, B) Jotai, C) React Hooks | **C) React Hooks** | Zustand/react-query in package.json aber ungenutzt. Distribution-Spec-Beispiele nutzen plain Hooks. |
| Core-Packaging | A) Ordner-Trennung, B) npm-Package | **B) npm-Package (`@rsgo/core`)** | Distribution-Specs erfordern Package-Level-Trennung für Downstream-Consumption |
| Store-Location | A) `ui/stores/`, B) `ui/hooks/` | **Weder — `@rsgo/core/src/hooks/`** | Spec: Core exportiert ViewModel-Hooks, Pages importieren sie |
| SignalR-Abstraktion | A) Event-Emitter, B) Callback-basiert, C) Observable | **B) Callback-basiert** | Entspricht RSGO-FRONTEND-ARCH.md Abschnitt 3.2 |

## Aktualisierte Architektur (Distribution-Ready)

Die Ordner-basierte `core/` + `ui/` Trennung wird zu einem **pnpm Workspaces Monorepo** mit npm-Packages erweitert:

```
src/ReadyStackGo.WebUi/                      Monorepo-Root
  pnpm-workspace.yaml
  packages/
    core/                                     @rsgo/core
      src/
        api/                                  ← verschoben aus src/api/
        types/                                ← extrahiert aus api/ und pages/
        hooks/                                ← ViewModel-Hooks (aus pages + contexts)
        realtime/                             ← SignalR-Services (aus hooks/)
        services/                             ← Auth/Environment-Logik (aus contexts)
    ui-generic/                               @rsgo/ui-generic
      src/
        components/                           ← verschoben aus src/components/
        context/                              ← dünne React Contexts
        hooks/                                ← UI-only (useModal, useGoBack)
        layouts/                              ← verschoben aus src/layout/
        pages/                                ← verschoben aus src/pages/
  apps/
    rsgo-generic/                             App-Einstiegspunkt
      src/main.tsx, App.tsx
      vite.config.ts                          → Build nach Api/wwwroot
```

Siehe vollständigen Plan in `C:\Users\MAD\.claude\plans\deep-baking-brook.md` (v0.26 Abschnitt).
