# Phase: WebUI Headless Store Hooks — Remaining Pages

## Ziel

Alle verbleibenden Seiten und Komponenten in `@rsgo/ui-generic` auf headless-konforme Architektur umstellen. Nach Abschluss darf **kein** UI-Code direkte API-Aufrufe machen — alle Daten fließen über Store-Hooks aus `@rsgo/core`.

**Ausgangslage:** 21 Store-Hooks in `@rsgo/core/src/hooks/` existieren bereits (Deployments, Settings-Listen, Health). 35 Dateien in `ui-generic` rufen API-Funktionen noch direkt auf.

**Nicht-Ziel:** Visuelle Änderungen. Funktionalität muss identisch bleiben.

## Analyse

### Bereits korrekt (21 Hooks vorhanden)

Deployment-Seiten, Settings-Listen (Registries, StackSources, TLS, API Keys), Health Dashboard, Notifications, Version Info — alle nutzen Store-Hooks aus `@rsgo/core`.

### Violations nach Gruppe

| Gruppe | Dateien | Violations | Neue Hooks | Erweiterte Hooks |
|--------|---------|------------|------------|------------------|
| Dashboard & Catalog | 3 | 3 | 3 | - |
| Monitoring | 5 | 7 | 4 | - |
| Environments | 4 | 4 | 1 | - |
| Settings CRUD-Subseiten | 7 | 7 | - | 3 |
| Profile & System | 3 | 3 | 3 | - |
| Wizard & Onboarding | 8 | 10 | 2 | - |
| Komponenten | 3 | 3 | 2 | - |
| Deep Imports | 2 | 2 | - | - |
| **Gesamt** | **35** | **39** | **15** | **3** |

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten und Impact — wichtigste/komplexeste zuerst:

- [x] **Feature 1: Monitoring Store-Hooks** — Containers, ContainerLogs, Volumes, ServiceHealthDetail
  - Neue Hooks:
    - `useContainerStore` — Liste, Start/Stop/Remove, Gruppierung nach Stack/Product (Business-Logik aus `Containers.tsx:170-246`), Orphan-Actions
    - `useContainerLogsStore` — Initial Fetch + Hub Integration
    - `useVolumeStore` — CRUD für Volumes + VolumeDetail + `useVolumeDetailStore` — Detail + Delete State Machine
    - `useServiceHealthDetailStore` — Health-Detail laden + Auto-Refresh
  - Betroffene Dateien: `pages/Monitoring/Containers.tsx`, `ContainerLogs.tsx`, `Volumes.tsx`, `VolumeDetail.tsx`, `ServiceHealthDetail.tsx`
  - Abhängig von: -

- [x] **Feature 2: Dashboard & Catalog Store-Hooks** — Dashboard, StackCatalog, ProductDetail
  - Neue Hooks:
    - `useDashboardStore` — Stats laden mit Polling
    - `useCatalogStore` — Products laden, Sync auslösen
    - `useProductDetailStore` — Product + Deployment + Upgrade-Check laden (Business-Logik aus `ProductDetail.tsx:51-86`)
  - Betroffene Dateien: `pages/Dashboard.tsx`, `pages/Catalog/StackCatalog.tsx`, `pages/Catalog/ProductDetail.tsx`
  - Abhängig von: -

- [x] **Feature 3: Settings CRUD-Subseiten** — Bestehende Hooks um CRUD-Actions erweitern
  - Erweiterte Hooks:
    - `useTlsStore` — Aktionen für `ResetToSelfSigned`, `UploadCertificate`, `ConfigureLetsEncrypt`
    - `useRegistryStore` — Aktionen für `AddRegistry`, `EditRegistry`, `DeleteRegistry` (getById, create, update, delete)
    - `useStackSourceStore` — Aktionen für `AddGitSource`, `AddLocalSource`, `AddFromCatalog`, `DeleteStackSource` (getById, create, delete, addFromCatalog)
  - Betroffene Dateien: `pages/Settings/Tls/ResetToSelfSigned.tsx`, `UploadCertificate.tsx`, `ConfigureLetsEncrypt.tsx`, `pages/Settings/Registries/AddRegistry.tsx`, `EditRegistry.tsx`, `DeleteRegistry.tsx`, `pages/Settings/StackSources/AddGitSource.tsx`, `AddLocalSource.tsx`, `AddFromCatalog.tsx`, `DeleteStackSource.tsx`
  - Abhängig von: -

- [x] **Feature 4: Environment Store-Hook** — Alle Environment-Seiten
  - Neuer Hook:
    - `useEnvironmentStore` — Liste, Default setzen, Create, Delete, getById
  - Betroffene Dateien: `pages/Environments/Environments.tsx`, `AddEnvironment.tsx`, `SetupEnvironment.tsx`, `DeleteEnvironment.tsx`
  - Abhängig von: -

- [ ] **Feature 5: Profile, System & Update Store-Hooks** — Profile, SystemInfo, UpdateStatus
  - Neue Hooks:
    - `useProfileStore` — Profil laden, Passwort ändern
    - `useSystemSettingsStore` — Health-Notification Settings (SystemInfo-Seite)
    - `useUpdateStore` — Update-Flow State-Machine (7 Phasen: idle → checking → available → updating → ...)
  - Betroffene Dateien: `pages/Profile/Profile.tsx`, `pages/Settings/System/SystemInfo.tsx`, `pages/UpdateStatus.tsx`
  - Abhängig von: -

- [ ] **Feature 6: Wizard & Onboarding Store-Hooks** — Gesamter Wizard- und Onboarding-Flow
  - Neue Hooks:
    - `useWizardStore` — Admin-Creation, Environment, StackSources, Registries, Install — mit WizardGuard
    - `useOnboardingStore` — Onboarding-Status, Steps, Org/Env/Sources/Registries — mit OnboardingGuard
  - Betroffene Dateien: `pages/Wizard/index.tsx`, `EnvironmentStep.tsx`, `StackSourcesStep.tsx`, `RegistriesStep.tsx`, `pages/Onboarding/index.tsx`, `OnboardingOrgStep.tsx`, `OnboardingEnvStep.tsx`, `OnboardingSourcesStep.tsx`, `components/wizard/WizardGuard.tsx`, `components/onboarding/OnboardingGuard.tsx`
  - Abhängig von: -

- [ ] **Feature 7: Komponenten-Hooks & Deep Import Cleanup** — HealthWidget, SetupHint, SqlServerConnectionBuilder, Deep Imports
  - Neue Hooks:
    - `useHealthWidgetStore` — Environment Health Summary + Health-Aggregation (Business-Logik aus `HealthWidget.tsx:25-30, 48-59`)
    - `useConnectionTestStore` — SQL Server Connection-Test
  - Deep Import Fixes:
    - `context/AuthContext.tsx` — `@rsgo/core/services/AuthService` → `@rsgo/core`
    - `context/EnvironmentContext.tsx` — `@rsgo/core/services/EnvironmentService` → `@rsgo/core`
  - Betroffene Dateien: `components/dashboard/HealthWidget.tsx`, `components/dashboard/SetupHint.tsx`, `components/variables/builders/SqlServerConnectionBuilder.tsx`, `components/health/HealthHistoryChart.tsx`, `context/AuthContext.tsx`, `context/EnvironmentContext.tsx`
  - Abhängig von: -

- [ ] **Feature 8: Final Audit & Cleanup** — Verifizierung, dass keine direkten API-Aufrufe mehr in ui-generic existieren
  - Automatischer Scan: `grep -r "apiGet\|apiPost\|apiPut\|apiDelete\|from.*api/" packages/ui-generic/src/` muss leer sein (außer Type-Imports)
  - Build prüfen: `pnpm build` und `tsc` sauber
  - Docker Container bauen und manuell testen
  - Abhängig von: Feature 1-7

- [ ] **Dokumentation & Phase abschließen** — Roadmap, Plan-Update, PR gegen main

## Offene Punkte

- [ ] Sollen Wizard/Onboarding-Hooks aufgeteilt werden (ein Hook pro Step) oder ein großer Hook pro Flow?
  - Empfehlung: Ein Hook pro Flow (`useWizardStore`, `useOnboardingStore`) — Steps teilen sich State (z.B. wizardStatus)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Hook-Erweiterung vs. neue Hooks | A) Neue Hooks für CRUD-Subseiten, B) Bestehende Hooks erweitern | **B) Erweitern** | `useRegistryStore` existiert für Liste — Add/Edit/Delete nutzen gleiche API, gleichen Cache |
| Wizard-Hook-Granularität | A) Ein Hook pro Step, B) Ein Hook pro Flow | **TBD** | Abhängig von Shared-State-Analyse |
| Container-Gruppierungslogik | A) Im Hook, B) Separate Utility | **A) Im Hook** | Gruppierung ist View-Model-Logik, nicht wiederverwendbar |
