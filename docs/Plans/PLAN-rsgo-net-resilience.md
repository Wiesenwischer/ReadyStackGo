<!-- GitHub Epic: #330 -->
# Phase: Self-Update rsgo-net Network Resilience

## Ziel

Nach einem Self-Update muss der neue RSGO Container **immer** auf dem `rsgo-net` Docker Network sein, unabhängig davon ob der alte Container es hatte oder nicht. Zusätzlich soll beim Application-Start geprüft werden ob rsgo-net vorhanden ist und bei Bedarf automatisch verbunden werden.

## Analyse

### Ist-Zustand

**SelfUpdateService** (`src/ReadyStackGo.Infrastructure.Docker/SelfUpdateService.cs`):
- `ConnectAdditionalNetworks()` (Zeile 355-386) kopiert nur Netzwerke vom alten Container
- Wenn der alte Container rsgo-net verloren hatte → neuer Container bekommt es auch nicht
- Selbst-perpetuierender Fehler

**DeploymentEngine** (`src/ReadyStackGo.Infrastructure/Services/Deployment/DeploymentEngine.cs`):
- Konstante `ManagementNetwork = "rsgo-net"` (Zeile 24-28)
- Alle deployed Container werden explizit auf rsgo-net verbunden (Zeile 829-834)
- RSGO selbst wird aber nicht geprüft

**HealthMonitoringService** (`src/ReadyStackGo.Application/Services/Impl/HealthMonitoringService.cs`):
- HTTP Health Checks nutzen Container-Hostnamen via Docker DNS (Zeile 277-278)
- Docker DNS funktioniert nur innerhalb desselben Netzwerks
- Ohne rsgo-net → `Resource temporarily unavailable` → alle Stacks Unhealthy

### Beobachtetes Problem (2026-02-26, erneut 2026-03-17)

1. RSGO Container verliert rsgo-net (Ursache unklar, möglicherweise Docker Engine Neustart)
2. Self-Update erstellt neuen Container → kopiert Netzwerke des alten (ohne rsgo-net)
3. Neuer Container hat kein rsgo-net → Health Checks schlagen fehl
4. Workaround: `docker network connect rsgo-net readystackgo`

## AMS UI Counterpart

- [x] **Nein** — reine Backend/Infrastructure-Änderung

## Features / Schritte

- [ ] **Feature 1: SelfUpdateService — rsgo-net garantieren** – Nach Container-Erstellung immer rsgo-net verbinden
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure.Docker/SelfUpdateService.cs`
  - Änderungen:
    - In `ConnectAdditionalNetworks()`: rsgo-net immer hinzufügen, auch wenn der alte Container es nicht hatte
    - Vor dem Connect: `rsgo-net` erstellen falls es nicht existiert (analog DeploymentEngine Zeile 214)
  - Pattern-Vorlage: `DeploymentEngine.EnsureNetworkExists()` (Zeile 210-220)
  - Abhängig von: -

- [ ] **Feature 2: Startup-Check — rsgo-net beim Start prüfen** – Beim Application-Start prüfen ob RSGO auf rsgo-net ist
  - Betroffene Dateien:
    - `src/ReadyStackGo.Api/BackgroundServices/HealthCollectorBackgroundService.cs` (oder neuer Startup-Task)
  - Änderungen:
    - Beim Start: eigene Container-ID ermitteln (über Hostname oder `/proc/1/cpuset`)
    - Prüfen ob Container auf rsgo-net ist
    - Falls nicht: automatisch `docker network connect rsgo-net {containerId}`
    - Log-Warning ausgeben
  - Abhängig von: -

- [ ] **Feature 3: Unit Tests**
  - SelfUpdateService: rsgo-net wird immer verbunden (auch wenn alter Container es nicht hatte)
  - Startup-Check: Logging und Auto-Connect
  - Abhängig von: Feature 1, 2

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**: SelfUpdateService mit Mock-Docker-API (rsgo-net in ConnectAdditionalNetworks)
- **Manueller Test**: Self-Update auf test-ux-dokker, Health Checks nach Update prüfen

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Wo rsgo-net sicherstellen | A: Nur SelfUpdate, B: SelfUpdate + Startup, C: Nur Startup | B | Belt-and-suspenders: SelfUpdate verhindert den Fehler, Startup-Check repariert bestehende Installationen |
| Netzwerk-Name | A: Hardcoded "rsgo-net", B: Konfigurierbar | A | Konsistent mit DeploymentEngine.ManagementNetwork, kein Use Case für andere Namen |
