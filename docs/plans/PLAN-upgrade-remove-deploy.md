<!-- GitHub Epic: #417 -->
# Phase: Upgrade Flow — sequential Remove → Deploy

## Ziel
Der Produkt-**Upgrade**-Flow soll pro Stack als sequentielles **Remove → Deploy** ablaufen — analog zum bestehenden **Redeploy** — damit Upgrades dieselben detaillierten Schritt-für-Schritt-Fortschritte in der UI zeigen wie ein manuelles Remove gefolgt von Deploy. Heute nutzt der Upgrade-Screen eine eigene, gröbere Fortschrittsdarstellung, die „nicht gut aktualisiert".

## Analyse

### Bestehende Architektur

**Redeploy implementiert das Zielmuster bereits** und dient als Vorlage:
- Backend `RedeployProductHandler` (`src/ReadyStackGo.Application/UseCases/Deployments/RedeployProduct/RedeployProductHandler.cs`, Z.171–267): pro Stack zweiphasig **Remove → Deploy**, mit `RemoveDeploymentAsync()` gefolgt von `DeployStackCommand`, und granularen Benachrichtigungen (`NotifyProductProgressAsync` mit `phase: "Removing"`/`"Redeploying"`, `NotifyStackCompletedAsync`).
- Frontend `RedeployProduct.tsx` + `useRedeployProductStore.ts`: Split-View (Stack-Liste links, Detail-Panel rechts), wiederverwendetes `DeploymentProgressPanel`, **Per-Stack-Logs** (`perStackProgress`/`perStackLogs`), 5 Status `pending | removing | deploying | running | failed`.

**Upgrade ist heute bespoke/gröber:**
- Backend `UpgradeProductHandler` (`src/ReadyStackGo.Application/UseCases/Deployments/UpgradeProduct/UpgradeProductHandler.cs`): entfernt nur *wegfallende* Stacks (Z.134–168) und deployt danach alle Ziel-Stacks via `DeployStackCommand`. Das eigentliche Entfernen alter Container passiert implizit in der `DeploymentEngine`-Phase „RemovingOldContainers" — nicht als sichtbarer, eigener Schritt. Progress nur auf Produkt-Ebene (Phase „ProductUpgrade", Z.227–229, `NotifyProgressAsync` Z.440–463).
- Frontend `UpgradeProduct.tsx` (Upgrading-Phase Z.249–412): **kein** `DeploymentProgressPanel`, einzelner Fortschrittsbalken, simple Stack-Liste, globale Init-Container-Logs. `useUpgradeProductStore.ts`: globaler `progressUpdate` + `initContainerLogs`, 4 Status (`pending | upgrading | running | failed`).

### Upgrade-Spezifika gegenüber Redeploy
Redeploy bleibt auf derselben Version; Upgrade wechselt die Version → drei Stack-Fälle:
1. Stack **nur in alter** Version (in Zielversion entfernt) → **nur Remove**
2. Stack **nur in neuer** Version (in Zielversion hinzugefügt) → **nur Deploy**
3. Stack **in beiden** Versionen → **Remove (alt) → Deploy (neu)** (Redeploy-Muster, ggf. mit geänderten Image-Tags/Variablen)

Versions-Bookkeeping bleibt: `ProductDeployment.InitiateUpgrade()` (neues Aggregat in Status `Upgrading`), altes Aggregat → `Superseded`, Variablen-Merge (4-Tier: Stack-Defaults < bestehende Werte < Shared-Overrides < Per-Stack-Overrides).

### Betroffene Bounded Contexts
- **Domain**: keine neuen Entities. Prüfen, ob `ProductDeploymentStatus`-Übergänge ausreichen (`Upgrading → Running/PartiallyRunning/Failed`); ggf. Stack-Status-Reset analog `StartRedeploy`.
- **Application**: `UpgradeProductHandler` umbauen (per-Stack Remove→Deploy, Versions-Diff-Logik, granulare Notifications). Wiederverwendung von `RemoveDeploymentAsync` + `DeployStackCommand`.
- **Infrastructure**: keine Änderung erwartet — `DeploymentEngine` (Remove- und Deploy-Phasen) sowie `DeploymentNotificationService`/`DeploymentHub` werden wiederverwendet.
- **API**: `UpgradeProductEndpoint`/`UpgradeProductCommand` Signatur bleibt; ggf. SignalR-`phase`-Werte angleichen.
- **WebUI (rsgo-generic)**: `UpgradeProduct.tsx` auf Split-View + `DeploymentProgressPanel`; `useUpgradeProductStore` auf Per-Stack-Progress/Logs + `removing`-Status (Vorlage: `useRedeployProductStore`).

## AMS UI Counterpart

> RSGO hat zwei UI-Distributionen mit unterschiedlichen Design-Systemen:
> - **rsgo-generic**: React + Tailwind CSS (Referenz, `packages/ui-generic`)
> - **AMS UI**: ConsistentUI/Lit Web Components (separates Repo `ReadyStackGo.Ams`)
>
> Geteilte Logik liegt in `@rsgo/core` (Hooks, API, State). Seiten/Layouts müssen pro Distribution reimplementiert werden.

**Benötigt AMS UI eine Entsprechung?**

- [x] **Ja (deferred)** — Die `@rsgo/core`-Änderungen (`useUpgradeProductStore`, `api/deployments.ts`, `useDeploymentHub`) kommen beiden UIs zugute. Die AMS-eigene Seite `packages/ui-ams/src/pages/Deployments/UpgradeProduct.tsx` muss auf das neue Per-Stack-/Split-View-Muster umgebaut werden. Eigenes PLAN file im AMS-Repo wird **jetzt** angelegt, Implementierung **deferred**.
- AMS-Plan: `C:\proj\ReadyStackGo.Ams\docs\Plans\PLAN-upgrade-remove-deploy.md` (Vorlage: dortiges `PLAN-redeploy-remove-product-ui.md`).

## Features / Schritte

Reihenfolge basierend auf Abhängigkeiten:

- [ ] **Feature 1: Backend — UpgradeProductHandler auf per-Stack Remove → Deploy** – Versions-Diff (removed/added/common), pro Stack zweiphasig Remove→Deploy, granulare SignalR-Notifications.
  - Betroffene Dateien: `UpgradeProductHandler.cs`
  - Pattern-Vorlage: `RedeployProductHandler.cs` (Z.171–267)
  - Abhängig von: -
- [ ] **Feature 2: @rsgo/core — useUpgradeProductStore erweitern** – `perStackProgress`/`perStackLogs`, Status um `removing` erweitern, phase-aware Routing der SignalR-Events.
  - Betroffene Dateien: `packages/core/src/hooks/useUpgradeProductStore.ts`, ggf. `api/deployments.ts`
  - Pattern-Vorlage: `useRedeployProductStore.ts`
  - Abhängig von: Feature 1 (SignalR-Phasen)
- [ ] **Feature 3: rsgo-generic — UpgradeProduct.tsx auf Split-View** – Stack-Liste + Detail-Panel mit `DeploymentProgressPanel`, Per-Stack-Logs.
  - Betroffene Dateien: `packages/ui-generic/src/pages/Deployments/UpgradeProduct.tsx`
  - Pattern-Vorlage: `RedeployProduct.tsx`, `components/deployments/DeploymentProgressPanel.tsx`
  - Abhängig von: Feature 2
- [ ] **Feature 4: AMS UI Counterpart (deferred)** – Eigenes PLAN file im AMS-Repo; Umsetzung der AMS-`UpgradeProduct.tsx` separat.
  - Abhängig von: Feature 2
- [ ] **Dokumentation & Website** – Wiki, Public Website (DE/EN), Roadmap
- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie
- **Unit Tests**: `UpgradeProductHandler` — Versions-Diff-Fälle: Stack nur alt (nur Remove), Stack nur neu (nur Deploy), Stack in beiden (Remove→Deploy); `ContinueOnError=false` bricht bei Stack-Fehler ab; korrektes Versions-Bookkeeping (`Superseded`/`InitiateUpgrade`). Edge Cases: leeres Stack-Set, identische Stacks, fehlgeschlagenes Remove vor Deploy.
- **Integration Tests**: Upgrade-Endpoint end-to-end gegen Test-DB — Status-Übergänge, Stack-Status-Sequenz, Notifications.
- **E2E Tests**: Upgrade eines Multi-Stack-Produkts; UI zeigt pro Stack Remove- dann Deploy-Phase mit Detail-Panel und Live-Logs (Playwright + Screenshots für die Doku).

## Offene Punkte
- [ ] Soll bei „Stack in beiden Versionen" immer Remove→Deploy laufen, oder darf bei identischem Image-Tag + identischen Variablen der Stack übersprungen werden (Skip-Optimierung)? Default-Vorschlag: immer Remove→Deploy für Konsistenz, später optional Skip.
- [ ] Verhalten bei `ContinueOnError`: gilt es pro Stack-Gesamtschritt (Remove+Deploy) oder getrennt? Vorschlag: ein fehlgeschlagenes Remove markiert den Stack failed und überspringt Deploy (analog Redeploy Z.185–186).
- [ ] Reihenfolge: Remove in Reverse-Order (`GetStacksInRemoveOrder`) und Deploy in Forward-Order (`GetStacksInDeployOrder`) — oder pro Stack interleaved wie Redeploy? Vorschlag: interleaved pro Stack (Redeploy-Muster), da das die saubere UI-Sequenz liefert.

## Entscheidungen
| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Milestone | v0.71 / v0.63 / v1.0 | **v0.71** | Klar abgegrenzte Upgrade-UX-Verbesserung, eigener Release nach v0.70.0 |
| AMS-UI-Counterpart | jetzt / deferred / keiner | **deferred** | `@rsgo/core` kommt beiden zugute; AMS-Seite separat, Plan jetzt angelegt |
| Orchestrierungs-Vorlage | Redeploy / eigenständig | **Redeploy** | Redeploy implementiert per-Stack Remove→Deploy + Detail-UI bereits |
