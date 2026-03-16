<!-- GitHub Epic: #306 -->
# Phase: Deployment Error Display Redesign

## Ziel

Deployment-Fehlermeldungen von rohen Text-Blöcken zu strukturierten, lesbaren Fehleranzeigen umbauen. Jeder Fehler zeigt: Zusammenfassung (was ist fehlgeschlagen), Container/Script-Name, und klappbare Details (Stack Trace, Logs).

## Analyse

### Bestehende Architektur

**Aktuell:** Fehler werden als einzelner roter Text-Block angezeigt (`deployment.errorMessage` / `stack.errorMessage`). Bei Init-Container-Fehlern enthält die Message den gesamten Stack Trace als Fließtext — unlesbar.

**Stellen wo Fehler angezeigt werden:**

| Stelle | Datei | Aktuell |
|--------|-------|---------|
| Product Error Alert | `ProductDeploymentDetail.tsx:257-262` | Raw `errorMessage` in rotem Block |
| Stack Error in Table | `ProductDeploymentDetail.tsx:424-426` | Raw `errorMessage` als Text unter Stack-Name |
| Init Container Results | `DeploymentDetail.tsx:434-488` | Exit Code + expandable Logs (schon besser) |
| Deployment Progress | `DeploymentProgressPanel.tsx:44-48` | Status-Message als Text |
| Deployments List | `Deployments.tsx:237-241` | Raw `errorMessage` unter Product-Card |

**Verfügbare Daten in den DTOs:**
- `errorMessage: string` — enthält oft den gesamten Stack Trace
- `InitContainerResultDto`: `serviceName`, `exitCode`, `success`, `logOutput`
- `DeploymentProgressUpdate`: `phase`, `message`, `currentService`, `isError`

### Betroffene Bounded Contexts
- **Domain**: Keine Änderung
- **Application**: Keine Änderung
- **Infrastructure**: Keine Änderung
- **API**: Keine Änderung
- **WebUI (rsgo-generic)**: Neue Error-Komponente, Refactoring bestehender Fehleranzeigen

## AMS UI Counterpart

- [x] **Ja (deferred)** — AMS-Counterpart wird später geplant
  - Begründung: RSGO Generic UI ist die Referenz-Implementierung. AMS übernimmt das Pattern im nächsten Release.

## Features / Schritte

- [ ] **Feature 1: DeploymentError Komponente** — Wiederverwendbare Fehler-Komponente
  - Aufbau: Fehler-Icon + Zusammenfassung (1 Zeile) + klappbare Details
  - Zusammenfassung: Erste sinnvolle Zeile aus der Error Message extrahieren (vor "---", vor "at ", vor Stack Trace)
  - Details: Vollständiger Error Text in `<pre>` Block mit Scroll, mono Font
  - Props: `title`, `summary`, `details`, `containerName?`, `exitCode?`
  - Betroffene Dateien:
    - `packages/ui-generic/src/components/ui/DeploymentError.tsx` (neu)

- [ ] **Feature 2: Product Deployment Error** — Strukturierte Fehleranzeige auf Product-Detail
  - Ersetze raw `errorMessage` Block durch `DeploymentError` Komponente
  - Zeige pro fehlgeschlagenem Stack eine eigene Error-Zeile mit Stack-Name
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx`

- [ ] **Feature 3: Stack Error in Stacks-Tabelle** — Kompakte Fehleranzeige pro Stack
  - Stack-Name + kurze Error-Zusammenfassung + "Show Details" Link
  - Details klappen sich aus (inline Accordion)
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Deployments/ProductDeploymentDetail.tsx` (StackRow)

- [ ] **Feature 4: Deployments-Liste** — Fehler in der Übersicht
  - Kompakte Error-Zusammenfassung statt rohem Text
  - Betroffene Dateien:
    - `packages/ui-generic/src/pages/Deployments/Deployments.tsx`

- [ ] **Feature 5: Error Message Parsing Utility** — Helper zum Extrahieren der Zusammenfassung
  - `parseErrorMessage(raw: string): { summary: string; details: string }`
  - Extrahiert erste sinnvolle Zeile als Summary
  - Erkennt Patterns: "Init container 'X' failed", "Failed to pull image", "Service 'X' failed to start"
  - Rest als klappbare Details
  - Betroffene Dateien:
    - `packages/core/src/utils/errorParser.ts` (neu)
  - Pattern-Vorlage: Keins (neu)

- [ ] **Phase abschließen** — Tests grün, PR gegen main

## Test-Strategie
- **Unit Tests**: `errorParser.ts` — verschiedene Error-Formate parsen (Init Container, Image Pull, Generic)
- **E2E Tests**: Visuell prüfen dass Fehler strukturiert angezeigt werden (schwer zu automatisieren ohne echte Fehler)

## Offene Punkte
- (keine)

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Scope | Nur UI, UI + Backend | **Nur UI** | Error Messages kommen unverändert vom Backend. Frontend parst und strukturiert die Anzeige. |
| Error Parsing | Regex, Heuristik, Backend-Kategorisierung | **Heuristik** | Einfachste Lösung: erste Zeile = Summary, Rest = Details. Patterns für bekannte Fehler (Init Container, Image Pull). |
| Klappbare Details | Eigene Accordion-Komponente, HTML `<details>` | **HTML `<details>`** | Nativ, kein JS nötig, barrierefrei, funktioniert mit SSR |
