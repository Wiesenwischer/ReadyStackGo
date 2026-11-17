<!-- GitHub Epic: #335 -->
# Phase: SSH Environment — Docker-native Health Checks

## Ziel

Für SSH Tunnel Environments sollen HTTP Health Checks übersprungen werden, da RSGO nicht auf dem Remote Docker Netzwerk ist und Container-Hostnamen nicht auflösen kann. Stattdessen werden nur Docker-native Status-Informationen genutzt (Container Status + Docker HEALTHCHECK).

## Analyse

### Problem

`HealthMonitoringService.CollectServiceHealthAsync()` führt HTTP Health Checks durch indem es `http://container-name:port/hc` aufruft. Das funktioniert nur wenn RSGO auf dem gleichen Docker Netzwerk wie die Container ist (rsgo-net). Für SSH Tunnel Environments ist RSGO auf dem lokalen Docker, die Container auf dem Remote Host — DNS-Auflösung unmöglich.

### Bestehende Architektur

**Health Check Entscheidung** (`HealthMonitoringService.cs` Zeile 220-247):
```csharp
if (healthConfig != null && _httpHealthChecker != null)
    → HTTP Health Check (via container hostname)
else
    → Docker Status Fallback (via Docker API)
```

**Docker Status Fallback** (`DetermineHealthStatusFromDocker`):
- Liest `container.State` (running/stopped) und `container.HealthStatus` (Docker HEALTHCHECK)
- Funktioniert über Docker API → funktioniert über SSH Tunnel ✓

**Flow**: `HealthCollectorService.CollectEnvironmentHealthAsync()` → kennt die `EnvironmentId` → kann den Environment-Type prüfen

### Lösung

In `HealthCollectorService.CollectEnvironmentHealthAsync()`: Wenn Environment.Type == SshTunnel → `serviceHealthConfigs = null` übergeben → HTTP Health Checks werden übersprungen → Docker Status Fallback greift.

## AMS UI Counterpart

- [x] **Nein** — reine Backend-Änderung

## Features / Schritte

- [ ] **Feature 1: Skip HTTP Health Checks für SSH Environments** – `serviceHealthConfigs` auf `null` setzen wenn Environment Type == SshTunnel
  - Betroffene Dateien:
    - `src/ReadyStackGo.Infrastructure/Services/Health/HealthCollectorService.cs`
  - Änderung: In `CollectEnvironmentHealthAsync()`, nach dem Laden des Environments prüfen ob `environment.Type == EnvironmentType.SshTunnel`. Wenn ja, `serviceHealthConfigs` als `null` an `CaptureHealthSnapshotAsync()` übergeben.
  - Abhängig von: -

- [ ] **Phase abschließen** – Alle Tests grün, PR gegen main

## Test-Strategie

- **Unit Tests**: HealthCollectorService mit SSH Environment → keine HTTP Health Checks
- **Manueller Test**: Health Dashboard für Test (SSH) Environment → stabile Status ohne Flipping

## Entscheidungen

| Entscheidung | Optionen | Gewählt | Begründung |
|---|---|---|---|
| Wo überspringen | A: HealthMonitoringService, B: HealthCollectorService | B | HealthCollectorService kennt das Environment, HealthMonitoringService ist environment-agnostisch |
| Langfristige Lösung | A: Docker exec curl, B: Docker HEALTHCHECK only, C: RSGO Agent | B (jetzt) + C (später) | Docker exec braucht curl im Container. Docker HEALTHCHECK reicht als Zwischenlösung. Agent kommt in v1.0 |
